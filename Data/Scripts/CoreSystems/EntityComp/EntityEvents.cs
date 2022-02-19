using System;
using System.Collections.Concurrent;
using System.Text;
using CoreSystems.Platform;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using static CoreSystems.Platform.CorePlatform;
using static CoreSystems.Session;

namespace CoreSystems.Support
{
    public partial class CoreComponent
    {
        internal void RegisterEvents(bool register = true)
        {
            if (register)
            {
                if (Registered)
                    Log.Line("BaseComp RegisterEvents error");
                //TODO change this
                Registered = true;
                if (IsBlock)
                {
                    if (Type == CompType.Weapon)
                        TerminalBlock.AppendingCustomInfo += AppendingCustomInfoWeapon;
                    else if (TypeSpecific == CompTypeSpecific.Support)
                        TerminalBlock.AppendingCustomInfo += AppendingCustomInfoSupport;
                    else if (TypeSpecific == CompTypeSpecific.Upgrade)
                        TerminalBlock.AppendingCustomInfo += AppendingCustomInfoUpgrade;

                    Cube.IsWorkingChanged += IsWorkingChanged;
                    IsWorkingChanged(Cube);
                }

                if (CoreInventory == null)
                {
                    if (TypeSpecific != CompTypeSpecific.Phantom && TypeSpecific != CompTypeSpecific.Control && !IsBomb)
                        Log.Line("BlockInventory is null");
                }
                else
                {
                    CoreInventory.InventoryContentChanged += OnContentsChanged;
                    Session.CoreInventoryItems[CoreInventory] = new ConcurrentDictionary<uint, BetterInventoryItem>();
                    Session.ConsumableItemList[CoreInventory] = Session.BetterItemsListPool.Get();

                    var items = CoreInventory.GetItems();
                    for (int i = 0; i < items.Count; i++)
                    {
                        var bItem = Session.BetterInventoryItems.Get();
                        var item = items[i];
                        bItem.Amount = (int)item.Amount;
                        bItem.Item = item;
                        bItem.Content = item.Content;

                        Session.CoreInventoryItems[CoreInventory][items[i].ItemId] = bItem;
                    }
                }
            }
            else
            {
                if (!Registered)
                    Log.Line("BaseComp UnRegisterEvents error");

                if (Registered)
                {
                    //TODO change this
                    Registered = false;

                    if (IsBlock) {

                        if (Type == CompType.Weapon)
                            TerminalBlock.AppendingCustomInfo -= AppendingCustomInfoWeapon;
                        else if (TypeSpecific == CompTypeSpecific.Support)
                            TerminalBlock.AppendingCustomInfo -= AppendingCustomInfoSupport;
                        else if (TypeSpecific == CompTypeSpecific.Upgrade)
                            TerminalBlock.AppendingCustomInfo -= AppendingCustomInfoUpgrade;

                        Cube.IsWorkingChanged -= IsWorkingChanged;
                    }

                    if (CoreInventory == null)
                    {
                        if (TypeSpecific != CompTypeSpecific.Control)
                            Log.Line("BlockInventory is null");
                    }
                    else
                    {
                        CoreInventory.InventoryContentChanged -= OnContentsChanged;
                        ConcurrentDictionary<uint, BetterInventoryItem> removedItems;
                        MyConcurrentList<BetterInventoryItem> removedList;

                        if (Session.CoreInventoryItems.TryRemove(CoreInventory, out removedItems))
                        {
                            foreach (var inventoryItems in removedItems)
                                Session.BetterInventoryItems.Return(inventoryItems.Value);

                            removedItems.Clear();
                        }

                        if (Session.ConsumableItemList.TryRemove(CoreInventory, out removedList))
                            Session.BetterItemsListPool.Return(removedList);
                    }
                }
            }
        }

