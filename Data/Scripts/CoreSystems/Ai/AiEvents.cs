using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static CoreSystems.Session;

namespace CoreSystems.Support
{
    public partial class Ai
    {
        internal void RegisterMyGridEvents(bool register, bool force = false)
        {
            if (register) {

                if (Registered)
                    Log.Line("Ai RegisterMyGridEvents error");

                Registered = true;
                if (AiType == AiTypes.Grid)
                {
                    GridEntity.OnFatBlockAdded += FatBlockAdded;
                    GridEntity.OnFatBlockRemoved += FatBlockRemoved;

                    if (SubGridsRegistered.ContainsKey(GridEntity))
                        Log.Line("Main Grid Already Registered");

                    SubGridsRegistered[GridEntity] = byte.MaxValue;

                }

                TopEntity.OnMarkForClose += GridClose;


            }
            else {

                if (Registered) {

                    Registered = false;

                    if (AiType == AiTypes.Grid)
                    {
                        GridEntity.OnFatBlockAdded -= FatBlockAdded;
                        GridEntity.OnFatBlockRemoved -= FatBlockRemoved;
                        
                        if (BlockMonitoring)
                            DelayedEventRegistration();

                        if (!SubGridsRegistered.ContainsKey(GridEntity))
                            Log.Line("Main Grid Already UnRegistered");
                        SubGridsRegistered.Remove(GridEntity);
                    }

                    TopEntity.OnMarkForClose -= GridClose;


                }
                else if (!force) Log.Line($"NotRegistered:- Aimarked:{MarkedForClose} - aiClosed:{Closed} - Ticks:{Session?.Tick - AiCloseTick} - NullSession:{Session == null} - topMarked:{TopEntity.MarkedForClose}");
            }
        }

        internal void DelayedEventRegistration(bool register = false)
        {
            if (AiType == AiTypes.Grid)
            {
                if (register && Registered)
                {
                    BlockMonitoring = true;
                    GridEntity.OnBlockAdded += OnBlockAdded;
                    GridEntity.OnBlockRemoved += OnBlockRemoved;
                    GridEntity.OnBlockIntegrityChanged += OnBlockIntegrityChanged;
                    LastBlockChangeTick = Session.Tick > 0 ? Session.Tick : 1;

                }
                else if (!register && Registered)
                {
                    GridEntity.OnBlockAdded -= OnBlockAdded;
                    GridEntity.OnBlockRemoved -= OnBlockRemoved;
                    GridEntity.OnBlockIntegrityChanged -= OnBlockIntegrityChanged;
                }
                else
                {
                    Log.Line($"DelayedEventRegistration failed: {register} - {Registered}");
                }
            }
            else
                Log.Line("DelayedEventRegistration failed no grid");
        }

        internal void OnBlockAdded(IMySlimBlock slim)
        {
            BlockChangeArea.Min = Vector3.Min(BlockChangeArea.Min, slim.Min);
            BlockChangeArea.Max = Vector3.Max(BlockChangeArea.Max, slim.Max);
            AddedBlockPositions[slim.Position] = slim;

            LastBlockChangeTick = Session.Tick;
        }

        internal void OnBlockRemoved(IMySlimBlock slim)
        {

            BlockChangeArea.Min = Vector3.Min(BlockChangeArea.Min, slim.Min);
            BlockChangeArea.Max = Vector3.Max(BlockChangeArea.Max, slim.Max);
            RemovedBlockPositions[slim.Position] = slim;

            LastBlockChangeTick = Session.Tick;
        }

        internal void OnBlockIntegrityChanged(IMySlimBlock mySlimBlock)
        {

        }

