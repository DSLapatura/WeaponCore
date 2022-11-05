using System;
using CoreSystems.Support;
using Jakaria;
using Sandbox.Engine.Physics;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition.HardPointDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using System.Runtime.CompilerServices;

namespace CoreSystems.Platform
{
    public partial class Weapon
    {
        internal static bool CanShootTarget(Weapon weapon, ref Vector3D targetCenter, Vector3D targetLinVel, Vector3D targetAccel, out Vector3D targetPos, bool checkSelfHit = false, MyEntity target = null)
        {
            if (weapon.PosChangedTick != weapon.Comp.Session.SimulationCount)
                weapon.UpdatePivotPos();

            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretController ? weapon : weapon.Comp.PrimaryWeapon;
            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var validEstimate = true;
            if (prediction != Prediction.Off && !weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
                targetPos = TrajectoryEstimation(weapon, targetCenter, targetLinVel, targetAccel, Vector3D.Zero, out validEstimate);
            else
                targetPos = targetCenter;
            var targetDir = targetPos - weapon.MyPivotPos;


            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            var inRange = rangeToTarget <= weapon.MaxTargetDistanceSqr && rangeToTarget >= weapon.MinTargetDistanceSqr;
            bool canTrack;
            bool isTracking;

            if (weapon.RotorTurretTracking)
                canTrack = validEstimate && MathFuncs.RotorTurretLookAt(weapon.MasterComp.Platform.Control, ref targetDir, rangeToTarget);
            else if (weapon == trackingWeapon)
                canTrack = validEstimate && MathFuncs.WeaponLookAt(weapon, ref targetDir, rangeToTarget, false, true, out isTracking);
            else
                canTrack = validEstimate && MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);

            bool selfHit = false;
            weapon.LastHitInfo = null;
            if (checkSelfHit && target != null)
            {

                var testLine = new LineD(targetCenter, weapon.BarrelOrigin);
                var predictedMuzzlePos = testLine.To + (-testLine.Direction * weapon.MuzzleDistToBarrelCenter);
                var ai = weapon.Comp.Ai;
                var localPredictedPos = Vector3I.Round(Vector3D.Transform(predictedMuzzlePos, ai.GridEntity.PositionComp.WorldMatrixNormalizedInv) * ai.GridEntity.GridSizeR);

                MyCube cube;
                var noCubeAtPosition = !ai.GridEntity.TryGetCube(localPredictedPos, out cube);
                if (noCubeAtPosition || cube.CubeBlock == weapon.Comp.Cube.SlimBlock)
                {

                    var noCubeInLine = !ai.GridEntity.GetIntersectionWithLine(ref testLine, ref ai.GridHitInfo);
                    var noCubesInLineOrHitSelf = noCubeInLine || ai.GridHitInfo.Position == weapon.Comp.Cube.Position;

                    if (noCubesInLineOrHitSelf)
                    {

                        weapon.System.Session.Physics.CastRay(predictedMuzzlePos, testLine.From, out weapon.LastHitInfo, CollisionLayers.DefaultCollisionLayer);

                        if (weapon.LastHitInfo != null && weapon.LastHitInfo.HitEntity == ai.GridEntity)
                            selfHit = true;
                    }
                }
                else selfHit = true;
            }
            return !selfHit && (inRange && canTrack || weapon.Comp.Data.Repo.Values.State.TrackingReticle);
        }

        internal static bool CheckSelfHit(Weapon w, ref Vector3D targetPos, ref Vector3D testPos, out Vector3D predictedMuzzlePos)
        {

            var testLine = new LineD(targetPos, testPos);
            predictedMuzzlePos = testLine.To + (-testLine.Direction * w.MuzzleDistToBarrelCenter);
            var ai = w.Comp.Ai;
            var localPredictedPos = Vector3I.Round(Vector3D.Transform(predictedMuzzlePos, ai.GridEntity.PositionComp.WorldMatrixNormalizedInv) * ai.GridEntity.GridSizeR);

            MyCube cube;
            var noCubeAtPosition = !ai.GridEntity.TryGetCube(localPredictedPos, out cube);
            if (noCubeAtPosition || cube.CubeBlock == w.Comp.Cube.SlimBlock)
            {

                var noCubeInLine = !ai.GridEntity.GetIntersectionWithLine(ref testLine, ref ai.GridHitInfo);
                var noCubesInLineOrHitSelf = noCubeInLine || ai.GridHitInfo.Position == w.Comp.Cube.Position;

                if (noCubesInLineOrHitSelf)
                {

                    w.System.Session.Physics.CastRay(predictedMuzzlePos, testLine.From, out w.LastHitInfo, CollisionLayers.DefaultCollisionLayer);

                    if (w.LastHitInfo != null && w.LastHitInfo.HitEntity == ai.GridEntity)
                        return true;
                }
            }
            else return true;

            return false;
        }