        private void OnContentsChanged(MyInventoryBase inv, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            BetterInventoryItem cachedItem;
            if (!Session.CoreInventoryItems[CoreInventory].TryGetValue(item.ItemId, out cachedItem))
            {
                cachedItem = Session.BetterInventoryItems.Get();
                cachedItem.Amount = (int)amount;
                cachedItem.Content = item.Content;
                cachedItem.Item = item;
                Session.CoreInventoryItems[CoreInventory].TryAdd(item.ItemId, cachedItem);
            }
            else if (cachedItem.Amount + amount > 0)
            {
                cachedItem.Amount += (int)amount;
            }
            else if (cachedItem.Amount + amount <= 0)
            {
                BetterInventoryItem removedItem;
                if (Session.CoreInventoryItems[CoreInventory].TryRemove(item.ItemId, out removedItem))
                    Session.BetterInventoryItems.Return(removedItem);
            }
            var collection = TypeSpecific != CompTypeSpecific.Phantom ? Platform.Weapons : Platform.Phantoms;
            if (Session.IsServer && amount <= 0) {
                for (int i = 0; i < collection.Count; i++)
                    collection[i].CheckInventorySystem = true;
            }
        }

        private static void OnMarkForClose(MyEntity myEntity)
        {
            
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            try {
                var wasFunctional = IsFunctional;
                IsFunctional = myCubeBlock.IsFunctional;

                if (Platform.State == PlatformState.Incomplete) {
                    Log.Line("Init on Incomplete");
                    Init();
                }
                else {

                    if (!wasFunctional && IsFunctional && IsWorkingChangedTick > 0)
                        Status = Start.ReInit;
                    IsWorking = myCubeBlock.IsWorking;
                    if (Cube.ResourceSink.CurrentInputByType(GId) < 0) Log.Line($"IsWorking:{IsWorking}(was:{wasFunctional}) - Func:{IsFunctional} - GridAvailPow:{Ai.GridAvailablePower} - SinkPow:{SinkPower} - SinkReq:{Cube.ResourceSink.RequiredInputByType(GId)} - SinkCur:{Cube.ResourceSink.CurrentInputByType(GId)}");

                    if (!IsWorking && Registered) {
                        var collection = TypeSpecific != CompTypeSpecific.Phantom ? Platform.Weapons : Platform.Phantoms;
                        foreach (var w in collection)
                            w.StopShooting();
                    }
                    IsWorkingChangedTick = Session.Tick;
                }

                if (wasFunctional && !IsFunctional && Platform.State == PlatformState.Ready) {

                    if (Type == CompType.Weapon)
                        ((Weapon.WeaponComponent)this).NotFunctional();

                }
                
                if (Session.MpActive && Session.IsServer) {

                    if (Type == CompType.Weapon)
                        ((Weapon.WeaponComponent)this).PowerLoss();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in IsWorkingChanged: {ex}", null, true); }
        }

        internal string GetSystemStatus()
        {
            if (!Cube.IsFunctional) return Localization.GetText("SystemStatusFault");
            if (!Cube.IsWorking) return Localization.GetText("SystemStatusOffline");
            return Ai.AiOwner != 0 ? Localization.GetText("SystemStatusOnline") : Localization.GetText("SystemStatusRogueAi");
        }

        private void AppendingCustomInfoWeapon(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var comp = ((Weapon.WeaponComponent)this);
                var status = GetSystemStatus();

                stringBuilder.Append(status);

                if (HasServerOverrides)
                    stringBuilder.Append("\nWeapon modified by server!\n")
                        .Append("Report issues to server admins.\n");

                stringBuilder.Append($"\n{Localization.GetText("WeaponInfoConstructDPS")}: " + Ai.EffectiveDps.ToString("e2"))
                    .Append($"\n                        [ " + Ai.PerfectDps.ToString("e2") +" ]")
                    .Append($"\n{Localization.GetText("WeaponInfoShotsPerSec")}: " + comp.RealShotsPerSec.ToString("0.00") + " (" + comp.ShotsPerSec.ToString("0.00") + ")")
                    .Append("\n")
                    .Append($"\n{Localization.GetText("WeaponTotalEffect")}: " + comp.TotalEffect.ToString("e2"))
                    .Append($"\n              [ " + Ai.Construct.RootAi?.Construct.TotalEffect.ToString("e2") + " ]")
                    .Append($"\n{Localization.GetText("WeaponTotalEffectAvgDps")}: " + comp.AverageEffect.ToString("N0") + " - (" + comp.AddEffect.ToString("N0") + ")")
                    .Append($"\n             [ " + Ai.Construct.RootAi?.Construct.AverageEffect.ToString("N0") + " - (" + Ai.Construct.RootAi?.Construct.AddEffect.ToString("N0") + ") ]")

                    .Append("\n")
                    .Append($"\n{Localization.GetText("WeaponInfoRealDps")}: " + comp.EffectiveDps.ToString("0.0"))
                    .Append($"\n{Localization.GetText("WeaponInfoPerfectDps")}: " + comp.PerfectDps.ToString("0.0"))
                    .Append($"\n{Localization.GetText("WeaponInfoPeakDps")}: " + comp.PeakDps.ToString("0.0"))
                    .Append($"\n{Localization.GetText("WeaponInfoBaseDps")}: " + comp.BaseDps.ToString("0.0"))
                    .Append($"\n{Localization.GetText("WeaponInfoAreaDps")}: " + comp.AreaDps.ToString("0.0"))
                    .Append($"\n{Localization.GetText("WeaponInfoExplode")}: " + comp.DetDps.ToString("0.0"));


                if (HeatPerSecond > 0)
                    stringBuilder.Append("\n__________________________________" )
                        .Append($"\n{Localization.GetText("WeaponInfoHeatGenerated")}: {HeatPerSecond:0.0} W ({(HeatPerSecond / MaxHeat) :P}/s)")
                        .Append($"\n{Localization.GetText("WeaponInfoHeatDissipated")}: {HeatSinkRate:0.0} W ({(HeatSinkRate / MaxHeat):P}/s)")
                        .Append($"\n{Localization.GetText("WeaponInfoCurrentHeat")}: {CurrentHeat:0.0} J ({(CurrentHeat / MaxHeat):P})");

                stringBuilder.Append("\n__________________________________")
                    .Append($"\n{Localization.GetText("WeaponInfoCurrentDraw")}: " + SinkPower.ToString("0.000") + " MW");

                if (comp.HasEnergyWeapon)
                    stringBuilder.Append($"\n{Localization.GetText("WeaponInfoRequiredPower")}: " + Platform.Structure.ActualPeakPowerCombined.ToString("0.00") + " MJ");

                stringBuilder.Append($"\n\n{Localization.GetText("WeaponInfoDividerLineWeapon")}");

                var collection = TypeSpecific != CompTypeSpecific.Phantom ? Platform.Weapons : Platform.Phantoms;
                for (int i = 0; i < collection.Count; i++)
                {
                    var w = collection[i];
                    string shots;
                    if (w.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || w.ActiveAmmoDef.AmmoDef.Const.IsHybrid)
                    {
                        var chargeTime = w.AssignedPower > 0 ? (int)((w.MaxCharge - w.ProtoWeaponAmmo.CurrentCharge) / w.AssignedPower * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS) : 0;

                        shots = "\nCharging: " + w.Charging +" ("+ chargeTime+")";

                        if (w.ActiveAmmoDef.AmmoDef.Const.IsHybrid) shots += "\n" + w.ActiveAmmoDef.AmmoDef.AmmoRound + ": " + w.ProtoWeaponAmmo.CurrentAmmo;
                    }

                    else shots = "\n" + w.ActiveAmmoDef.AmmoDef.AmmoRound + ": " + w.ProtoWeaponAmmo.CurrentAmmo;

                    var burst = w.ActiveAmmoDef.AmmoDef.Const.BurstMode ? $"\n{Localization.GetText("WeaponInfoBurst")}: " + w.ShotsFired + "(" + w.System.ShotsPerBurst + $") - {Localization.GetText("WeaponInfoDelay")}: " + w .System.Values.HardPoint.Loading.DelayAfterBurst : string.Empty;

                    var endReturn = i + 1 != collection.Count ? "\n" : string.Empty;

                    stringBuilder.Append($"\n{Localization.GetText("WeaponInfoName")}: " + w.System.PartName + shots + burst + $"\n{Localization.GetText("WeaponInfoHasTarget")}: " + (w.ActiveAmmoDef.AmmoDef.Const.RequiresTarget ? w.Target.HasTarget.ToString() : "n/a") + $"\n{Localization.GetText("WeaponInfoReloading")}: " + w.Loading +  $"\n{Localization.GetText("WeaponInfoLoS")}: " + !w.PauseShoot + endReturn);

                    string otherAmmo = null;
                    for (int j = 0; j < w.System.AmmoTypes.Length; j++)
                    {
                        var ammo = w.System.AmmoTypes[j];
                        if (ammo == w.ActiveAmmoDef || !ammo.AmmoDef.Const.IsTurretSelectable || string.IsNullOrEmpty(ammo.AmmoDef.AmmoRound) || ammo.AmmoName == "Energy")
                            continue;
                        
                        if (otherAmmo == null)
                            otherAmmo = "\n\nAlternate Magazines:";

                        otherAmmo += $"\n{ammo.AmmoDef.AmmoRound}";
                    }

                    if (otherAmmo != null)
                        stringBuilder.Append(otherAmmo);
                }

                if (Debug || comp.Data.Repo.Values.Set.Overrides.Debug)
                {
                    foreach (var weapon in collection)
                    {
                        var chargeTime = weapon.AssignedPower > 0 ? (int)((weapon.MaxCharge - weapon.ProtoWeaponAmmo.CurrentCharge) / weapon.AssignedPower * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS) : 0;
                        stringBuilder.Append($"\n\nWeapon: {weapon.System.PartName} - Enabled: {IsWorking}");
                        stringBuilder.Append($"\nTargetState: {weapon.Target.CurrentState} - Manual: {weapon.BaseComp.UserControlled || weapon.Target.TargetState == Target.TargetStates.IsFake}");
                        stringBuilder.Append($"\nEvent: {weapon.LastEvent} - ProtoWeaponAmmo :{!weapon.NoMagsToLoad}");
                        stringBuilder.Append($"\nOverHeat: {weapon.PartState.Overheated} - Shooting: {weapon.IsShooting}");
                        stringBuilder.Append($"\nisAligned: {weapon.Target.IsAligned}");
                        stringBuilder.Append($"\nCanShoot: {weapon.ShotReady} - Charging: {weapon.Charging}");
                        stringBuilder.Append($"\nAiShooting: {weapon.AiShooting}");
                        stringBuilder.Append($"\n{(weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo ? "ChargeSize: " + weapon.ActiveAmmoDef.AmmoDef.Const.ChargSize : "MagSize: " +  weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize)} ({weapon.ProtoWeaponAmmo.CurrentCharge})");
                        stringBuilder.Append($"\nChargeTime: {chargeTime}");
                        stringBuilder.Append($"\nCharging: {weapon.Charging}({weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge})");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Weapon AppendingCustomInfo: {ex}", null, true); }
        }

        private void AppendingCustomInfoSupport(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetSystemStatus();

                stringBuilder.Append(status + "\nCurrent: )");

                stringBuilder.Append("\n\n==== SupportSys ====");

                var weaponCnt = Platform.Support.Count;
                for (int i = 0; i < weaponCnt; i++)
                {
                    var a = Platform.Support[i];
                }

                if (Debug)
                {
                    foreach (var support in Platform.Support)
                    {
                        stringBuilder.Append($"\n\nPart: {support.CoreSystem.PartName} - Enabled: {IsWorking}");
                        stringBuilder.Append($"\nManual: {support.BaseComp.UserControlled}");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AppendingCustomInfoSupport: {ex}", null, true); }
        }

        private void AppendingCustomInfoUpgrade(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetSystemStatus();

                stringBuilder.Append(status + "\nCurrent: )");

                stringBuilder.Append("\n\n==== Upgrade ====");

                var weaponCnt = Platform.Support.Count;
                for (int i = 0; i < weaponCnt; i++)
                {
                    var a = Platform.Upgrades[i];
                }

                if (Debug)
                {
                    foreach (var upgrade in Platform.Upgrades)
                    {
                        stringBuilder.Append($"\n\nPart: {upgrade.CoreSystem.PartName} - Enabled: {IsWorking}");
                        stringBuilder.Append($"\nManual: {upgrade.BaseComp.UserControlled}");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AppendingCustomInfoUpgrade: {ex}", null, true); }
        }
    }
}
