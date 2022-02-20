using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Support;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
namespace CoreSystems.Platform
{
    public partial class ControlSys : Part
    {
        internal static bool TrajectoryEstimation(Weapon weapon, out Vector3D targetDirection)
        {
            var target = weapon.Target.TargetEntity;
            if (target?.GetTopMostParent()?.Physics?.LinearVelocity == null)
            {
                targetDirection = Vector3D.Zero;
                return false;
            }

            var targetPos = target.PositionComp.WorldAABB.Center;

            var shooter = weapon.Comp.FunctionalBlock;
            var ammoDef = weapon.ActiveAmmoDef.AmmoDef;
            if (ammoDef.Const.IsBeamWeapon)
            {
                targetDirection = Vector3D.Normalize(targetPos - shooter.PositionComp.WorldAABB.Center);
                return true;
            }

            var targetVel = target.GetTopMostParent().Physics.LinearVelocity;
            var shooterVel = shooter.GetTopMostParent().Physics.LinearVelocity;

            var projectileMaxSpeed = ammoDef.Const.DesiredProjectileSpeed;
            var shooterPos = weapon.MyPivotPos;
            Vector3D deltaPos = targetPos - shooterPos;
            Vector3D deltaVel = targetVel - shooterVel;
            Vector3D deltaPosNorm;
            if (Vector3D.IsZero(deltaPos)) deltaPosNorm = Vector3D.Zero;
            else if (Vector3D.IsUnit(ref deltaPos)) deltaPosNorm = deltaPos;
            else Vector3D.Normalize(ref deltaPos, out deltaPosNorm);

            double closingSpeed;
            Vector3D.Dot(ref deltaVel, ref deltaPosNorm, out closingSpeed);

            Vector3D closingVel = closingSpeed * deltaPosNorm;
            Vector3D lateralVel = deltaVel - closingVel;
            double projectileMaxSpeedSqr = projectileMaxSpeed * projectileMaxSpeed;
            double ttiDiff = projectileMaxSpeedSqr - lateralVel.LengthSquared();

            if (ttiDiff < 0)
            {
                targetDirection = shooter.PositionComp.WorldMatrixRef.Forward;
                return false;
            }

            double projectileClosingSpeed = Math.Sqrt(ttiDiff) - closingSpeed;

            double closingDistance;
            Vector3D.Dot(ref deltaPos, ref deltaPosNorm, out closingDistance);

            double timeToIntercept = closingDistance / projectileClosingSpeed;

            if (timeToIntercept < 0)
            {
                targetDirection = shooter.PositionComp.WorldMatrixRef.Forward;
                return false;
            }

            targetDirection = Vector3D.Normalize(targetPos + timeToIntercept * (Vector3D)(targetVel - shooterVel * 1) - shooterPos);
            return true;

        }