        internal static void LeadTarget(Weapon weapon, MyEntity target, out Vector3D targetPos, out bool couldHit, out bool willHit)
        {
            if (weapon.PosChangedTick != weapon.Comp.Session.SimulationCount)
                weapon.UpdatePivotPos();

            var vel = target.Physics.LinearVelocity;
            var accel = target.Physics.LinearAcceleration;
            var trackingWeapon = weapon.TurretController || weapon.Comp.PrimaryWeapon == null ? weapon : weapon.Comp.PrimaryWeapon;

            var box = target.PositionComp.LocalAABB;
            var obb = new MyOrientedBoundingBoxD(box, target.PositionComp.WorldMatrixRef);

            var validEstimate = true;
            var advancedMode = weapon.ActiveAmmoDef.AmmoDef.Trajectory.AccelPerSec > 0 || weapon.Comp.Ai.InPlanetGravity && weapon.ActiveAmmoDef.AmmoDef.Const.FeelsGravity;

            if (!weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
                targetPos = TrajectoryEstimation(weapon, obb.Center, vel, accel, Vector3D.Zero, out validEstimate, true, advancedMode, weapon.System.Prediction != Prediction.Advanced);
            else
                targetPos = obb.Center;

            obb.Center = targetPos;
            weapon.TargetBox = obb;

            var obbAbsMax = obb.HalfExtent.AbsMax();
            var maxRangeSqr = obbAbsMax + weapon.MaxTargetDistance;
            var minRangeSqr = obbAbsMax + weapon.MinTargetDistance;

            maxRangeSqr *= maxRangeSqr;
            minRangeSqr *= minRangeSqr;
            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            couldHit = validEstimate && rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr;

            bool canTrack = false;
            if (validEstimate && rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr)
            {
                var targetDir = targetPos - weapon.MyPivotPos;
                if (weapon == trackingWeapon)
                {
                    double checkAzimuth;
                    double checkElevation;

                    MathFuncs.GetRotationAngles(ref targetDir, ref weapon.WeaponConstMatrix, out checkAzimuth, out checkElevation);

                    var azConstraint = Math.Min(weapon.MaxAzToleranceRadians, Math.Max(weapon.MinAzToleranceRadians, checkAzimuth));
                    var elConstraint = Math.Min(weapon.MaxElToleranceRadians, Math.Max(weapon.MinElToleranceRadians, checkElevation));

                    Vector3D constraintVector;
                    Vector3D.CreateFromAzimuthAndElevation(azConstraint, elConstraint, out constraintVector);
                    Vector3D.Rotate(ref constraintVector, ref weapon.WeaponConstMatrix, out constraintVector);

                    var testRay = new RayD(ref weapon.MyPivotPos, ref constraintVector);
                    if (obb.Intersects(ref testRay) != null)
                        canTrack = true;

                    if (weapon.Comp.Debug)
                        weapon.LimitLine = new LineD(weapon.MyPivotPos, weapon.MyPivotPos + (constraintVector * weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory));
                }
                else
                    canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);
            }
            willHit = canTrack;
        }

        internal static bool CanShootTargetObb(Weapon weapon, MyEntity entity, Vector3D targetLinVel, Vector3D targetAccel, out Vector3D targetPos)
        {   
            if (weapon.PosChangedTick != weapon.Comp.Session.SimulationCount)
                weapon.UpdatePivotPos();

            var prediction = weapon.System.Values.HardPoint.AimLeadingPrediction;
            var trackingWeapon = weapon.TurretController ? weapon : weapon.Comp.PrimaryWeapon;

            if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
            if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

            var box = entity.PositionComp.LocalAABB;
            var obb = new MyOrientedBoundingBoxD(box, entity.PositionComp.WorldMatrixRef);
            var tempObb = obb;
            var validEstimate = true;
            
            if (prediction != Prediction.Off && !weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
                targetPos = TrajectoryEstimation(weapon, obb.Center, targetLinVel, targetAccel, Vector3D.Zero, out validEstimate);
            else
                targetPos = obb.Center;

            obb.Center = targetPos;
            weapon.TargetBox = obb;

            var obbAbsMax = obb.HalfExtent.AbsMax();
            var maxRangeSqr = obbAbsMax + weapon.MaxTargetDistance;
            var minRangeSqr = obbAbsMax + weapon.MinTargetDistance;

            maxRangeSqr *= maxRangeSqr;
            minRangeSqr *= minRangeSqr;
            double rangeToTarget;
            if (weapon.ActiveAmmoDef.AmmoDef.Const.FeelsGravity) Vector3D.DistanceSquared(ref tempObb.Center, ref weapon.MyPivotPos, out rangeToTarget);
            else Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);

            bool canTrack = false;
            if (validEstimate && rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr)
            {
                var targetDir = targetPos - weapon.MyPivotPos;
                if (weapon.RotorTurretTracking)
                    canTrack = MathFuncs.RotorTurretLookAt(weapon.MasterComp.Platform.Control, ref targetDir, rangeToTarget);
                else if (weapon == trackingWeapon)
                {
                    double checkAzimuth;
                    double checkElevation;

                    MathFuncs.GetRotationAngles(ref targetDir, ref weapon.WeaponConstMatrix, out checkAzimuth, out checkElevation);
                    var azConstraint = Math.Min(weapon.MaxAzToleranceRadians, Math.Max(weapon.MinAzToleranceRadians, checkAzimuth));
                    var elConstraint = Math.Min(weapon.MaxElToleranceRadians, Math.Max(weapon.MinElToleranceRadians, checkElevation));

                    Vector3D constraintVector;
                    Vector3D.CreateFromAzimuthAndElevation(azConstraint, elConstraint, out constraintVector);
                    Vector3D.Rotate(ref constraintVector, ref weapon.WeaponConstMatrix, out constraintVector);

                    var testRay = new RayD(ref weapon.MyPivotPos, ref constraintVector);
                    if (obb.Intersects(ref testRay) != null) canTrack = true;

                    if (weapon.Comp.Debug)
                        weapon.LimitLine = new LineD(weapon.MyPivotPos, weapon.MyPivotPos + (constraintVector * weapon.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory));
                }
                else
                    canTrack = MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);
            }
            return canTrack;
        }

