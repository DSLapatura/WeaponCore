using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Support;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
namespace CoreSystems.Platform
{
    public partial class ControlSys : Part
    {
        internal readonly ControlInfo Info = new ControlInfo();
        internal readonly ControlComponent Comp;
        internal readonly ControlSystem System;
        internal readonly MyStringHash PartHash;

        internal IMyMotorStator BaseMap;
        internal IMyMotorStator OtherMap;
        internal Weapon TrackingWeapon;
        internal ProtoControlPartState PartState;

        internal ControlSys(ControlSystem system, ControlComponent comp, int partId)
        {
            System = system;
            Comp = comp;
            Init(comp, system, partId);
            PartHash = Comp.Structure.PartHashes[partId];
        }

        public class RotorMap
        {
            internal Ai Ai;
            internal Dummy Scope;
        }
    }
}
