using CoreSystems.Support;
using VRageMath;
namespace CoreSystems.Platform
{
    public partial class ControlSys : Part
    {
        internal static bool TrajectoryEstimation(Ai topAi, ControlSys control, out Vector3D targetDirection)
        {
            var weapon = control.WeaponComp.PrimaryWeapon;
            var cValues = control.Comp.Data.Repo.Values;

            Vector3D targetCenter;
            Vector3D targetVel;
            Vector3D targetAcc;

            Ai.FakeTarget.FakeWorldTargetInfo fakeTargetInfo = null;
            if (cValues.Set.Overrides.Control != ProtoWeaponOverrides.ControlModes.Auto && control.ValidFakeTargetInfo(cValues.State.PlayerId, out fakeTargetInfo))
            {
                targetCenter = fakeTargetInfo.WorldPosition;
                targetVel = fakeTargetInfo.LinearVelocity;
                targetAcc = fakeTargetInfo.Acceleration;
            }
            else if (weapon.Target.TargetEntity != null)
            {
                targetCenter = weapon.Target.TargetEntity.PositionComp.WorldAABB.Center;
                var topEnt = weapon.Target.TargetEntity.GetTopMostParent();
                targetVel = topEnt.Physics.LinearVelocity;
                targetAcc = topEnt.Physics.LinearAcceleration;
            }
            else 
            {
                targetDirection = Vector3D.Zero;
                topAi.RotorTargetPosition = Vector3D.MaxValue;
                return false;
            }

            var shooterPos = control.OtherMap.Top.PositionComp.WorldAABB.Center;
            var maxRangeSqr = fakeTargetInfo != null ? topAi.Construct.RootAi.MaxTargetingRangeSqr : (cValues.Set.Range * cValues.Set.Range);

            bool valid;
            topAi.RotorTargetPosition =  Weapon.TrajectoryEstimation(weapon, targetCenter, targetVel, targetAcc, shooterPos, out valid, true, true, true);
            targetDirection = Vector3D.Normalize(topAi.RotorTargetPosition - shooterPos);
            return valid && Vector3D.DistanceSquared(topAi.RotorTargetPosition, shooterPos) < maxRangeSqr;
        }

    }
}