        internal static bool TargetAligned(Weapon weapon, Target target, out Vector3D targetPos)
        {

            if (weapon.PosChangedTick != weapon.Comp.Session.SimulationCount)
                weapon.UpdatePivotPos();

            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            Vector3D targetCenter;

            Ai.FakeTarget.FakeWorldTargetInfo fakeTargetInfo = null;
            if (weapon.Comp.Data.Repo.Values.Set.Overrides.Control != ProtoWeaponOverrides.ControlModes.Auto && weapon.ValidFakeTargetInfo(weapon.Comp.Data.Repo.Values.State.PlayerId, out fakeTargetInfo))
            {
                targetCenter = fakeTargetInfo.WorldPosition;
            }
            else if (target.TargetState == Target.TargetStates.IsProjectile)
                targetCenter = target.Projectile?.Position ?? Vector3D.Zero;
            else if (target.TargetState != Target.TargetStates.IsFake)
                targetCenter = target.TargetEntity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            else
                targetCenter = Vector3D.Zero;

            var validEstimate = true;
            if (weapon.System.Prediction != Prediction.Off && (!weapon.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && weapon.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0))
            {

                if (fakeTargetInfo != null)
                {
                    targetLinVel = fakeTargetInfo.LinearVelocity;
                    targetAccel = fakeTargetInfo.Acceleration;
                }
                else
                {

                    var cube = target.TargetEntity as MyCubeBlock;
                    var topMostEnt = cube != null ? cube.CubeGrid : target.TargetEntity;

                    if (target.Projectile != null)
                    {
                        targetLinVel = target.Projectile.Velocity;
                        targetAccel = target.Projectile.MaxAccelVelocity;
                    }
                    else if (topMostEnt?.Physics != null)
                    {
                        targetLinVel = topMostEnt.Physics.LinearVelocity;
                        targetAccel = topMostEnt.Physics.LinearAcceleration;
                    }
                }
                if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
                if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

                targetPos = TrajectoryEstimation(weapon, targetCenter, targetLinVel, targetAccel, Vector3D.Zero, out validEstimate);
            }
            else
                targetPos = targetCenter;

            var targetDir = targetPos - weapon.MyPivotPos;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref weapon.MyPivotPos, out rangeToTarget);
            var inRange = rangeToTarget <= weapon.MaxTargetDistanceSqr && rangeToTarget >= weapon.MinTargetDistanceSqr;

            var isAligned = validEstimate && (inRange || weapon.Comp.Data.Repo.Values.State.TrackingReticle) && MathFuncs.IsDotProductWithinTolerance(ref weapon.MyPivotFwd, ref targetDir, weapon.AimingTolerance);

            weapon.Target.TargetPos = targetPos;
            weapon.Target.IsAligned = isAligned;

