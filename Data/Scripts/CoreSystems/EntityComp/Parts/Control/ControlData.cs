using System;
using CoreSystems.Support;
using Sandbox.ModAPI;

namespace CoreSystems.Platform
{
    public partial class ControlSys 
    {
        internal class ControlCompData : CompData
        {
            internal readonly ControlComponent Comp;
            internal ProtoControlRepo Repo;

            internal ControlCompData(ControlComponent comp)
            {
                Init(comp);
                Comp = comp;
            }

            internal void Load()
            {
                if (Comp.CoreEntity.Storage == null) return;

                ProtoControlRepo load = null;
                string rawData;
                bool validData = false;
                if (Comp.CoreEntity.Storage.TryGetValue(Comp.Session.CompDataGuid, out rawData))
                {
                    try
                    {
                        var base64 = Convert.FromBase64String(rawData);
                        load = MyAPIGateway.Utilities.SerializeFromBinary<ProtoControlRepo>(base64);
                        validData = load?.Values.Other != null;
                    }
                    catch (Exception e)
                    {
                        Log.Line("Invalid PartState Loaded, Re-init");
                    }
                }

                if (validData && load.Version == Session.VersionControl)
                {
                    Repo = load;
                    var p = Comp.Platform.Control;
                    p.PartState = Repo.Values.State.Control;

                }
                else
                {
                    Repo = new ProtoControlRepo
                    {
                        Values = new ProtoControlComp
                        {
                            State = new ProtoControlState { Control = new ProtoControlPartState() },
                            Set = new ProtoWeaponSettings(),
                            Other = new ProtoControlOtherSettings(),
                        },
                    };

                    var state = Repo.Values.State.Control = new ProtoControlPartState();
                    var p = Comp.Platform.Control;

                    if (p != null)
                    {
                        p.PartState = state;
                    }

                    Repo.Values.Set.Range = -1;
                }
                ProtoRepoBase = Repo;
            }

            internal void Change(DataState state)
            {
                switch (state)
                {
                    case DataState.Load:
                        Load();
                        break;
                    case DataState.Reset:
                        Repo.ResetToFreshLoadState();
                        break;
                }
            }
        }
    }
}
