using System.Collections.Generic;
using VRageMath;
using WeaponCore.Data.Scripts.CoreSystems.Comms;

namespace WeaponCore.Data.Scripts.CoreSystems.Support
{
    internal class SpaceTrees
    {
        internal MyDynamicAABBTreeD DividedSpace = new MyDynamicAABBTreeD(Vector3D.One * 10.0, 10.0);
        internal void RegisterSignal(RadioSource source, ref BoundingSphereD volume)
        {
            if (source.PruningProxyId != -1)
                return;
            BoundingBoxD result;
            BoundingBoxD.CreateFromSphere(ref volume, out result);
            source.PruningProxyId = DividedSpace.AddProxy(ref result, source, 0U);
        }

        internal void UnregisterSignal(RadioSource source)
        {
            if (source.PruningProxyId == -1)
                return;
            DividedSpace.RemoveProxy(source.PruningProxyId);
            source.PruningProxyId = -1;
        }

        internal void OnSignalMoved(RadioSource source, ref Vector3 velocity, ref BoundingSphereD volume)
        {
            if (source.PruningProxyId == -1)
                return;
            BoundingBoxD result;
            BoundingBoxD.CreateFromSphere(ref volume, out result);
            DividedSpace.MoveProxy(source.PruningProxyId, ref result, velocity);
        }

        internal void GetAllSignalsInSphere(ref BoundingSphereD sphere, List<RadioSource> result, bool clearList = true)
        {
            DividedSpace.OverlapAllBoundingSphere(ref sphere, result, clearList);
        }
    }
}