            return isAligned;
        }

        internal static bool TrackingTarget(Weapon w, Target target, out bool targetLock)
        {
            Vector3D targetPos;
            Vector3 targetLinVel = Vector3.Zero;
            Vector3 targetAccel = Vector3.Zero;
            Vector3D targetCenter;
            targetLock = false;

            var baseData = w.Comp.Data.Repo.Values;
            var session = w.System.Session;
            var ai = w.Comp.Ai;

            Ai.FakeTarget.FakeWorldTargetInfo fakeTargetInfo = null;
            if (w.Comp.FakeMode && w.ValidFakeTargetInfo(baseData.State.PlayerId, out fakeTargetInfo))
                targetCenter = fakeTargetInfo.WorldPosition;
            else if (target.TargetState == Target.TargetStates.IsProjectile)
                targetCenter = target.Projectile?.Position ?? Vector3D.Zero;
            else if (target.TargetState != Target.TargetStates.IsFake)
                targetCenter = target.TargetEntity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            else
                targetCenter = Vector3D.Zero;

            var validEstimate = true;
            if (w.System.Prediction != Prediction.Off && !w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && w.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0)
            {

                if (fakeTargetInfo != null)
                {
                    targetLinVel = fakeTargetInfo.LinearVelocity;
                    targetAccel = fakeTargetInfo.Acceleration;
                }
                else
                {
                    var cube = target.TargetEntity as MyCubeBlock;
                    var topMostEnt = cube != null ? cube.CubeGrid : target.TargetEntity;

                    if (target.Projectile != null)
                    {
                        targetLinVel = target.Projectile.Velocity;
                        targetAccel = target.Projectile.MaxAccelVelocity;
                    }
                    else if (topMostEnt?.Physics != null)
                    {
                        targetLinVel = topMostEnt.Physics.LinearVelocity;
                        targetAccel = topMostEnt.Physics.LinearAcceleration;
                    }
                }
                if (Vector3D.IsZero(targetLinVel, 5E-03)) targetLinVel = Vector3.Zero;
                if (Vector3D.IsZero(targetAccel, 5E-03)) targetAccel = Vector3.Zero;

                targetPos = TrajectoryEstimation(w, targetCenter, targetLinVel, targetAccel, Vector3D.Zero, out validEstimate);
            }
            else
                targetPos = targetCenter;

            w.Target.TargetPos = targetPos;

            double rangeToTargetSqr;
            Vector3D.DistanceSquared(ref targetPos, ref w.MyPivotPos, out rangeToTargetSqr);

            var targetDir = targetPos - w.MyPivotPos;
            var readyToTrack = validEstimate && !w.Comp.ResettingSubparts && (baseData.State.TrackingReticle || rangeToTargetSqr <= w.MaxTargetDistanceSqr && rangeToTargetSqr >= w.MinTargetDistanceSqr);

            var locked = true;
            var isTracking = false;

            if (readyToTrack && w.PosChangedTick != w.Comp.Session.SimulationCount)
                    w.UpdatePivotPos();

            if (readyToTrack && baseData.State.Control != ProtoWeaponState.ControlMode.Camera)
            {
                if (MathFuncs.WeaponLookAt(w, ref targetDir, rangeToTargetSqr, true, false, out isTracking))
                {

                    w.ReturingHome = false;
                    locked = false;
                    
                    w.AimBarrel();
                }
            }

            w.Rotating = !locked;

            if (w.HasHardPointSound && w.PlayingHardPointSound && !w.Rotating)
                w.StopHardPointSound();

            var isAligned = false;

            if (isTracking)
                isAligned = locked || MathFuncs.IsDotProductWithinTolerance(ref w.MyPivotFwd, ref targetDir, w.AimingTolerance);

            var wasAligned = w.Target.IsAligned;
            w.Target.IsAligned = isAligned;

            var alignedChange = wasAligned != isAligned;
            if (w.System.DesignatorWeapon && session.IsServer && alignedChange)
            {
                for (int i = 0; i < w.Comp.Platform.Weapons.Count; i++)
                {
                    var weapon = w.Comp.Platform.Weapons[i];
                    if (isAligned && !weapon.System.DesignatorWeapon)
                        weapon.Target.Reset(session.Tick, Target.States.Designator);
                    else if (!isAligned && weapon.System.DesignatorWeapon)
                        weapon.Target.Reset(session.Tick, Target.States.Designator);
                }
            }

            targetLock = isTracking && w.Target.IsAligned;

            if (baseData.State.Control == ProtoWeaponState.ControlMode.Camera || w.Comp.FakeMode || session.IsServer && baseData.Set.Overrides.Repel && ai.DetectionInfo.DroneInRange && target.IsDrone && (session.AwakeCount == w.Acquire.SlotId || ai.Construct.RootAi.Construct.LastDroneTick == session.Tick) && Ai.SwitchToDrone(w))
                return true;

            var rayCheckTest = !w.Comp.Session.IsClient && targetLock && baseData.State.Control != ProtoWeaponState.ControlMode.Camera && (w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance != TrajectoryDef.GuidanceType.Smart && w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance != TrajectoryDef.GuidanceType.DroneAdvanced) && (!w.Casting && session.Tick - w.Comp.LastRayCastTick > 29 || w.System.Values.HardPoint.Other.MuzzleCheck && session.Tick - w.LastMuzzleCheck > 29);

            if (rayCheckTest && !w.RayCheckTest())
                return false;

            return isTracking;
        }

        private const int LosMax = 10;
        private int _losAngle = 11;
        private bool _increase;
        private int GetAngle()
        {
            if (_increase && _losAngle + 1 <= LosMax)
                ++_losAngle;
            else if (_increase)
            {
                _increase = false;
                _losAngle = 9;
            }
            else if (_losAngle - 1 > 0)
                --_losAngle;
            else
            {
                _increase = true;
                _losAngle = 2;
            }
            return _losAngle;
        }

        public bool TargetInRange(MyEntity target)
        {
            var worldVolume = target.PositionComp.WorldVolume;
            var targetPos = worldVolume.Center;
            var tRadius = worldVolume.Radius;
            var maxRangeSqr = tRadius + MaxTargetDistance;
            var minRangeSqr = tRadius + MinTargetDistance;

            maxRangeSqr *= maxRangeSqr;
            minRangeSqr *= minRangeSqr;

            double rangeToTarget;
            Vector3D.DistanceSquared(ref targetPos, ref MyPivotPos, out rangeToTarget);
            var inRange = rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr;
            var block = target as MyCubeBlock;
            var overrides = Comp.Data.Repo.Values.Set.Overrides;
            return inRange && (block == null || !overrides.FocusSubSystem || overrides.SubSystem == WeaponDefinition.TargetingDef.BlockTypes.Any || ValidSubSystemTarget(block, overrides.SubSystem));
        }

        public bool SmartLos()
        {
            _losAngle = 11;
            Comp.Data.Repo.Values.Set.Overrides.Debug = false;
            PauseShoot = false;
            LastSmartLosCheck = Comp.Ai.Session.Tick;
            if (PosChangedTick != System.Session.SimulationCount)
                UpdatePivotPos();
            var info = GetScope.Info;

            var checkLevel = Comp.Ai.IsStatic ? 1 : 5;
            bool losBlocked = false;
            for (int j = 0; j < 10; j++)
            {

                if (losBlocked)
                    break;

                var angle = GetAngle();
                int blockedDir = 0;
                for (int i = 0; i < checkLevel; i++)
                {

                    var source = GetSmartLosPosition(i, ref info, angle);

                    IHitInfo hitInfo;
                    Comp.Ai.Session.Physics.CastRay(source, info.Position, out hitInfo, 15, false);
                    var grid = hitInfo?.HitEntity?.GetTopMostParent() as MyCubeGrid;
                    if (grid != null && grid.IsInSameLogicalGroupAs(Comp.Ai.GridEntity) && grid.GetTargetedBlock(hitInfo.Position + (-info.Direction * 0.1f)) != Comp.Cube.SlimBlock)
                    {

                        if (i == 0)
                            blockedDir = 5;
                        else
                            ++blockedDir;

                        if (blockedDir >= 4 || i > 0 && i > blockedDir)
                            break;
                    }
                }
                losBlocked = blockedDir >= 4;
            }

            PauseShoot = losBlocked;

            return !PauseShoot;
        }

        private Vector3D GetSmartLosPosition(int i, ref Dummy.DummyInfo info, int degrees)
        {
            double angle = MathHelperD.ToRadians(degrees);
            var perpDir = Vector3D.CalculatePerpendicularVector(info.Direction);
            Vector3D up;
            Vector3D.Normalize(ref perpDir, out up);
            Vector3D right;
            Vector3D.Cross(ref info.Direction, ref up, out right);
            var offset = Math.Tan(angle); // angle better be in radians

            var destPos = info.Position;

            switch (i)
            {
                case 0:
                    return destPos + (info.Direction * Comp.Ai.TopEntityVolume.Radius);
                case 1:
                    return destPos + ((info.Direction + up * offset) * Comp.Ai.TopEntityVolume.Radius);
                case 2:
                    return destPos + ((info.Direction - up * offset) * Comp.Ai.TopEntityVolume.Radius);
                case 3:
                    return destPos + ((info.Direction + right * offset) * Comp.Ai.TopEntityVolume.Radius);
                case 4:
                    return destPos + ((info.Direction - right * offset) * Comp.Ai.TopEntityVolume.Radius);
            }

            return Vector3D.Zero;
        }

        internal void SmartLosDebug()
        {
            if (PosChangedTick != System.Session.SimulationCount)
                UpdatePivotPos();

            var info = GetScope.Info;

            var checkLevel = Comp.Ai.IsStatic ? 1 : 5;
            var angle = Comp.Session.Tick20 ? GetAngle() : _losAngle;
            for (int i = 0; i < checkLevel; i++)
            {
                var source = GetSmartLosPosition(i, ref info, angle);
                IHitInfo hitInfo;
                Comp.Ai.Session.Physics.CastRay(source, info.Position, out hitInfo, 15, false);
                var grid = hitInfo?.HitEntity?.GetTopMostParent() as MyCubeGrid;
                var hit = grid != null && grid.IsInSameLogicalGroupAs(Comp.Ai.GridEntity) && grid.GetTargetedBlock(hitInfo.Position + (-info.Direction * 0.1f)) != Comp.Cube.SlimBlock;

                var line = new LineD(source, info.Position);
                DsDebugDraw.DrawLine(line, hit ? Color.Red : Color.Blue, 0.05f);
            }
        }

        internal static Vector3D TrajectoryEstimation(Weapon weapon, Vector3D targetPos, Vector3D targetVel, Vector3D targetAcc, Vector3D shooterPos, out bool valid, bool overrideMode = false, bool setAdvOverride = false, bool skipAccel = false)
        {
            valid = true;
            var ai = weapon.Comp.Ai;
            var session = ai.Session;
            var ammoDef = weapon.ActiveAmmoDef.AmmoDef;

            if (ai.VelocityUpdateTick != session.Tick)
            {
                ai.TopEntityVolume.Center = ai.TopEntity.PositionComp.WorldVolume.Center;
                ai.TopEntityVel = ai.TopEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                ai.IsStatic = ai.TopEntity.Physics?.IsStatic ?? false;
                ai.VelocityUpdateTick = session.Tick;
            }

            var updateGravity = ammoDef.Const.FeelsGravity && ai.InPlanetGravity;

            if (updateGravity && session.Tick - weapon.GravityTick > 119)
            {
                weapon.GravityTick = session.Tick;
                float interference;
                weapon.GravityPoint = session.Physics.CalculateNaturalGravityAt(weapon.MyPivotPos, out interference);
                weapon.GravityUnitDir = weapon.GravityPoint;
                weapon.GravityLength = weapon.GravityUnitDir.Normalize();
            }
            else if (!updateGravity)
                weapon.GravityPoint = Vector3D.Zero;

            var gravityMultiplier = ammoDef.Const.FeelsGravity && !MyUtils.IsZero(weapon.GravityPoint) ? ammoDef.Const.GravityMultiplier : 0f;
            bool hasGravity = gravityMultiplier > 1e-6 && !MyUtils.IsZero(weapon.GravityPoint);

            var targetMaxSpeed = weapon.Comp.Session.MaxEntitySpeed;
            shooterPos = MyUtils.IsZero(shooterPos) ? weapon.MyPivotPos : shooterPos;

            var shooterVel = (Vector3D)weapon.Comp.Ai.TopEntityVel;
            var projectileMaxSpeed = ammoDef.Const.DesiredProjectileSpeed;
            var projectileInitSpeed = ammoDef.Trajectory.AccelPerSec * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            var projectileAccMag = ammoDef.Trajectory.AccelPerSec;
            var basic = weapon.System.Prediction != Prediction.Advanced && !overrideMode || overrideMode && !setAdvOverride;
            
            if (basic && weapon.System.Prediction == Prediction.Accurate && hasGravity && ai.InPlanetGravity)
            {
                basic = false;
                skipAccel = true;
            }

            Vector3D deltaPos = targetPos - shooterPos;
            Vector3D deltaVel = targetVel - shooterVel;
            Vector3D deltaPosNorm;
            double deltaLength = 0;
            if (Vector3D.IsZero(deltaPos))
            {
                deltaPosNorm = Vector3D.Zero;
            }
            else if (Vector3D.IsUnit(ref deltaPos))
            {
                deltaPosNorm = deltaPos;
                deltaLength = 1;
            }
            else
            {
                deltaPosNorm = deltaPos;
                deltaLength = deltaPosNorm.Normalize();
            }

            double closingSpeed;
            Vector3D.Dot(ref deltaVel, ref deltaPosNorm, out closingSpeed);

            Vector3D closingVel = closingSpeed * deltaPosNorm;
            Vector3D lateralVel = deltaVel - closingVel;
            double projectileMaxSpeedSqr = projectileMaxSpeed * projectileMaxSpeed;
            double ttiDiff = projectileMaxSpeedSqr - lateralVel.LengthSquared();

            if (ttiDiff < 0)
            {
                valid = false;
                return targetPos;
            }

            double projectileClosingSpeed = Math.Sqrt(ttiDiff) - closingSpeed;

            double closingDistance;
            Vector3D.Dot(ref deltaPos, ref deltaPosNorm, out closingDistance);

            double timeToIntercept = ttiDiff < 0 ? 0 : closingDistance / projectileClosingSpeed;

            if (timeToIntercept < 0)
            {
                valid = false;
                return targetPos;
            }

            double maxSpeedSqr = targetMaxSpeed * targetMaxSpeed;
            double shooterVelScaleFactor = 1;
            bool projectileAccelerates = projectileAccMag > 1e-6;

            if (!basic && projectileAccelerates)
                shooterVelScaleFactor = Math.Min(1, (projectileMaxSpeed - projectileInitSpeed) / projectileAccMag);

            Vector3D estimatedImpactPoint = targetPos + timeToIntercept * (targetVel - shooterVel * shooterVelScaleFactor);
            
            if (basic)
            {
                return estimatedImpactPoint;
            }

            Vector3D aimDirection = estimatedImpactPoint - shooterPos;

            Vector3D projectileVel = shooterVel;
            Vector3D projectilePos = shooterPos;

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
                    return estimatedImpactPoint;
                }

                if (Vector3D.IsZero(deltaPos)) aimDirectionNorm = Vector3D.Zero;
                else if (Vector3D.IsUnit(ref deltaPos)) aimDirectionNorm = aimDirection;
                else Vector3D.Normalize(ref aimDirection, out aimDirectionNorm);
                projectileVel += aimDirectionNorm * projectileMaxSpeed;
            }

            var deepSim = projectileAccelerates || hasGravity;
            var count = deepSim ? 320 : 60;

            double dt = Math.Max(MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS, timeToIntercept / count); // This can be a const somewhere
            double dtSqr = dt * dt;
            Vector3D targetAccStep = targetAcc * dt;
            Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt;

            Vector3D aimOffset = Vector3D.Zero;

            //BD Todo:  Clamp this for projectiles OR targets that don't accelerate
            if (!skipAccel && (projectileAccelerates || targetVel.LengthSquared() >= 0.01))
            {
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
                    if (diffLenSq < projectileMaxSpeedSqr * dtSqr || Vector3D.Dot(diff, aimDirectionNorm) < 0)
                        break;
                }
            }

            Vector3D perpendicularAimOffset = !skipAccel ? aimOffset - Vector3D.Dot(aimOffset, aimDirectionNorm) * aimDirectionNorm : Vector3D.Zero;

            Vector3D gravityOffset = Vector3D.Zero;
            //gravity nonsense for differing elevations
            if (hasGravity && ai.InPlanetGravity)
            {
                var targetAngle = Math.Acos(Vector3D.Dot(weapon.GravityPoint, deltaPos) / (weapon.GravityLength * deltaLength));
                double elevationDifference;
                if (targetAngle >= 1.5708) //Target is above weapon
                {
                    targetAngle -= 1.5708; //angle-90
                    elevationDifference = -Math.Sin(targetAngle) * deltaLength;
                }
                else //Target is below weapon
                {
                    targetAngle = 1.5708 - targetAngle; //90-angle
                    elevationDifference = -Math.Sin(targetAngle) * deltaLength;
                }
                var horizontalDistance = Math.Sqrt(deltaLength * deltaLength - elevationDifference * elevationDifference);
                
                //Minimized for my sanity
                var g = -(weapon.GravityLength * gravityMultiplier);
                var v = projectileMaxSpeed;
                var h = elevationDifference;
                var d = horizontalDistance;

                var angleCheck = (v * v * v * v) - 2 * (v * v) * -h * g - (g * g) * (d * d);

                if (angleCheck <= 0)
                    return estimatedImpactPoint + perpendicularAimOffset + gravityOffset;

                //lord help me
                var angleSqrt = Math.Sqrt(angleCheck);
                var angle1 = -Math.Atan((v * v + angleSqrt) / (g * d));//Higher angle
                var angle2 = -Math.Atan((v * v - angleSqrt) / (g * d));//Lower angle                //Try angle 2 first (the lower one)
                
                var verticalDistance = Math.Tan(angle2) * horizontalDistance; //without below-the-horizon modifier
                gravityOffset = new Vector3D((verticalDistance + Math.Abs(elevationDifference)) * -weapon.GravityUnitDir);
                if (angle1 > 1.57)
                    return estimatedImpactPoint + perpendicularAimOffset + gravityOffset;

                var targetAimPoint = estimatedImpactPoint + perpendicularAimOffset + gravityOffset;
                var targetDirection = targetAimPoint - shooterPos;

                bool isTracking;
                if (!weapon.RotorTurretTracking && !MathFuncs.WeaponLookAt(weapon, ref targetDirection, deltaLength * deltaLength, false, true, out isTracking)) //Angle 2 obscured, switch to angle 1
                {
                    verticalDistance = Math.Tan(angle1) * horizontalDistance;
                    gravityOffset = new Vector3D((verticalDistance + Math.Abs(elevationDifference)) * -weapon.GravityUnitDir);
                }
                else if (weapon.RotorTurretTracking && !MathFuncs.RotorTurretLookAt(weapon.MasterComp.Platform.Control, ref targetDirection, deltaLength * deltaLength))
                {
                    verticalDistance = Math.Tan(angle1) * horizontalDistance;
                    gravityOffset = new Vector3D((verticalDistance + Math.Abs(elevationDifference)) * -weapon.GravityUnitDir);
                }
            }

            return estimatedImpactPoint + perpendicularAimOffset + gravityOffset;
        }

        public void ManualShootRayCallBack(IHitInfo hitInfo)
        {
            Casting = false;
            var masterWeapon = System.TrackTargets ? this : Comp.PrimaryWeapon;

            var grid = hitInfo.HitEntity as MyCubeGrid;
            if (grid != null)
            {
                if (grid.IsSameConstructAs(Comp.Cube.CubeGrid))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed, false);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed, false);
                }
            }
        }

        public bool HitFriendlyShield(Vector3D weaponPos, Vector3D targetPos, Vector3D dir)
        {
            var testRay = new RayD(weaponPos, dir);
            Comp.Ai.TestShields.Clear();
            var checkDistanceSqr = Vector3.DistanceSquared(targetPos, weaponPos);

            for (int i = 0; i < Comp.Ai.NearByFriendlyShields.Count; i++)
            {
                var shield = Comp.Ai.NearByFriendlyShields[i];
                var dist = testRay.Intersects(shield.PositionComp.WorldVolume);
                if (dist != null && dist.Value * dist.Value <= checkDistanceSqr)
                    Comp.Ai.TestShields.Add(shield);
            }

            if (Comp.Ai.TestShields.Count == 0)
                return false;

            var result = Comp.Ai.Session.SApi.IntersectEntToShieldFast(Comp.Ai.TestShields, testRay, true, false, Comp.Ai.AiOwner, checkDistanceSqr);

            return result.Item1 && result.Item2 > 0;
        }

        public bool MuzzleHitSelf()
        {
            for (int i = 0; i < Muzzles.Length; i++)
            {
                var m = Muzzles[i];
                var grid = Comp.Ai.GridEntity;
                var dummy = Dummies[i];
                var newInfo = dummy.Info;
                m.Direction = newInfo.Direction;
                m.Position = newInfo.Position;
                m.LastUpdateTick = Comp.Session.Tick;

                var start = m.Position;
                var end = m.Position + (m.Direction * grid.PositionComp.LocalVolume.Radius);

                Vector3D? hit;
                if (GridIntersection.BresenhamGridIntersection(grid, ref start, ref end, out hit, Comp.Cube, Comp.Ai))
                    return true;
            }
            return false;
        }
        private bool RayCheckTest()
        {
            if (PosChangedTick != Comp.Session.SimulationCount)
                UpdatePivotPos();

            var scopeInfo = GetScope.Info;
            var trackingCheckPosition = ScopeDistToCheckPos > 0 ? scopeInfo.Position - (scopeInfo.Direction * ScopeDistToCheckPos) : scopeInfo.Position;
            var overrides = Comp.Data.Repo.Values.Set.Overrides;

            if (System.Session.DebugLos && Target.TargetState == Target.TargetStates.IsEntity)
            {
                var trackPos = BarrelOrigin + (MyPivotFwd * MuzzleDistToBarrelCenter);
                var targetTestPos = Target.TargetEntity.PositionComp.WorldAABB.Center;
                var topEntity = Target.TargetEntity.GetTopMostParent();
                IHitInfo hitInfo;
                if (System.Session.Physics.CastRay(trackPos, targetTestPos, out hitInfo) && hitInfo.HitEntity == topEntity)
                {
                    var hitPos = hitInfo.Position;
                    double closestDist;
                    MyUtils.GetClosestPointOnLine(ref trackingCheckPosition, ref targetTestPos, ref hitPos, out closestDist);
                    var tDir = Vector3D.Normalize(targetTestPos - trackingCheckPosition);
                    var closestPos = trackingCheckPosition + (tDir * closestDist);

                    var missAmount = Vector3D.Distance(hitPos, closestPos);
                    System.Session.Rays++;
                    System.Session.RayMissAmounts += missAmount;

                }
            }

            var tick = Comp.Session.Tick;
            var masterWeapon = System.TrackTargets || Comp.PrimaryWeapon == null ? this : Comp.PrimaryWeapon;

            if (System.Values.HardPoint.Other.MuzzleCheck)
            {
                LastMuzzleCheck = tick;
                if (MuzzleHitSelf())
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckSelfHit, !Comp.FakeMode);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckSelfHit, !Comp.FakeMode);
                    return false;
                }
                if (tick - Comp.LastRayCastTick <= 29) return true;
            }

            if (Target.TargetEntity is IMyCharacter && !overrides.Biologicals || Target.TargetEntity is MyCubeBlock && !overrides.Grids)
            {
                masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                return false;
            }

            Comp.LastRayCastTick = tick;

            if (Target.TargetState == Target.TargetStates.IsFake)
            {
                Casting = true;
                Comp.Session.Physics.CastRayParallel(ref trackingCheckPosition, ref Target.TargetPos, CollisionLayers.DefaultCollisionLayer, ManualShootRayCallBack);
                return true;
            }

            if (Comp.FakeMode) return true;


            if (Target.TargetState == Target.TargetStates.IsProjectile)
            {
                if (!Comp.Ai.LiveProjectile.Contains(Target.Projectile))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckProjectile);
                    return false;
                }
            }
            if (Target.TargetState != Target.TargetStates.IsProjectile)
            {
                var character = Target.TargetEntity as IMyCharacter;
                if ((Target.TargetEntity == null || Target.TargetEntity.MarkedForClose) || character != null && (character.IsDead || character.Integrity <= 0 || Comp.Session.AdminMap.ContainsKey(character) || ((uint)character.Flags & 0x1000000) > 0))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckOther);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckOther);
                    return false;
                }

                var cube = Target.TargetEntity as MyCubeBlock;
                if (cube != null)
                {
                    var invalidCube = !cube.IsWorking || cube.MarkedForClose;
                    var rootAi = Comp.Ai.Construct.RootAi;
                    var focusFailed = overrides.FocusTargets && !rootAi.Construct.Focus.EntityIsFocused(rootAi, cube.CubeGrid);
                    var checkSubsystem = overrides.FocusSubSystem && overrides.SubSystem != WeaponDefinition.TargetingDef.BlockTypes.Any;
                    if (invalidCube || focusFailed || ((uint)cube.CubeGrid.Flags & 0x1000000) > 0 || checkSubsystem && !ValidSubSystemTarget(cube, overrides.SubSystem))
                    {
                        masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckDeadBlock);
                        if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckDeadBlock);
                        FastTargetResetTick = System.Session.Tick;
                        return false;
                    }

                }
                var topMostEnt = Target.TargetEntity.GetTopMostParent();
                if (Target.TopEntityId != topMostEnt.EntityId || !Comp.Ai.Targets.ContainsKey(topMostEnt))
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }
            }

            var targetPos = Target.Projectile?.Position ?? Target.TargetEntity?.PositionComp.WorldAABB.Center ?? Vector3D.Zero;
            var distToTargetSqr = Vector3D.DistanceSquared(targetPos, trackingCheckPosition);
            if (distToTargetSqr > MaxTargetDistanceSqr && distToTargetSqr < MinTargetDistanceSqr)
            {
                masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckDistExceeded);
                if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckDistExceeded);
                return false;
            }
            WaterData water = null;
            if (System.Session.WaterApiLoaded && !ActiveAmmoDef.AmmoDef.IgnoreWater && Comp.Ai.InPlanetGravity && Comp.Ai.MyPlanet != null && System.Session.WaterMap.TryGetValue(Comp.Ai.MyPlanet.EntityId, out water))
            {
                var waterSphere = new BoundingSphereD(Comp.Ai.MyPlanet.PositionComp.WorldAABB.Center, water.MinRadius);
                if (waterSphere.Contains(targetPos) != ContainmentType.Disjoint)
                {
                    masterWeapon.Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    if (masterWeapon != this) Target.Reset(Comp.Session.Tick, Target.States.RayCheckFailed);
                    return false;
                }
            }
            Casting = true;

            Comp.Session.Physics.CastRayParallel(ref trackingCheckPosition, ref targetPos, CollisionLayers.DefaultCollisionLayer, RayCallBack.NormalShootRayCallBack);
            return true;
        }

        internal bool ValidSubSystemTarget(MyCubeBlock cube, WeaponDefinition.TargetingDef.BlockTypes subsystem)
        {
            switch (subsystem)
            {
                case WeaponDefinition.TargetingDef.BlockTypes.Jumping:
                    return cube is MyJumpDrive || cube is IMyDecoy;
                case WeaponDefinition.TargetingDef.BlockTypes.Offense:
                    return cube is IMyGunBaseUser || cube is MyConveyorSorter && System.Session.PartPlatforms.ContainsKey(cube.BlockDefinition.Id) || cube is IMyWarhead || cube is IMyDecoy;
                case WeaponDefinition.TargetingDef.BlockTypes.Power:
                    return cube is IMyPowerProducer || cube is IMyDecoy;
                case WeaponDefinition.TargetingDef.BlockTypes.Production:
                    return cube is IMyProductionBlock || cube is IMyUpgradeModule && System.Session.VanillaUpgradeModuleHashes.Contains(cube.BlockDefinition.Id.SubtypeName) || cube is IMyDecoy;
                case WeaponDefinition.TargetingDef.BlockTypes.Steering:
                    var cockpit = cube as MyCockpit;
                    return cube is MyGyro || cockpit != null && cockpit.EnableShipControl || cube is IMyDecoy;
                case WeaponDefinition.TargetingDef.BlockTypes.Thrust:
                    return cube is MyThrust || cube is IMyDecoy;
                case WeaponDefinition.TargetingDef.BlockTypes.Utility:
                    return !(cube is IMyProductionBlock) && cube is IMyUpgradeModule || cube is IMyRadioAntenna || cube is IMyLaserAntenna || cube is MyRemoteControl || cube is IMyShipToolBase || cube is IMyMedicalRoom || cube is IMyCameraBlock || cube is IMyDecoy; 
                default:
                    return false;
            }
        }

        internal void InitTracking()
        {
            RotationSpeed = System.AzStep;
            ElevationSpeed = System.ElStep;
            var minAz = System.MinAzimuth;
            var maxAz = System.MaxAzimuth;
            var minEl = System.MinElevation;
            var maxEl = System.MaxElevation;
            var toleranceRads = System.WConst.AimingToleranceRads;

            MinElevationRadians = MinElToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(minEl));
            MaxElevationRadians = MaxElToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(maxEl));

            MinAzimuthRadians = MinAzToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(minAz));
            MaxAzimuthRadians = MaxAzToleranceRadians = MathHelperD.ToRadians(MathFuncs.NormalizeAngle(maxAz));

            if (System.TurretMovement == WeaponSystem.TurretType.AzimuthOnly || System.Values.HardPoint.AddToleranceToTracking)
            {
                MinElToleranceRadians -= toleranceRads;
                MaxElToleranceRadians += toleranceRads;
            }
            else if (System.TurretMovement == WeaponSystem.TurretType.ElevationOnly || System.Values.HardPoint.AddToleranceToTracking)
            {
                MinAzToleranceRadians -= toleranceRads;
                MaxAzToleranceRadians += toleranceRads;
            }

            if (MinElToleranceRadians > MaxElToleranceRadians)
                MinElToleranceRadians -= 6.283185f;

            if (MinAzToleranceRadians > MaxAzToleranceRadians)
                MinAzToleranceRadians -= 6.283185f;

            var dummyInfo = Dummies[MiddleMuzzleIndex];
            MuzzleDistToBarrelCenter = Vector3D.Distance(dummyInfo.Info.Position, dummyInfo.Entity.PositionComp.WorldAABB.Center);
        }
    }
}