using CoreSystems.Support;
using Sandbox.ModAPI;
using VRage.Utils;
namespace CoreSystems.Platform
{
    public partial class ControlSys : Part
    {
        internal readonly ControlComponent Comp;
        internal readonly ControlSystem System;
        internal readonly MyStringHash PartHash;

        internal IMyMotorStator BaseMap;
        internal IMyMotorStator OtherMap;
        internal Ai TopAi;
        internal ProtoControlPartState PartState;
        internal bool IsAimed;

        internal ControlSys(ControlSystem system, ControlComponent comp, int partId)
        {
            System = system;
            Comp = comp;
            Init(comp, system, partId);
            PartHash = Comp.Structure.PartHashes[partId];
        }


        internal void CleanControl()
        {
            if (TopAi != null)
            {
                if (TopAi?.RootComp?.PrimaryWeapon != null)
                    TopAi.RootComp.PrimaryWeapon.RotorTurretTracking = false;

                if (TopAi?.RootComp?.Ai?.ControlComp != null)
                    TopAi.RootComp.Ai.ControlComp = null;

                if (TopAi?.RootComp != null)
                    TopAi.RootComp = null;

                TopAi = null;
            }
        }
    }
}
