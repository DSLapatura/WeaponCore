using System.Collections.Concurrent;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using static CoreSystems.Support.SupportDefinition.SupportEffect.Protections;

namespace CoreSystems.Platform
{
    public partial class SupportSys
    {
        private ConcurrentDictionary<IMySlimBlock, SupportSys> GetSupportCollection()
        {
            switch (System.Values.Effect.Protection)
            {
                case EnergeticProt:
                case KineticProt:
                case GenericProt:
                    return System.Session.ProtSupports;
                case Regenerate:
                    return System.Session.RegenSupports;
                case Structural:
                    return System.Session.StructalSupports;
            }
            return null;
        }
    }
}
