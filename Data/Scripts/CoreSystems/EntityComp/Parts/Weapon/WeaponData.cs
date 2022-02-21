using System;
using CoreSystems.Support;
using Sandbox.ModAPI;

namespace CoreSystems.Platform
{
    public partial class Weapon 
    {
        internal class WeaponCompData : CompData
        {
            internal readonly WeaponComponent Comp;
            internal ProtoWeaponRepo Repo;
            internal WeaponCompData(WeaponComponent comp)
            {
                Init(comp);
                Comp = comp;
            }

            internal void Load()
            {
                if (Comp.CoreEntity.Storage == null) return;

                ProtoWeaponRepo load = null;
                string rawData;
                bool validData = false;
                if (Comp.CoreEntity.Storage.TryGetValue(Comp.Session.CompDataGuid, out rawData))
                {
                    try
                    {
                        var base64 = Convert.FromBase64String(rawData);
                        load = MyAPIGateway.Utilities.SerializeFromBinary<ProtoWeaponRepo>(base64);
                        validData = load != null;
                    }
                    catch (Exception e)
                    {
                        //Log.Line("Invalid PartState Loaded, Re-init");
                    }
                }
                var collection = Comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? Comp.Platform.Weapons : Comp.Platform.Phantoms;
                if (validData && load.Version == Session.VersionControl)
                {
                    Repo = load;
                    if (Comp.Session.IsServer)
                        Repo.Values.Targets = new ProtoWeaponTransferTarget[collection.Count];

                    for (int i = 0; i < collection.Count; i++)
                    {
                        var w = collection[i];

                        w.PartState = Repo.Values.State.Weapons[i];
                        w.Reload = Repo.Values.Reloads[i];

                        w.ProtoWeaponAmmo = Repo.Ammos[i];

                        if (Comp.Session.IsServer)
                        {
                            Repo.Values.Targets[i] = new ProtoWeaponTransferTarget();
                            w.TargetData = Repo.Values.Targets[i];
                            w.TargetData.WeaponRandom = new WeaponRandomGenerator();
                            w.TargetData.WeaponRandom.Init(w);
                        }
                        else
                        {
                            w.ProtoWeaponAmmo = Repo.Ammos[i];
                            w.ClientStartId = w.Reload.StartId;
                            w.ClientEndId = w.Reload.EndId;
                            w.TargetData = Repo.Values.Targets[i];
                            w.TargetData.WeaponRandom.Init(w);

                            if (w.Reload.AmmoTypeId >= w.System.AmmoTypes.Length)
                                w.Reload.AmmoTypeId = 0;
                        }

                    }
                }
                else
                {

                    Repo = new ProtoWeaponRepo
                    {
                        Values = new ProtoWeaponComp
                        {
                            State = new ProtoWeaponState { Weapons = new ProtoWeaponPartState[collection.Count], Tasks = new ProtoWeaponCompTasks()},
                            Set = new ProtoWeaponSettings(),
                            Targets = new ProtoWeaponTransferTarget[collection.Count],
                            Reloads = new ProtoWeaponReload[collection.Count],
                        },
                        Ammos = new ProtoWeaponAmmo[collection.Count],

                    };

                    if (Comp.Session.IsCreative && Comp.Session.IsServer) {
                        Repo.Values.Set.Overrides.Neutrals = true;
                        Repo.Values.Set.Overrides.Biologicals = false;
                    }

                    for (int i = 0; i < collection.Count; i++)
                    {
                        var state = Repo.Values.State.Weapons[i] = new ProtoWeaponPartState();
                        var reload = Repo.Values.Reloads[i] = new ProtoWeaponReload();
                        var ammo = Repo.Ammos[i] = new ProtoWeaponAmmo();
                        var w = collection[i];

                        if (w != null)
                        {
                            w.PartState = state;
                            w.Reload = reload;
                            w.ProtoWeaponAmmo = ammo;

                            Repo.Values.Targets[i] = new ProtoWeaponTransferTarget();
                            w.TargetData = Repo.Values.Targets[i];
                            w.TargetData.WeaponRandom = new WeaponRandomGenerator();
                            w.TargetData.WeaponRandom.Init(w);

                            if (w.System.Values.HardPoint.HardWare.CriticalReaction.PreArmed)
                                Repo.Values.Set.Overrides.Armed = true;

                            if (w.System.Values.HardPoint.HardWare.CriticalReaction.DefaultArmedTimer > Repo.Values.Set.Overrides.ArmedTimer)
                                Repo.Values.Set.Overrides.ArmedTimer = w.System.Values.HardPoint.HardWare.CriticalReaction.DefaultArmedTimer;

                            //if (!Comp.Session.Settings.Enforcement.DisableLeads && (!Comp.HasTurret && !Comp.OverrideLeads || Comp.HasTurret && Comp.OverrideLeads))
                            //    Repo.Values.Set.Overrides.LeadGroup = 1;

                        }
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
                        Repo.ResetToFreshLoadState(Comp);
                        break;
                }
            }
        }
    }
}
