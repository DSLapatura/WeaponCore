﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Support;
using Jakaria;
using Jakaria.API;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Platform.ControlSys;
using static CoreSystems.Support.WeaponDefinition.TargetingDef.BlockTypes;
namespace CoreSystems
{
    public class GridMap
    {
        public readonly Dictionary<long, Ai.PlayerController> PlayerControllers = new Dictionary<long, Ai.PlayerController>();
        public ConcurrentCachingList<MyCubeBlock> MyCubeBocks;
        public MyGridTargeting Targeting;
        public GridGroupMap GroupMap;
        public volatile bool Trash;
        public uint PowerCheckTick;
        public uint LastControllerTick;
        public uint LastSortTick;
        public int MostBlocks;
        public bool SuspectedDrone;
        public bool Powered;

        internal void Clean()
        {
            PlayerControllers.Clear();
            Targeting = null;
            GroupMap = null;
            MyCubeBocks = null;
            LastSortTick = 0;
            MostBlocks = 0;
            PowerCheckTick = 0;
            LastControllerTick = 0;
            SuspectedDrone = false;
            Powered = false;
        }
    }

    internal struct DeferedTypeCleaning
    {
        internal uint RequestTick;
        internal ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> Collection;
    }

    public partial class Session
    {

        public void UpdateDbsInQueue()
        {
            DbUpdating = true;

            if (DbTask.IsComplete && DbTask.valid && DbTask.Exceptions != null)
                TaskHasErrors(ref DbTask, "DbTask");

            DbTask = MyAPIGateway.Parallel.StartBackground(ProcessDbs, ProcessDbsCallBack);
        }


        private void ProcessDbs()
        {
            for (int i = 0; i < DbsToUpdate.Count; i++)
            {

                var db = DbsToUpdate[i];
                using (db.Ai.DbLock.AcquireExclusiveUsing())
                {

                    var ai = db.Ai;
                    if (!ai.MarkedForClose && !ai.Closed && ai.Version == db.Version)
                        ai.Scan();
                }
            }
        }

