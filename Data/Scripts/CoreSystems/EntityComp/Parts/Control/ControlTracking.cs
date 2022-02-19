using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Support;
using Sandbox.ModAPI;
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
    }
}
