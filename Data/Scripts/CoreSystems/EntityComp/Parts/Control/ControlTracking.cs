using CoreSystems.Support;
using VRage.Game.Entity;
using VRageMath;
namespace CoreSystems.Platform
{
    public partial class ControlSys : Part
    {
        internal static bool TrajectoryEstimation(ControlSys control, out Vector3D targetDirection)
        {
            var topAi = control.TopAi;
            var weapon = topAi.RootComp.PrimaryWeapon;
            var cValues = control.Comp.Data.Repo.Values;
            Vector3D targetCenter;
            Vector3D targetVel = Vector3D.Zero;
            Vector3D targetAcc = Vector3D.Zero;
            var eTarget = weapon.Target.TargetObject as MyEntity;
            Ai.FakeTarget.FakeWorldTargetInfo fakeTargetInfo = null;
            if (cValues.Set.Overrides.Control != ProtoWeaponOverrides.ControlModes.Auto && control.ValidFakeTargetInfo(cValues.State.PlayerId, out fakeTargetInfo))
            {
                targetCenter = fakeTargetInfo.WorldPosition;
                targetVel = fakeTargetInfo.LinearVelocity;
                targetAcc = fakeTargetInfo.Acceleration;
            }
            else if (eTarget != null)
            {
                targetCenter = eTarget.PositionComp.WorldAABB.Center;
                var topEnt = eTarget.GetTopMostParent();
                if (topEnt != null)
                {
                    targetVel = topEnt.Physics?.LinearVelocity ?? Vector3D.Zero;
                    targetAcc = topEnt.Physics?.LinearAcceleration ?? Vector3D.Zero;
                }
            }
            else 
            {
                targetDirection = Vector3D.Zero;
                topAi.RotorTargetPosition = Vector3D.MaxValue;
                return false;
            }

            var shooterPos = weapon.GetScope.Info.Position;
            var maxRangeSqr = fakeTargetInfo != null && topAi.Construct.RootAi != null ? topAi.Construct.RootAi.MaxTargetingRangeSqr : cValues.Set.Range * cValues.Set.Range;

            bool valid;
            topAi.RotorTargetPosition =  Weapon.TrajectoryEstimation(weapon, targetCenter, targetVel, targetAcc, shooterPos, out valid, true, true, true);
            targetDirection = Vector3D.Normalize(topAi.RotorTargetPosition - shooterPos);
            return valid && Vector3D.DistanceSquared(topAi.RotorTargetPosition, shooterPos) < maxRangeSqr;
        }

    }
}