        internal static bool TrajectoryEstimation(Weapon weapon, out Vector3D targetDirection, bool force = false)
        {
            var ai = weapon.Comp.Ai;
            var session = ai.Session;
            var ammoDef = weapon.ActiveAmmoDef.AmmoDef;

            var target = weapon.Target.TargetEntity;
            if (target?.GetTopMostParent()?.Physics?.LinearVelocity == null)
            {
                targetDirection = Vector3D.Zero;
                return false;
            }
            
            var targetPos = target.PositionComp.WorldAABB.Center;

            var shooter = weapon.Comp.FunctionalBlock;
            if (ammoDef.Const.IsBeamWeapon)
            {
                targetDirection = Vector3D.Normalize(targetPos - shooter.PositionComp.WorldAABB.Center);
                return true;
            }
            
            if (ai.VelocityUpdateTick != session.Tick)
            {
                ai.TopEntityVolume.Center = ai.TopEntity.PositionComp.WorldVolume.Center;
                ai.GridVel = ai.GridEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                ai.IsStatic = ai.GridEntity.Physics?.IsStatic ?? false;
                ai.VelocityUpdateTick = session.Tick;
            }

            if (ammoDef.Const.FeelsGravity && session.Tick - weapon.GravityTick > 119)
            {
                weapon.GravityTick = session.Tick;
                float interference;
                weapon.GravityPoint = session.Physics.CalculateNaturalGravityAt(weapon.MyPivotPos, out interference);
            }

            var gravityMultiplier = ammoDef.Const.FeelsGravity && !MyUtils.IsZero(weapon.GravityPoint) ? ammoDef.Const.GravityMultiplier : 0f;
            var targetMaxSpeed = weapon.Comp.Session.MaxEntitySpeed;
            var shooterPos = weapon.MyPivotPos;

            var shooterVel = (Vector3D)weapon.Comp.Ai.GridVel;
            var projectileMaxSpeed = ammoDef.Const.DesiredProjectileSpeed;
            var projectileInitSpeed = ammoDef.Trajectory.AccelPerSec * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            var projectileAccMag = ammoDef.Trajectory.AccelPerSec;
            var gravity = weapon.GravityPoint;
            var basic = MyUtils.IsZero(weapon.GravityPoint);
            var targetVel = (Vector3D)target.GetTopMostParent().Physics.LinearVelocity;
            Vector3D deltaPos = targetPos - shooterPos;
            Vector3D deltaVel = targetVel - shooterVel;
            Vector3D deltaPosNorm;
            if (Vector3D.IsZero(deltaPos)) deltaPosNorm = Vector3D.Zero;
            else if (Vector3D.IsUnit(ref deltaPos)) deltaPosNorm = deltaPos;
            else Vector3D.Normalize(ref deltaPos, out deltaPosNorm);

            double closingSpeed;
            Vector3D.Dot(ref deltaVel, ref deltaPosNorm, out closingSpeed);

            Vector3D closingVel = closingSpeed * deltaPosNorm;
            Vector3D lateralVel = deltaVel - closingVel;
            double projectileMaxSpeedSqr = projectileMaxSpeed * projectileMaxSpeed;
            double ttiDiff = projectileMaxSpeedSqr - lateralVel.LengthSquared();

            if (ttiDiff < 0)
            {
                targetDirection = weapon.GetScope.CachedDir;
                return false;
            }

            double projectileClosingSpeed = Math.Sqrt(ttiDiff) - closingSpeed;

            double closingDistance;
            Vector3D.Dot(ref deltaPos, ref deltaPosNorm, out closingDistance);

            double timeToIntercept = ttiDiff < 0 ? 0 : closingDistance / projectileClosingSpeed;

            if (timeToIntercept < 0)
            {
                targetDirection = weapon.GetScope.CachedDir;
                return false;
            }

            double maxSpeedSqr = targetMaxSpeed * targetMaxSpeed;
            double shooterVelScaleFactor = 1;
            bool projectileAccelerates = projectileAccMag > 1e-6;
            bool hasGravity = gravityMultiplier > 1e-6 && !MyUtils.IsZero(weapon.GravityPoint);

            if (!basic && projectileAccelerates)
                shooterVelScaleFactor = Math.Min(1, (projectileMaxSpeed - projectileInitSpeed) / projectileAccMag);

            Vector3D estimatedImpactPoint = targetPos + timeToIntercept * (targetVel - shooterVel * shooterVelScaleFactor);
            if (basic)
            {
                targetDirection = Vector3D.Normalize(estimatedImpactPoint - shooter.PositionComp.WorldAABB.Center);
                return true;
            }
            Vector3D aimDirection = estimatedImpactPoint - shooterPos;

            Vector3D projectileVel = shooterVel;
            Vector3D projectilePos = shooterPos;
            var targetAcc = (Vector3D)target.Physics.LinearAcceleration;

            Vector3D aimDirectionNorm;
            if (projectileAccelerates)
            {

                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else aimDirectionNorm = Vector3D.Normalize(aimDirection);
                projectileVel += aimDirectionNorm * projectileInitSpeed;
            }
            else
            {

                if (targetAcc.LengthSquared() < 1 && !hasGravity)
                {
                    targetDirection = Vector3D.Normalize(estimatedImpactPoint - shooter.PositionComp.WorldAABB.Center);
                    return true;
                }

                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else Vector3D.Normalize(ref aimDirection, out aimDirectionNorm);
                projectileVel += aimDirectionNorm * projectileMaxSpeed;
            }

            var count = projectileAccelerates ? 600 : hasGravity ? 320 : 60;

            double dt = Math.Max(MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS, timeToIntercept / count); // This can be a const somewhere
            double dtSqr = dt * dt;
            Vector3D targetAccStep = targetAcc * dt;
            Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt;

            Vector3D aimOffset = Vector3D.Zero;
            double minTime = 0;

            for (int i = 0; i < count; ++i)
            {

                targetVel += targetAccStep;

                if (targetVel.LengthSquared() > maxSpeedSqr)
                {
                    Vector3D targetNormVel;
                    Vector3D.Normalize(ref targetVel, out targetNormVel);
                    targetVel = targetNormVel * targetMaxSpeed;

                }

                targetPos += targetVel * dt;
                if (projectileAccelerates)
                {

                    projectileVel += projectileAccStep;
                    if (projectileVel.LengthSquared() > projectileMaxSpeedSqr)
                    {
                        Vector3D pNormVel;
                        Vector3D.Normalize(ref projectileVel, out pNormVel);
                        projectileVel = pNormVel * projectileMaxSpeed;
                    }
                }

                projectilePos += projectileVel * dt;
                Vector3D diff = (targetPos - projectilePos);
                double diffLenSq = diff.LengthSquared();
                aimOffset = diff;
                minTime = dt * (i + 1);

                if (diffLenSq < projectileMaxSpeedSqr * dtSqr || Vector3D.Dot(diff, aimDirectionNorm) < 0)
                    break;
            }
            Vector3D perpendicularAimOffset = aimOffset - Vector3D.Dot(aimOffset, aimDirectionNorm) * aimDirectionNorm;
            Vector3D gravityOffset = hasGravity ? -0.5 * minTime * minTime * gravity : Vector3D.Zero;

            targetDirection = Vector3D.Normalize((estimatedImpactPoint + perpendicularAimOffset + gravityOffset) - shooter.PositionComp.WorldAABB.Center);

            return true;
        }

    }
}