        internal void FatBlockAdded(MyCubeBlock cube)
        {
            try
            {
                var battery = cube as MyBatteryBlock;

                var weaponType = (cube is MyConveyorSorter || cube is IMyUserControllableGun);
                var isWeaponBase = weaponType && cube.BlockDefinition != null && (Session.ReplaceVanilla && Session.VanillaIds.ContainsKey(cube.BlockDefinition.Id) || Session.PartPlatforms.ContainsKey(cube.BlockDefinition.Id));
                var stator = cube as IMyMotorStator;

                if (!isWeaponBase && (cube is MyConveyor || cube is IMyConveyorTube || cube is MyConveyorSorter || cube is MyCargoContainer || cube is MyCockpit || cube is IMyAssembler || cube is IMyShipConnector) && cube.CubeGrid.IsSameConstructAs(GridEntity)) { 
                    
                    MyInventory inventory;
                    if (cube.HasInventory && cube.TryGetInventory(out inventory) && InventoryMonitor.TryAdd(cube, inventory)) {

                        inventory.InventoryContentChanged += CheckAmmoInventory;
                        Construct.RootAi.Construct.NewInventoryDetected = true;

                        int monitors;
                        if (!Session.InventoryMonitors.TryGetValue(inventory, out monitors)) {
                            
                            Session.InventoryMonitors[inventory] = 0;
                            Session.InventoryItems[inventory] = Session.PhysicalItemListPool.Get();
                            Session.ConsumableItemList[inventory] = Session.BetterItemsListPool.Get();
                        }
                        else
                            Session.InventoryMonitors[inventory] = monitors + 1;
                    }
                }
                else if (battery != null) {
                    if (Batteries.Add(battery)) SourceCount++;
                    UpdatePowerSources = true;
                } 
                else if (isWeaponBase)
                {
                    MyOrientedBoundingBoxD b;
                    BoundingSphereD s;
                    MyOrientedBoundingBoxD blockBox;
                    SUtils.GetBlockOrientedBoundingBox(cube, out blockBox);
                    if (!ModOverride && Session.IsPartAreaRestricted(cube.BlockDefinition.Id.SubtypeId, blockBox, cube.CubeGrid, cube.EntityId, null, out b, out s))
                    {
                        if (Session.IsServer)
                        {
                            cube.CubeGrid.RemoveBlock(cube.SlimBlock);
                        }
                    }
                }
                else if (stator != null)
                {
                    StatorMap statorMap;
                    if (!Session.StatorMaps.TryGetValue(stator, out statorMap))
                    {
                        statorMap = Session.StatorMapPool.Count > 0 ? Session.StatorMapPool.Pop() : new StatorMap();
                        statorMap.Stator = stator;
                        Session.StatorMaps[stator] = statorMap;
                    }

                    if (stator.TopGrid == TopEntity)
                    {
                        statorMap.TopAi = this;
                        TopStators[stator] = statorMap;
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex} - {cube?.BlockDefinition == null} - RootAiNull: {Construct.RootAi == null}", null, true); }
        }

        private void FatBlockRemoved(MyCubeBlock cube)
        {
            try
            {
                var stator = cube as IMyMotorStator;
                if (stator != null)
                {
                    TopStators.Remove(stator);
                    return;
                }


                var weaponType = (cube is MyConveyorSorter || cube is IMyUserControllableGun);
                var cubeDef = cube.BlockDefinition;
                var isWeaponBase = weaponType && cubeDef != null && (Session.ReplaceVanilla && Session.VanillaIds.ContainsKey(cubeDef.Id) || Session.PartPlatforms.ContainsKey(cubeDef.Id));
                var battery = cube as MyBatteryBlock;
                MyInventory inventory;


                if (!isWeaponBase && cube.HasInventory && cube.TryGetInventory(out inventory)) {

                    if (!InventoryRemove(cube, inventory))
                        Log.Line($"FatBlock inventory remove failed: {cube.BlockDefinition?.Id.SubtypeName} - gridMatch:{cube.CubeGrid == TopEntity} - aiMarked:{MarkedForClose} - {cube.CubeGrid.DebugName} - {TopEntity?.DebugName}");
                }
                else if (battery != null)
                {

                    if (Batteries.Remove(battery))
                        SourceCount--;

                    UpdatePowerSources = true;
                }

            }
            catch (Exception ex) { Log.Line($"Exception in FatBlockRemoved last: {ex} - Marked: {MarkedForClose} - Closed:{Closed}", null, true); }
        }
        
        
        private bool InventoryRemove(MyEntity entity, MyInventory inventory)
        {
            try
            {
                MyInventory oldInventory;
                if (InventoryMonitor.TryRemove(entity, out oldInventory)) {

                    inventory.InventoryContentChanged -= CheckAmmoInventory;

                    int monitors;
                    if (Session.InventoryMonitors.TryGetValue(inventory, out monitors)) {

                        if (--monitors < 0) {

                            MyConcurrentList<MyPhysicalInventoryItem> removedPhysical;
                            MyConcurrentList<BetterInventoryItem> removedBetter;

                            if (Session.InventoryItems.TryRemove(inventory, out removedPhysical))
                                Session.PhysicalItemListPool.Return(removedPhysical);

                            if (Session.ConsumableItemList.TryRemove(inventory, out removedBetter))
                                Session.BetterItemsListPool.Return(removedBetter);
                            
                            Session.InventoryMonitors.Remove(inventory);
                        }
                        else Session.InventoryMonitors[inventory] = monitors;
                    }
                    else return false;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in InventoryRemove: {ex}"); }
            return true;
        }

        internal void CheckAmmoInventory(MyInventoryBase inventory, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                if (amount <= 0 || item.Content == null || inventory == null) return;
                var itemDef = item.Content.GetObjectId();
                if (Session.AmmoDefIds.Contains(itemDef))
                {
                    Construct.RootAi?.Construct.RecentItems.Add(itemDef);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in CheckAmmoInventory: {ex} - BlockName:{((MyEntity)inventory?.Entity)?.DebugName} - BlockMarked:{((MyEntity)inventory?.Entity)?.MarkedForClose} - aiMarked:{MarkedForClose} - Session:{Session != null} - item:{item.Content?.SubtypeName} - RootConstruct:{Construct?.RootAi?.Construct != null}", null, true); }
        }

        internal void GridClose(MyEntity myEntity)
        {
            if (Session == null || TopEntity == null || Closed)
            {
                Log.Line($"[GridClose] Session: {Session != null} - MyGrid:{TopEntity != null} - Closed:{Closed} - myEntity:{myEntity != null}");
                return;
            }

            MarkedForClose = true;
            AiMarkedTick = Session.Tick;

            RegisterMyGridEvents(false);

            CleanSubGrids();
            ForceCloseAiInventories();
            
            Session.DelayedAiClean.Add(this);
            Session.DelayedAiClean.ApplyAdditions();
        }
    }
}