        private void ProcessDbsCallBack()
        {
            try
            {
                DsUtil.Start("db");
                for (int d = 0; d < DbsToUpdate.Count; d++)
                {
                    var db = DbsToUpdate[d];
                    using (db.Ai.DbLock.AcquireExclusiveUsing())
                    {
                        var ai = db.Ai;
                        if (ai.TopEntity.MarkedForClose || ai.MarkedForClose || db.Version != ai.Version)
                        {
                            ai.ScanInProgress = false;
                            continue;
                        }

                        if (ai.MyPlanetTmp != null)
                            ai.MyPlanetInfo();

                        ai.DetectionInfo.Clean(ai);
                        ai.CleanSortedTargets();
                        ai.Targets.Clear();

                        var newEntCnt = ai.NewEntities.Count;
                        ai.SortedTargets.Capacity = newEntCnt;
                        for (int i = 0; i < newEntCnt; i++)
                        {
                            var detectInfo = ai.NewEntities[i];
                            var ent = detectInfo.Parent;
                            if (ent.Physics == null) continue;

                            var grid = ent as MyCubeGrid;
                            Ai targetAi = null;

                            if (grid != null)
                                EntityAIs.TryGetValue(grid, out targetAi);

                            var targetInfo = TargetInfoPool.Get();
                            targetInfo.Init(ref detectInfo, ai, targetAi);

                            ai.SortedTargets.Add(targetInfo);
                            ai.Targets[ent] = targetInfo;

                            var checkFocus = ai.Construct.Data.Repo.FocusData.HasFocus && (targetInfo.Target?.EntityId == ai.Construct.Data.Repo.FocusData.Target);

                            if (targetInfo.Drone)
                                ai.DetectionInfo.DroneAdd(ai, targetInfo);

                            if (ai.RamProtection && targetInfo.DistSqr < 136900 && targetInfo.IsGrid)
                                ai.DetectionInfo.RamProximity = true;

                            if (targetInfo.DistSqr < ai.MaxTargetingRangeSqr && (checkFocus || targetInfo.OffenseRating > 0))
                            {
                                if (checkFocus || targetInfo.DistSqr < ai.DetectionInfo.PriorityRangeSqr && targetInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                                {
                                    ai.DetectionInfo.PriorityInRange = true;
                                    ai.DetectionInfo.PriorityRangeSqr = targetInfo.DistSqr;
                                }

                                if (checkFocus || targetInfo.DistSqr < ai.DetectionInfo.OtherRangeSqr && targetInfo.EntInfo.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)
                                {
                                    ai.DetectionInfo.OtherInRange = true;
                                    ai.DetectionInfo.OtherRangeSqr = targetInfo.DistSqr;
                                }

                                if (targetInfo.Drone && targetInfo.DistSqr < ai.DetectionInfo.DroneRangeSqr)
                                {
                                    ai.DetectionInfo.DroneInRange = true;
                                    ai.DetectionInfo.DroneRangeSqr = targetInfo.DistSqr;
                                }
                            }
                        }

                        ai.NewEntities.Clear();
                        ai.SortedTargets.Sort(TargetCompare);
                        ai.TargetAis.Clear();
                        ai.TargetAis.AddRange(ai.TargetAisTmp);
                        ai.TargetAisTmp.Clear();

                        ai.Obstructions.Clear();
                        ai.Obstructions.AddRange(ai.ObstructionsTmp);
                        ai.ObstructionsTmp.Clear();

                        ai.MyShield = null;
                        ai.ShieldNear = false;
                        ai.FriendlyShieldNear = false;
                        if (ai.NearByShieldsTmp.Count > 0)
                            ai.NearByShield();

                        ai.StaticsInRange.Clear();
                        ai.StaticsInRange.AddRange(ai.StaticsInRangeTmp);
                        ai.StaticsInRangeTmp.Clear();
                        ai.StaticEntitiesInRange = ai.StaticsInRange.Count > 0;
                        ai.MyStaticInfo();

                        ai.BlockCount = ai.AiType == Ai.AiTypes.Grid ? ai.GridEntity.BlocksCount : 0;
                        ai.NearByEntities = ai.NearByEntitiesTmp;

                        if (!ai.DetectionInfo.PriorityInRange && ai.LiveProjectile.Count > 0)
                        {
                            ai.DetectionInfo.PriorityInRange = true;
                            ai.DetectionInfo.PriorityRangeSqr = 0;
                        }

                        ai.DetectionInfo.SomethingInRange = ai.DetectionInfo.PriorityInRange || ai.DetectionInfo.OtherInRange;

                        ai.DbReady = ai.SortedTargets.Count > 0 || ai.TargetAis.Count > 0 || Tick - ai.LiveProjectileTick < 3600 || ai.LiveProjectile.Count > 0 || ai.Construct.RootAi.Construct.ControllingPlayers.Count > 0 || ai.FirstRun;

                        MyCubeBlock activeCube;
                        ai.AiSleep = ai.Construct.RootAi.Construct.ControllingPlayers.Count <= 0 && (!ai.DetectionInfo.PriorityInRange && !ai.DetectionInfo.OtherInRange || !ai.DetectOtherSignals && ai.DetectionInfo.OtherInRange) && (ai.Data.Repo.ActiveTerminal <= 0 || MyEntities.TryGetEntityById(ai.Data.Repo.ActiveTerminal, out activeCube) && activeCube != null && !ai.SubGridCache.Contains(activeCube.CubeGrid));

                        ai.DbUpdated = true;
                        ai.FirstRun = false;
                        ai.ScanInProgress = false;
                    }
                }
                DbsToUpdate.Clear();
                DsUtil.Complete("db", true);
                DbUpdating = false;
            }
            catch (Exception ex) { Log.Line($"Exception in ProcessDbsCallBack: {ex}"); }
        }

        private void UpdateWaters()
        {

            if (!PlanetTemp.IsEmpty) {

                foreach (var planetToAdd in PlanetTemp) {
                    if (planetToAdd.Key.EntityId != 0) 
                        PlanetMap.TryAdd(planetToAdd.Key.EntityId, planetToAdd.Key);
                }

                PlanetTemp.Clear();
            }

            foreach (var planet in PlanetMap.Values)
            {
                WaterData data;
                if (WaterModAPI.HasWater(planet.EntityId))
                {
                    if (!WaterMap.TryGetValue(planet.EntityId, out data))
                    {
                        data = new WaterData(planet);
                        WaterMap[planet.EntityId] = data;
                    }

                    var radiusInfo = WaterModAPI.GetPhysical(data.WaterId);
                    data.Center = radiusInfo.Item1;
                    data.Radius = radiusInfo.Item2;
                    data.MinRadius = radiusInfo.Item3;
                    data.MaxRadius = radiusInfo.Item4;
                    var waveInfo = WaterModAPI.GetWaveData(data.WaterId);
                    data.WaveHeight = waveInfo.Item1;
                    data.WaveSpeed = waveInfo.Item2;
                    var tideInfo = WaterModAPI.GetTideData(data.WaterId);
                    data.TideHeight = tideInfo.Item1;
                    data.TideSpeed = tideInfo.Item2;
                }
                else WaterMap.TryRemove(planet.EntityId, out data);
            }
        }

        private void UpdatePlayerPainters()
        {
            ActiveMarks.Clear();
            foreach (var pair in PlayerDummyTargets)
            {
                PlayerMap player;
                if (Players.TryGetValue(pair.Key, out player))
                {
                    var painted = pair.Value.PaintedTarget;
                    MyEntity target;
                    if (painted.EntityId != 0 && MyEntities.TryGetEntityById(painted.EntityId, out target))
                    {
                        var grid = target as MyCubeGrid;
                        if (player.Player.IdentityId == PlayerId && grid != null && !Settings.ClientConfig.StikcyPainter)
                        {

                            var v3 = grid.LocalToGridInteger(painted.LocalPosition);
                            MyCube cube;

                            if (!grid.TryGetCube(v3, out cube))
                            {

                                var startPos = grid.GridIntegerToWorld(v3);
                                var endPos = startPos + (TargetUi.AimDirection * grid.PositionComp.LocalVolume.Radius);

                                if (grid.RayCastBlocks(startPos, endPos) == null)
                                {
                                    if (++painted.MissCount > 2)
                                        painted.ClearMark(Tick);
                                }
                            }
                        }
                        var rep = MyIDModule.GetRelationPlayerPlayer(PlayerId, player.Player.IdentityId);
                        var self = rep == MyRelationsBetweenPlayers.Self;
                        var friend = rep == MyRelationsBetweenPlayers.Allies;
                        var neut = rep == MyRelationsBetweenPlayers.Neutral;
                        var color = neut ? new Vector4(1, 1, 1, 1) : self ? new Vector4(0.025f, 1f, 0.25f, 2) : friend ? new Vector4(0.025f, 0.025f, 1, 2) : new Vector4(1, 0.025f, 0.025f, 2);
                        ActiveMarks.Add(new MyTuple<IMyPlayer, Vector4, Ai.FakeTarget>(player.Player, color, painted));
                    }
                }
            }
        }


        internal void CheckDirtyGridInfos(bool mainThread = false)
        {
            if ((mainThread || Tick60 || DirtyGrid))
            {
                using (_dityGridLock.Acquire())
                {
                    if (DirtyGridInfos.Count <= 0)
                        return;
                }

                if (GridTask.valid && GridTask.Exceptions != null)
                    TaskHasErrors(ref GridTask, "GridTask");
                if (mainThread) UpdateGrids();
                else GridTask = MyAPIGateway.Parallel.StartBackground(UpdateGrids);
            }
        }


        private void UpdateGrids()
        {
            DeferedUpBlockTypeCleanUp();

            DirtyGridsTmp.Clear();
            using (_dityGridLock.Acquire())
            {
                DirtyGridsTmp.AddRange(DirtyGridInfos);
                DirtyGridInfos.Clear();
                DirtyGrid = false;
            }

            for (int i = 0; i < DirtyGridsTmp.Count; i++)
            {
                var grid = DirtyGridsTmp[i];
                var newTypeMap = BlockTypePool.Get();
                newTypeMap[Offense] = ConcurrentListPool.Get();
                newTypeMap[Utility] = ConcurrentListPool.Get();
                newTypeMap[Thrust] = ConcurrentListPool.Get();
                newTypeMap[Steering] = ConcurrentListPool.Get();
                newTypeMap[Jumping] = ConcurrentListPool.Get();
                newTypeMap[Power] = ConcurrentListPool.Get();
                newTypeMap[Production] = ConcurrentListPool.Get();

                ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> noFatTypeMap;

                GridMap gridMap;
                if (GridToInfoMap.TryGetValue(grid, out gridMap))
                {
                    var allFat = gridMap.MyCubeBocks;
                    allFat.ApplyChanges();
                    if (gridMap.LastSortTick == 0 || Tick - gridMap.LastSortTick > 600)
                    {
                        gridMap.LastSortTick = Tick + 1;
                        allFat.Sort(CubeComparer);
                    }
                    var terminals = 0;
                    var tStatus = gridMap.Targeting == null || gridMap.Targeting.AllowScanning;
                    var thrusters = 0;
                    var gyros = 0;
                    var powerProducers = 0;
                    var warHead = 0;
                    var working = 0;
                    var functional = 0;
                    var controlblock = 0;
                    for (int j = 0; j < allFat.Count; j++)
                    {
                        var fat = allFat[j];
                        terminals++;
                        using (fat.Pin())
                        {

                            if (fat.MarkedForClose) continue;
                            if (fat.IsWorking)
                                ++working;

                            ++functional;

                            var cockpit = fat as MyCockpit;
                            var decoy = fat as IMyDecoy;
                            var bomb = fat as IMyWarhead;
                            var upgrade = fat as IMyUpgradeModule;

                            if (decoy != null)
                            {
                                WeaponDefinition.TargetingDef.BlockTypes type;
                                if (DecoyMap.TryGetValue(fat, out type))
                                    newTypeMap[type].Add(fat);
                                else
                                {
                                    newTypeMap[Utility].Add(fat);
                                    DecoyMap[fat] = Utility;
                                }
                                continue;
                            }

                            if (fat is IMyProductionBlock || upgrade != null && VanillaUpgradeModuleHashes.Contains(fat.BlockDefinition.Id.SubtypeName))
                                newTypeMap[Production].Add(fat);
                            else if (fat is IMyPowerProducer)
                            {
                                newTypeMap[Power].Add(fat);
                                powerProducers++;
                            }
                            else if (fat is IMyGunBaseUser || bomb != null || fat is MyConveyorSorter && PartPlatforms.ContainsKey(fat.BlockDefinition.Id))
                            {
                                if (bomb != null)
                                    warHead++;

                                if (!tStatus && fat is IMyGunBaseUser && !PartPlatforms.ContainsKey(fat.BlockDefinition.Id))
                                    tStatus = gridMap.Targeting.AllowScanning = true;

                                newTypeMap[Offense].Add(fat);
                            }
                            else if (upgrade != null || fat is IMyRadioAntenna  || fat is IMyLaserAntenna || fat is MyRemoteControl || fat is IMyShipToolBase || fat is IMyMedicalRoom || fat is IMyCameraBlock) newTypeMap[Utility].Add(fat);
                            else if (fat is MyThrust)
                            {
                                newTypeMap[Thrust].Add(fat);
                                thrusters++;
                            }
                            else if (fat is MyGyro || cockpit != null && cockpit.EnableShipControl)
                            {
                                newTypeMap[Steering].Add(fat);
                                gyros++;
                            }

                            else if (fat is MyJumpDrive) newTypeMap[Jumping].Add(fat);
                        }
                    }

                    foreach (var type in newTypeMap)
                        type.Value.ApplyAdditions();


                    gridMap.MyCubeBocks.ApplyAdditions();
                    gridMap.SuspectedDrone = warHead > 0 || powerProducers > 0 && thrusters > 0;

                    gridMap.Trash = terminals == 0;
                    gridMap.Powered = working > 0;
                    gridMap.PowerCheckTick = Tick;

                    var gridBlocks = grid.BlocksCount;

                    if (gridBlocks > gridMap.MostBlocks) gridMap.MostBlocks = gridBlocks;

                    ConcurrentDictionary<WeaponDefinition.TargetingDef.BlockTypes, ConcurrentCachingList<MyCubeBlock>> oldTypeMap;
                    if (GridToBlockTypeMap.TryGetValue(grid, out oldTypeMap))
                    {
                        GridToBlockTypeMap[grid] = newTypeMap;
                        BlockTypeCleanUp.Enqueue(new DeferedTypeCleaning { Collection = oldTypeMap, RequestTick = Tick });
                    }
                    else GridToBlockTypeMap[grid] = newTypeMap;
                }
                else if (GridToBlockTypeMap.TryRemove(grid, out noFatTypeMap))
                    BlockTypeCleanUp.Enqueue(new DeferedTypeCleaning { Collection = noFatTypeMap, RequestTick = Tick });
            }
            DirtyGridsTmp.Clear();
        }
    }
}
