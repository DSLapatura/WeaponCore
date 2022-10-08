using System;
using CoreSystems.Support;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components;
using VRage.Utils;
using VRageMath;
namespace CoreSystems.Platform
{
    public partial class ControlSys : Part
    {
        internal static bool TrajectoryEstimation(Ai topAi, ControlSys control, out Vector3D targetDirection)
        {
            var weapon = control.TrackingWeapon;
            var cValues = control.Comp.Data.Repo.Values;

            Ai.FakeTarget.FakeWorldTargetInfo fakeTargetInfo = null;
            if (cValues.Set.Overrides.Control != ProtoWeaponOverrides.ControlModes.Auto) 
                control.ValidFakeTargetInfo(cValues.State.PlayerId, out fakeTargetInfo);

            var targetCenter = fakeTargetInfo?.WorldPosition ?? weapon.Target.TargetEntity.PositionComp.WorldAABB.Center;
            var targetVel = (Vector3D)(fakeTargetInfo?.LinearVelocity ?? weapon.Target.TargetEntity.GetTopMostParent().Physics.LinearVelocity);
            var targetAcc = (Vector3D)(fakeTargetInfo?.Acceleration ?? weapon.Target.TargetEntity.GetTopMostParent().Physics.LinearAcceleration);
            var shooterPos = control.OtherMap.Top.PositionComp.WorldAABB.Center;

            var maxRangeSqr = fakeTargetInfo != null ? topAi.Construct.RootAi.MaxTargetingRangeSqr : (cValues.Set.Range * cValues.Set.Range);

            if (fakeTargetInfo == null && weapon.Target.TargetEntity?.GetTopMostParent()?.Physics?.LinearVelocity == null || Vector3D.DistanceSquared(targetCenter, shooterPos) >= maxRangeSqr )
            {
                targetDirection = Vector3D.Zero;
                topAi.RotorTargetPosition = Vector3D.MaxValue;
                return false;
            }

            bool valid;
            topAi.RotorTargetPosition =  Weapon.TrajectoryEstimation(weapon, targetCenter, targetVel, targetAcc, shooterPos, out valid, true, true, true);
            targetDirection = Vector3D.Normalize(topAi.RotorTargetPosition - shooterPos);
            return valid && Vector3D.DistanceSquared(topAi.RotorTargetPosition, shooterPos) < maxRangeSqr;
        }

    }
}
