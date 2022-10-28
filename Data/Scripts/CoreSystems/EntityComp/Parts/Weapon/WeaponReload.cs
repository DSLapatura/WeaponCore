using System;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using static CoreSystems.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace CoreSystems.Platform
{
    public partial class Weapon 
    {
        internal void ChangeActiveAmmoServer()
        {
            var proposed = ProposedAmmoId != -1;
            var ammoType = proposed ? System.AmmoTypes[ProposedAmmoId] : System.AmmoTypes[Reload.AmmoTypeId];
            ScheduleAmmoChange = false;

            if (ActiveAmmoDef == ammoType)
                return;

            if (proposed)
            {
                Reload.AmmoTypeId = ProposedAmmoId;
                ProposedAmmoId = -1;
                ProtoWeaponAmmo.CurrentAmmo = 0;
                Reload.CurrentMags = Comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? 0 : int.MaxValue;
            }

            ActiveAmmoDef = System.AmmoTypes[Reload.AmmoTypeId];
            if (string.IsNullOrEmpty(AmmoName)) 
                AmmoName = ActiveAmmoDef.AmmoName;
            PrepAmmoShuffle();

            if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                Reload.CurrentMags = Comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? Comp.CoreInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe() : int.MaxValue;

            AmmoName = ActiveAmmoDef.AmmoName;

            CheckInventorySystem = true;

            UpdateRof();
            SetWeaponDps();
            UpdateWeaponRange();
        }

        internal void ChangeActiveAmmoClient()
        {
            var ammoType = System.AmmoTypes[Reload.AmmoTypeId];

            if (ActiveAmmoDef == ammoType)
                return;

            ActiveAmmoDef = System.AmmoTypes[Reload.AmmoTypeId];
            PrepAmmoShuffle();

            UpdateRof();
            SetWeaponDps();
            UpdateWeaponRange();
        }

        internal void PrepAmmoShuffle()
        {
            if (AmmoShufflePattern.Length != ActiveAmmoDef.AmmoDef.Const.WeaponPatternCount) 
                Array.Resize(ref AmmoShufflePattern, ActiveAmmoDef.AmmoDef.Const.WeaponPatternCount);

            for (int i = 0; i < AmmoShufflePattern.Length; i++)
                AmmoShufflePattern[i] = i;
        }

        internal void QueueAmmoChange(int newAmmoId)
        {
            var serverAccept = System.Session.IsServer && (!Loading || DelayedCycleId < 0);
            var clientAccept = System.Session.IsClient && ClientMakeUpShots == 0 && !ServerQueuedAmmo && (!ClientReloading || ProtoWeaponAmmo.CurrentAmmo == 0);
            if (clientAccept || serverAccept)
            {
                DelayedCycleId = newAmmoId;
                AmmoName = System.AmmoTypes[newAmmoId].AmmoNameQueued;

                if (System.Session.IsClient)
                    ChangeAmmo(newAmmoId);
            }
        }

        internal void ChangeAmmo(int newAmmoId)
        {
            if (System.Session.IsServer)
            {
                DelayedCycleId = -1;
                ProposedAmmoId = newAmmoId;
                var instantChange = System.Session.IsCreative || !ActiveAmmoDef.AmmoDef.Const.Reloadable;
                var canReload = ProtoWeaponAmmo.CurrentAmmo == 0;

                var proposedAmmo = System.AmmoTypes[ProposedAmmoId];

                if (instantChange)
                    ChangeActiveAmmoServer();
                else 
                    ScheduleAmmoChange = true;

                if (proposedAmmo.AmmoDef.Const.Reloadable && canReload)
                    ComputeServerStorage(true);
            }
            else 
                System.Session.SendAmmoCycleRequest(this, newAmmoId);
        }

        internal bool HasAmmo()
        {
            if (Comp.Session.IsCreative || !ActiveAmmoDef.AmmoDef.Const.Reloadable || System.DesignatorWeapon) {
                NoMagsToLoad = false;
                return true;
            }

            Reload.CurrentMags = Comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? Comp.CoreInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe() : Reload.CurrentMags;
            var energyDrainable = ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && Comp.Ai.HasPower;
            var nothingToLoad = Reload.CurrentMags <= 0 && !energyDrainable;

            if (NoMagsToLoad) {
                if (nothingToLoad)
                    return false;

                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, false);
                Comp.Ai.Construct.RootAi.Construct.OutOfAmmoWeapons.Remove(this);
                NoMagsToLoad = false;
                LastMagSeenTick = System.Session.Tick;
            }
            else if (nothingToLoad)
            {
                EventTriggerStateChanged(EventTriggers.NoMagsToLoad, true);
                Comp.Ai.Construct.RootAi.Construct.OutOfAmmoWeapons.Add(this);

                if (!NoMagsToLoad) 
                    CheckInventorySystem = true;

                NoMagsToLoad = true;
            }

            return !NoMagsToLoad;
        }

        internal bool ClientReload(bool networkCaller = false)
        {
            var syncUp = Reload.StartId > ClientStartId;

            if (!syncUp) {
                var energyDrainable = ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && Comp.Ai.HasPower;
                if (Reload.CurrentMags <= 0 && !energyDrainable && ActiveAmmoDef.AmmoDef.Const.Reloadable && !System.DesignatorWeapon && !Loading) {
                    
                    if (!Comp.Session.IsCreative) {

                        if (!NoMagsToLoad)
                            EventTriggerStateChanged(EventTriggers.NoMagsToLoad, true);
                        NoMagsToLoad = true;
                    }
                }
                
                if (Loading && ClientMakeUpShots < 1 && LoadingWait && Reload.EndId > ClientEndId)
                    Reloaded(1);

                return false;
            }
            ClientStartId = Reload.StartId;
            ClientMakeUpShots += ProtoWeaponAmmo.CurrentAmmo;


            ProtoWeaponAmmo.CurrentAmmo = 0;

            if (!Comp.Session.IsCreative) {

                if (NoMagsToLoad) {
                    EventTriggerStateChanged(EventTriggers.NoMagsToLoad, false);
                    NoMagsToLoad = false;
                }
            }

            ClientReloading = true;

            StartReload();
            return true;
        }

        internal bool ComputeServerStorage(bool calledFromReload = false)
        {
            var s = Comp.Session;
            var isPhantom = Comp.TypeSpecific == CoreComponent.CompTypeSpecific.Phantom;
            if (System.DesignatorWeapon || !Comp.IsWorking || !ActiveAmmoDef.AmmoDef.Const.Reloadable || !Comp.HasInventory && !isPhantom) return false;

            if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && !isPhantom)
            {
                if (!s.IsCreative)
                {
                    Comp.CurrentInventoryVolume = (float)Comp.CoreInventory.CurrentVolume;
                    var freeVolume = System.MaxAmmoVolume - Comp.CurrentInventoryVolume;
                    var spotsFree = (int)(freeVolume / ActiveAmmoDef.AmmoDef.Const.MagVolume);
                    Reload.CurrentMags = Comp.CoreInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe();
                    CurrentAmmoVolume = Reload.CurrentMags * ActiveAmmoDef.AmmoDef.Const.MagVolume;

                    var magsRequested = (int)((System.FullAmmoVolume - CurrentAmmoVolume) / ActiveAmmoDef.AmmoDef.Const.MagVolume);
                    var magsGranted = magsRequested > spotsFree ? spotsFree : magsRequested;
                    var requestedVolume = ActiveAmmoDef.AmmoDef.Const.MagVolume * magsGranted;
                    var spaceAvailable = freeVolume > requestedVolume;
                    var lowThreshold = System.MaxAmmoVolume * 0.25f;

                    var pullAmmo = magsGranted > 0 && CurrentAmmoVolume < lowThreshold && spaceAvailable;
                    
                    var failSafeTimer = s.Tick - LastInventoryTick > 600;
                    
                    if (pullAmmo && (CheckInventorySystem || failSafeTimer && Comp.Ai.Construct.RootAi.Construct.OutOfAmmoWeapons.Contains(this)) && !s.PartToPullConsumable.Contains(this)) {

                        CheckInventorySystem = false;
                        LastInventoryTick = s.Tick;
                        s.PartToPullConsumable.Add(this);
                        s.GridsToUpdateInventories.Add(Comp.Ai);
                    }
                    else if (CheckInventorySystem && failSafeTimer && !s.PartToPullConsumable.Contains(this))
                        CheckInventorySystem = false;
                }
            }

            var invalidStates = ProtoWeaponAmmo.CurrentAmmo != 0 || Loading || calledFromReload || Reload.WaitForClient || (System.MaxReloads > 0 && Reload.LifetimeLoads >= System.MaxReloads);
            return !invalidStates && ServerReload();
        }

        internal bool ServerReload()
        {
            if (DelayedCycleId >= 0)
                ChangeAmmo(DelayedCycleId);

            if (ScheduleAmmoChange) 
                ChangeActiveAmmoServer();

            var hasAmmo = HasAmmo();

            if (!hasAmmo) 
                return false;

            ++Reload.StartId;
            ++ClientStartId;
            ++Reload.LifetimeLoads;

            if (!ActiveAmmoDef.AmmoDef.Const.EnergyAmmo) {

                var isPhantom = Comp.TypeSpecific == CoreComponent.CompTypeSpecific.Phantom;
                Reload.MagsLoaded = ActiveAmmoDef.AmmoDef.Const.MagsToLoad <= Reload.CurrentMags || Comp.Session.IsCreative ? ActiveAmmoDef.AmmoDef.Const.MagsToLoad : Reload.CurrentMags;
                
                if (!Comp.Session.IsCreative)
                {
                    if (!isPhantom && Comp.CoreInventory.ItemsCanBeRemoved(Reload.MagsLoaded, ActiveAmmoDef.AmmoDef.Const.AmmoItem))
                    {
                        if (System.HasAmmoSelection) {
                            var magItem = Comp.CoreInventory.FindItem(ActiveAmmoDef.AmmoDefinitionId) ?? ActiveAmmoDef.AmmoDef.Const.AmmoItem;
                            Comp.CoreInventory.RemoveItems(magItem.ItemId, Reload.MagsLoaded);
                        }
                        else
                            Comp.CoreInventory.RemoveItems(ActiveAmmoDef.AmmoDef.Const.AmmoItem.ItemId, Reload.MagsLoaded);
                    }
                    else if (!isPhantom && Comp.CoreInventory.ItemCount > 0 && Comp.CoreInventory.ContainItems(Reload.MagsLoaded, ActiveAmmoDef.AmmoDef.Const.AmmoItem.Content))
                    {
                        Comp.CoreInventory.Remove(ActiveAmmoDef.AmmoDef.Const.AmmoItem, Reload.MagsLoaded);
                    }
                }

                Reload.CurrentMags = !isPhantom ? Comp.CoreInventory.GetItemAmount(ActiveAmmoDef.AmmoDefinitionId).ToIntSafe() : Reload.CurrentMags - Reload.MagsLoaded;
                if (Reload.CurrentMags == 0)
                    CheckInventorySystem = true;
            }

            StartReload();
            return true;
        }

        internal void StartReload()
        {
            Loading = true;
            if (Comp.Rifle != null)
                Comp.HandheldReload();

            if (!ActiveAmmoDef.AmmoDef.Const.BurstMode && !ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay && System.Values.HardPoint.Loading.GiveUpAfter)
                GiveUpTarget();

            EventTriggerStateChanged(EventTriggers.Reloading, true);



            if (ActiveAmmoDef.AmmoDef.Const.MustCharge)
                ChargeReload();
            
            if (!ActiveAmmoDef.AmmoDef.Const.MustCharge || ActiveAmmoDef.AmmoDef.Const.IsHybrid) {

                var timeSinceShot = LastShootTick > 0 ? System.Session.Tick - LastShootTick : 0;
                var delayTime = timeSinceShot <= System.Values.HardPoint.Loading.DelayAfterBurst ? System.Values.HardPoint.Loading.DelayAfterBurst - timeSinceShot : 0;
                var delay = delayTime > 0 && ShotsFired == 0;
                if (System.WConst.ReloadTime > 0 || delay)
                {
                    ReloadEndTick = (uint)(Comp.Session.Tick + (!delay || System.WConst.ReloadTime > delayTime ? System.WConst.ReloadTime : delayTime));
                }
                else Reloaded(3);
            }

            if (System.Session.MpActive && System.Session.IsServer)
            {
                if (ActiveAmmoDef.AmmoDef.Const.SlowFireFixedWeapon && Comp.Data.Repo.Values.State.PlayerId > 0)
                    Reload.WaitForClient = !System.Session.IsHost;

                System.Session.SendWeaponReload(this);
            }

            if (ReloadEmitter == null || ReloadEmitter.IsPlaying) return;
            ReloadEmitter.PlaySound(System.ReloadSoundPairs, true, false, false, false, false, false);
        }

        internal void Reloaded(object o = null)
        {
            var input = o as int? ?? 0;
            var callBack = input == 1;
            var earlyExit = input == 2;
            using (Comp.CoreEntity.Pin()) {

                if (PartState == null || Comp.Data.Repo == null || Comp.Ai == null || Comp.CoreEntity.MarkedForClose) {
                    CancelReload();
                    return;
                }

                if (input == 4) {
                    ProtoWeaponAmmo.CurrentCharge = MaxCharge;
                    EstimatedCharge = MaxCharge;
                    return;
                }

                if (ActiveAmmoDef.AmmoDef.Const.MustCharge && !callBack && !earlyExit) {

                    ProtoWeaponAmmo.CurrentCharge = MaxCharge;
                    EstimatedCharge = MaxCharge;

                    if (ActiveAmmoDef.AmmoDef.Const.IsHybrid && LoadingWait)
                        return;
                }
                else if (ActiveAmmoDef.AmmoDef.Const.IsHybrid && Charging && ReloadEndTick != uint.MaxValue)
                {
                    ReloadEndTick = uint.MaxValue - 1;
                    return;
                }

                ProtoWeaponAmmo.CurrentAmmo = Reload.MagsLoaded * ActiveAmmoDef.AmmoDef.Const.MagazineSize;
                if (System.Session.IsServer) {

                    ++Reload.EndId;
                    ClientEndId = Reload.EndId;

                    if (Comp.TypeSpecific == CoreComponent.CompTypeSpecific.Phantom && ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                        --Reload.CurrentMags;

                    if (System.Session.MpActive) {

                        System.Session.SendWeaponReload(this);
                        if (Reload.EndId == 1)
                            System.Session.SendWeaponAmmoData(this);
                    }
                }
                else {
                    ClientReloading = false;
                    
                    ClientMakeUpShots = 0;
                    ClientEndId = Reload.EndId;
                    ServerQueuedAmmo = false;

                    if (DelayedCycleId == ActiveAmmoDef.AmmoDef.Const.AmmoIdxPos)
                    {
                        AmmoName = ActiveAmmoDef.AmmoName;
                        DelayedCycleId = -1;
                    }

                    if (ActiveAmmoDef.AmmoDef.Const.SlowFireFixedWeapon && System.Session.PlayerId == Comp.Data.Repo.Values.State.PlayerId)
                        System.Session.SendClientReady(this);
                }
                TargetData.WeaponRandom.TurretRandom = new XorShiftRandomStruct((ulong)(TargetData.WeaponRandom.CurrentSeed + (Reload.EndId + 1000000)));
                EventTriggerStateChanged(EventTriggers.Reloading, false);
                LastLoadedTick = Comp.Session.Tick;

                if (!ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay)
                    ShotsFired = 0;

                Loading = false;
                ReloadEndTick = uint.MaxValue;
                ProjectileCounter = 0;
                NextMuzzle = 0;

                if (Comp.ShootManager.LastCycle != uint.MaxValue)
                    Comp.ShootManager.EndShootMode(ShootManager.EndReason.Reload);
            }
        }

        public void ChargeReload()
        {
            ProtoWeaponAmmo.CurrentCharge = 0;
            EstimatedCharge = 0;

            Comp.Ai.Charger.Add(this);
        }

        public void CancelReload()
        {
            if (ReloadEndTick == uint.MaxValue)
                return;
            NextMuzzle = 0;
            EventTriggerStateChanged(EventTriggers.Reloading, false);
            LastLoadedTick = Comp.Session.Tick;
            Loading = false;
            ReloadEndTick = uint.MaxValue;
            ProjectileCounter = 0;
        }
    }
}
