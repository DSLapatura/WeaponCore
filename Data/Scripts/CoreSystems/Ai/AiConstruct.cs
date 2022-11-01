using System;
using System.Collections.Generic;
using CoreSystems.Platform;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.FocusData;

namespace CoreSystems.Support
{
    public partial class Ai
    {
        public void SubGridChanges()
        {
            SubGridCache.Clear();
            if (TopEntityMap.GroupMap == null)
            {
                Log.Line($"GridGroup null");
                return;
            }

            foreach (var ent in TopEntityMap.GroupMap.Construct.Keys) {

                var grid = ent as MyCubeGrid;
                if (grid == null)
                    continue;
                    
                SubGridCache.Add(grid);
                if (grid == TopEntity || SubGridsRegistered.ContainsKey(grid)) continue;
                RegisterSubGrid(grid);

            }

            foreach (var sub in SubGridsRegistered.Keys) {

                if (sub == TopEntity)
                    continue;

                if (!TopEntityMap.GroupMap.Construct.ContainsKey(sub))
                    UnRegisterSubGrid(sub);
            }

            Construct.ControllingPlayers.Clear();
            if (Construct.WeaponGroups.Count > 0)
                Construct.CleanWeaponGroups(Session);
        }

        public void RegisterSubGrid(MyCubeGrid grid)
        {
            grid.Flags |= (EntityFlags)(1 << 31);
            grid.OnFatBlockAdded += FatBlockAdded;
            grid.OnFatBlockRemoved += FatBlockRemoved;

            SubGridsRegistered[grid] = byte.MaxValue;

            foreach (var cube in grid.GetFatBlocks()) {

                var battery = cube as MyBatteryBlock;
                var stator = cube as IMyMotorStator;
                var tool = cube as IMyShipToolBase;
                if (battery != null || cube.HasInventory || stator != null || tool != null)
                {
                    FatBlockAdded(cube);
                }
            }
        }

        public void UnRegisterSubGrid(MyCubeGrid grid)
        {
            SubGridsRegistered.Remove(grid);
            grid.OnFatBlockAdded -= FatBlockAdded;
            grid.OnFatBlockRemoved -= FatBlockRemoved;

            foreach (var cube in grid.GetFatBlocks()) {
                
                var battery = cube as MyBatteryBlock;
                var stator = cube as IMyMotorStator;
                var tool = cube as IMyShipToolBase;
                if (InventoryMonitor.ContainsKey(cube) || battery != null && Batteries.Contains(battery) || stator != null || tool != null)
                {
                    FatBlockRemoved(cube);
                }
            }

            Ai removeAi;
            if (!Session.EntityAIs.ContainsKey(grid))
                Session.EntityToMasterAi.TryRemove(grid, out removeAi);
        }

        public void CleanSubGrids()
        {
            foreach (var grid in SubGridCache) {
                if (grid == TopEntity) continue;
                UnRegisterSubGrid(grid);
            }

            SubGridCache.Clear();
        } 

        public class Constructs
        {
            internal readonly HashSet<MyDefinitionId> RecentItems = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
            internal readonly HashSet<Weapon> OutOfAmmoWeapons = new HashSet<Weapon>();
            internal readonly Dictionary<MyStringHash, int> Counter = new Dictionary<MyStringHash, int>(MyStringHash.Comparer);
            internal readonly Focus Focus = new Focus();
            internal readonly ConstructData Data = new ConstructData();
            internal readonly Dictionary<long, PlayerController> ControllingPlayers = new Dictionary<long, PlayerController>();
            internal readonly Dictionary<MyEntity, TargetInfo> ConstructTargetInfoCache = new Dictionary<MyEntity, TargetInfo>();

            internal readonly HashSet<MyEntity> PreviousTargets = new HashSet<MyEntity>();
            internal readonly RunningAverage DamageAverage = new RunningAverage(10);
            internal readonly Dictionary<int, WeaponGroup> WeaponGroups = new Dictionary<int, WeaponGroup>();
            internal readonly Ai Ai;
            internal float OptimalDps;
            internal int BlockCount;
            internal Ai RootAi;
            internal Ai LargestAi;
            internal bool NewInventoryDetected;
            internal int DroneCount;
            internal uint LastDroneTick;
            internal uint LastEffectUpdateTick;
            internal uint TargetResetTick;
            internal uint LastRefreshTick;
            internal uint LastTargetInfoTick;
            internal bool DirtyWeaponGroups;
            internal bool DroneAlert;
            internal bool InfiniteResources;
            internal double TotalEffect;
            internal long TotalPrimaryEffect;
            internal long TotalAoeEffect;
            internal long TotalShieldEffect;
            internal long TotalProjectileEffect;
            internal double PreviousTotalEffect;
            internal double AddEffect;
            internal double AverageEffect;
            internal double MaxLockRange;


            internal enum UpdateType
            {
                Full,
                Focus,
                None,
            }

            public Constructs(Ai ai)
            {
                Ai = ai;
            }

            internal void Refresh()
            {
                
                if (Ai.Session.IsServer && RootAi.Construct.RecentItems.Count > 0) 
                    CheckEmptyWeapons();

                OptimalDps = 0;
                BlockCount = 0;
                LastRefreshTick = Ai.Session.Tick;
                if (Ai.TopEntity != null) {
                    Ai leadingAi = null;
                    Ai largestAi = null;
                    int leadingBlocks = 0;
                    var maxLockRange = 0d;
                    foreach (var grid in Ai.SubGridCache) {

                        Ai subAi;
                        if (Ai.Session.EntityAIs.TryGetValue(grid, out subAi)) {
                            
                            if (leadingAi == null)
                                leadingAi = subAi;
                            else  {
                                if (leadingAi.TopEntity.EntityId > grid.EntityId)
                                    leadingAi = subAi;
                            }
                        }
                        if (Ai.Session.TopEntityToInfoMap.ContainsKey(grid)) {
                            var blockCount = Ai.Session.TopEntityToInfoMap[grid].MostBlocks;
                            if (blockCount > leadingBlocks)
                            {
                                leadingBlocks = blockCount;
                                largestAi = subAi;
                            }
                            BlockCount += blockCount;

                            if (subAi != null)
                            {
                                OptimalDps += subAi.OptimalDps;
                                if (subAi.Construct.MaxLockRange > maxLockRange)
                                    maxLockRange = subAi.Construct.MaxLockRange;
                            }
                        }
                        else Log.Line($"ConstructRefresh Failed sub no GridMap, sub is caller:{grid == Ai.TopEntity}");
                    }
                    RootAi = leadingAi;
                    LargestAi = largestAi;

                    if (RootAi == null) {

                        RootAi = Ai;
                        LargestAi = Ai;
                        if (Ai.Construct.MaxLockRange > maxLockRange)
                            maxLockRange = Ai.Construct.MaxLockRange;
                    }

                    RootAi.Construct.MaxLockRange = maxLockRange;
                }
                else
                {
                    Log.Line($"2 - does this ever get hit?");

                    if (Ai.TopEntity != null && Ai.AiType != AiTypes.Grid)
                    {
                        RootAi = Ai;
                        LargestAi = Ai;
                        Ai.Session.EntityToMasterAi[RootAi.TopEntity] = RootAi;

                    }
                    else
                    {
                        RootAi = null;
                        LargestAi = null;
                    }
                }

                if (RootAi != null) {

                    if (RootAi.AiType != AiTypes.Grid)
                    {
                        foreach (var ai in Ai.TopEntityMap.GroupMap.Ais)
                            RootAi.Session.EntityToMasterAi[ai.TopEntity] = RootAi;
                    }
                    else
                    {
                        foreach (var sub in Ai.SubGridCache)
                            RootAi.Session.EntityToMasterAi[sub] = RootAi;
                    }
                }
            }

            internal void UpdateEffect(uint tick)
            {
                var add = TotalEffect - PreviousTotalEffect;
                AddEffect = add > 0 ? add : AddEffect;
                AverageEffect = DamageAverage.Add((int)add);
                PreviousTotalEffect = TotalEffect;
                LastEffectUpdateTick = tick;
            }

            internal void DroneCleanup()
            {
                DroneAlert = false;
                DroneCount = 0;
            }

            internal void UpdateConstruct(UpdateType type, bool sync = true)
            {
                switch (type)
                {
                    case UpdateType.Full:
                    {
                        UpdateLeafs();
                        if (RootAi.Session.MpActive && RootAi.Session.IsServer && sync)
                            RootAi.Session.SendConstruct(RootAi);
                        break;
                    }
                    case UpdateType.Focus:
                    {
                        UpdateLeafFoci();
                        if (RootAi.Session.MpActive && RootAi.Session.IsServer && sync)
                            RootAi.Session.SendConstructFoci(RootAi);
                        break;
                    }
                }
            }

            internal void NetRefreshAi()
            {
                if (RootAi.AiType == AiTypes.Grid) {

                    foreach (var sub in RootAi.SubGridCache) {

                        Ai ai;
                        if (RootAi.Session.EntityAIs.TryGetValue(sub, out ai))
                        {
                            ai.AiSleep = false;

                            if (ai.Session.MpActive)
                                ai.Session.SendAiData(ai);
                        }
                    }
                }
                else {
                    RootAi.AiSleep = false;

                    if (RootAi.Session.MpActive)
                        RootAi.Session.SendAiData(RootAi);
                }
            }

            internal static void WeaponGroupsMarkDirty(GridGroupMap map)
            {
                if (map == null || map.Ais.Count == 0)
                {
                    //Log.Line($"WeaponGroupsMarkDirty gridgroup had no AIs");
                    return;
                }
                var rootAi = map.Ais[0].Construct.RootAi;
                
                if (rootAi != null)
                    rootAi.Construct.DirtyWeaponGroups = true;
            }

            internal static void RebuildWeaponGroups(GridGroupMap map)
            {
                if (map.Ais.Count == 0)
                {
                    Log.Line($"RebuildWeaponGroups gridgroup had no AIs");
                    return;
                }
                var s = map.Session;
                var rootAi = map.Ais[0].Construct.RootAi;
                var rootConstruct = rootAi.Construct;

                rootConstruct.DirtyWeaponGroups = false;

                if (rootConstruct.WeaponGroups.Count > 0)
                    rootConstruct.CleanWeaponGroups(s);

                foreach (var ai in map.Ais)
                {
                    foreach (var g in ai.CompWeaponGroups)
                    {
                        if (!g.Key.IsWorking)
                            continue;

                        var overrides = g.Key.Data.Repo.Values.Set.Overrides;

                        WeaponGroup group;
                        if (!rootConstruct.WeaponGroups.TryGetValue(g.Value, out group))
                            group = s.GroupPool.Count > 0 ? s.GroupPool.Pop() : new WeaponGroup();
                        
                        rootConstruct.WeaponGroups[g.Value] = group;


                        WeaponSequence sequence;
                        if (!group.Sequences.TryGetValue(overrides.SequenceId, out sequence))
                            sequence = s.SequencePool.Count > 0 ? s.SequencePool.Pop() : new WeaponSequence();

                        group.Sequences[overrides.SequenceId] = sequence;
                        group.OrderSequencesIds.Add(overrides.SequenceId);
                        sequence.AddWeapon(g.Key);
                    }
                }

                foreach (var g in rootConstruct.WeaponGroups)
                    g.Value.OrderSequencesIds.Sort();
            }

            internal static void UpdatePlayerStates(GridGroupMap map)
            {
                if (map.Ais.Count == 0)
                {
                    Log.Line($"UpdatePlayerStates gridgroup had no AIs");
                    return;
                }
                var rootAi = map.Ais[0].Construct.RootAi;

                var rootConstruct = rootAi.Construct;
                var s = rootAi.Session;
                rootConstruct.ControllingPlayers.Clear();


                foreach (var req in map.ControlPlayerRequest)
                {
                    if (req.Value.LastChangeReason == PlayerController.ChangeType.Add)
                        rootAi.TopEntityMap.PlayerControllers[req.Key] = req.Value;
                    else if (req.Value.LastChangeReason == PlayerController.ChangeType.Remove)
                        rootAi.TopEntityMap.PlayerControllers.Remove(req.Key);
                }

                foreach (var ai in rootAi.TopEntityMap.GroupMap.Ais)
                {
                    TopMap topMap;
                    if (s.TopEntityToInfoMap.TryGetValue(ai.TopEntity, out topMap))
                    {
                        foreach (var c in topMap.PlayerControllers)
                        {
                            rootConstruct.ControllingPlayers[c.Key] = c.Value;
                            UpdatePlayerLockState(rootAi.Session, c.Key);
                        }
                    }
                    else 
                        Log.Line($"UpdatePlayerStates could not find grid map");
                }
            }

            internal static bool UpdatePlayerLockState(Session s, long playerId)
            {
                PlayerMap playerMap;
                if (!s.Players.TryGetValue(playerId, out playerMap))
                {
                    if (s.IsClient && !s.DeferredPlayerLock.ContainsKey(playerId))
                        s.DeferredPlayerLock[playerId] = 120;
                }
                else if (playerMap.Player.Character != null && playerMap.Player.Character.Components.TryGet(out playerMap.TargetFocus) && playerMap.Player.Character.Components.TryGet(out playerMap.TargetLock))
                {
                    playerMap.TargetFocusDef.AngularToleranceFromCrosshair = 25;
                    //playerMap.TargetFocusDef.FocusSearchMaxDistance = !setDefault ? RootAi.Construct.MaxLockRange : 2000;
                    playerMap.TargetFocusDef.FocusSearchMaxDistance = 0; //temp
                    playerMap.TargetFocus.Init(playerMap.TargetFocusDef);
                    return true;
                }
                else 
                {
                    if (s.IsClient && !s.DeferredPlayerLock.ContainsKey(playerId))
                        s.DeferredPlayerLock[playerId] = 120;
                }
                return false;
            }

            internal static void BuildAiListAndCounters(GridGroupMap map)
            {
                var ais = map.Ais;

                for (int i = 0; i < ais.Count; i++)
                {

                    var checkAi = ais[i];
                    checkAi.Construct.Counter.Clear();

                    for (int x = 0; x < ais.Count; x++)
                    {
                        foreach (var wc in ais[x].PartCounting)
                            checkAi.Construct.AddWeaponCount(wc.Key, wc.Value.Current);
                    }
                }
            }

            internal static void BuildAiListAndCounters(Ai cAi)
            {

                var ais = cAi.TopEntityMap.GroupMap.Ais;
                for (int i = 0; i < ais.Count; i++) {

                    var checkAi = ais[i];
                    checkAi.Construct.Counter.Clear();

                    for (int x = 0; x < ais.Count; x++) {
                        foreach (var wc in ais[x].PartCounting)
                            checkAi.Construct.AddWeaponCount(wc.Key, wc.Value.Current);
                    }
                }
            }

            internal bool GetConstructTargetInfo(MyEntity target, out TargetInfo targetInfo)
            {
                for (int i = 0; i < Ai.TopEntityMap.GroupMap.Ais.Count; i++)
                {
                    var ai = Ai.TopEntityMap.GroupMap.Ais[i];
                    if (ai.TargetsUpdatedTick > LastTargetInfoTick)
                    {
                        LastTargetInfoTick = Ai.Session.Tick;
                        break;
                    }
                }

                if (LastTargetInfoTick == Ai.Session.Tick)
                {
                    ConstructTargetInfoCache.Clear();
                    for (int i = 0; i < Ai.TopEntityMap.GroupMap.Ais.Count; i++)
                    {
                        var ai = Ai.TopEntityMap.GroupMap.Ais[i];
                        foreach (var info in ai.Targets)
                        {
                            ConstructTargetInfoCache[info.Key] = info.Value;
                        }
                    }
                }

                return ConstructTargetInfoCache.TryGetValue(target, out targetInfo);
            }

            internal void AddWeaponCount(MyStringHash weaponHash, int incrementBy = 1)
            {
                if (!Counter.ContainsKey(weaponHash))
                    Counter.Add(weaponHash, incrementBy);
                else Counter[weaponHash] += incrementBy;
            }

            internal int GetPartCount(MyStringHash weaponHash)
            {
                int value;
                return Counter.TryGetValue(weaponHash, out value) ? value : 0;
            }

            internal void UpdateLeafs()
            {
                foreach (var sub in RootAi.SubGridCache)
                {
                    if (RootAi.TopEntity == sub)
                        continue;

                    Ai ai;
                    if (RootAi.Session.EntityAIs.TryGetValue(sub, out ai))
                    {
                        ai.Construct.Data.Repo.Sync(ai.Construct, RootAi.Construct.Data.Repo, true);
                    }
                }
            }

            internal void UpdateLeafFoci()
            {
                foreach (var sub in RootAi.SubGridCache)
                {
                    if (RootAi.TopEntity == sub)
                        continue;

                    Ai ai;
                    if (RootAi.Session.EntityAIs.TryGetValue(sub, out ai))
                        ai.Construct.Data.Repo.FocusData.Sync(ai, RootAi.Construct.Data.Repo.FocusData);
                }
            }

            internal void CheckEmptyWeapons()
            {
                foreach (var w in OutOfAmmoWeapons)
                {
                    if (RecentItems.Contains(w.ActiveAmmoDef.AmmoDefinitionId))
                        w.CheckInventorySystem = true;
                }
                RecentItems.Clear();
            }

            internal void CheckForMissingAmmo()
            {
                NewInventoryDetected = false;
                foreach (var w in RootAi.Construct.OutOfAmmoWeapons)
                    w.CheckInventorySystem = true;
            }

            internal void Init(Ai ai)
            {
                RootAi = ai;
                Data.Init(ai);
            }

            internal void CleanWeaponGroups(Session session)
            {
                foreach (var pair in WeaponGroups)
                {
                    var group = pair.Value;
                    group.Clean(session);
                }
                WeaponGroups.Clear();
                DirtyWeaponGroups = false;
            }

            internal void Clean()
            {
                if (TotalEffect > 0)
                {
                    try
                    {
                        PlayerMap player;
                        var playerName = Ai.Session.Players.TryGetValue(Ai.AiOwner, out player) ? player.Player.DisplayName ?? string.Empty : string.Empty;
                        Log.Stats($"{Ai.TopEntity?.DisplayName}, {playerName}, {(long)TotalEffect}, {TotalPrimaryEffect}, {TotalAoeEffect}, {TotalShieldEffect}, {TotalProjectileEffect}", "griddmgstats");
                    }
                    catch (Exception ex) { Log.Line($"Exception in ConstructClean: {ex}", null, true); }
                }

                if (WeaponGroups.Count > 0)
                    CleanWeaponGroups(RootAi.Session);

                Data.Clean();
                OptimalDps = 0;
                BlockCount = 0;
                AverageEffect = 0;
                TotalEffect = 0;
                TotalPrimaryEffect = 0;
                TotalAoeEffect = 0;
                TotalShieldEffect = 0;
                TotalProjectileEffect = 0;
                PreviousTotalEffect = 0;
                LastRefreshTick = 0;
                TargetResetTick = 0;
                LastTargetInfoTick = 0;
                DirtyWeaponGroups = false;
                RootAi = null;
                LargestAi = null;
                Counter.Clear();
                PreviousTargets.Clear();
                ControllingPlayers.Clear();
                ConstructTargetInfoCache.Clear();
            }

            public void RegisterModFromDamageHandler(long modId) => RegisterModFromDamageHandler(modId, true);
            public void UnregisterModFromDamageHandler(long modId) => RegisterModFromDamageHandler(modId, false);

            public void RegisterModFromDamageHandler(long modId, bool add)
            {
                foreach (var ai in RootAi.TopEntityMap.GroupMap.Ais)
                {
                    foreach (var comp in ai.CompBase.Values)
                    {
                        if (add)
                            comp.DamageHandlerRegistrants.Add(modId);
                        else
                            comp.DamageHandlerRegistrants.Remove(modId);
                    }
                }
            }

            public bool GiveAllCompsInfiniteResources()
            {
                InfiniteResources = !InfiniteResources;
                foreach (var ai in RootAi.TopEntityMap.GroupMap.Ais)
                    foreach (var comp in ai.CompBase.Values)
                        comp.InfiniteResource = InfiniteResources;

                return InfiniteResources;
            }
        }
    }

    public class Focus
    {
        public long PrevTarget;
        public long OldTarget;
        public LockModes OldLocked;

        public uint LastUpdateTick;
        public bool OldHasFocus;
        public float OldDistToNearestFocusSqr;
        
        public bool ChangeDetected(Ai ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;
            var forceUpdate = LastUpdateTick == 0 || ai.Session.Tick - LastUpdateTick > 600;
            if (forceUpdate || fd.Target != OldTarget || fd.Locked != OldLocked || fd.HasFocus != OldHasFocus || Math.Abs(fd.DistToNearestFocusSqr - OldDistToNearestFocusSqr) > 0)
            {
                if (fd.Target > 0)
                    PrevTarget = fd.Target;
                
                OldTarget = fd.Target;
                OldLocked = fd.Locked;
                OldHasFocus = fd.HasFocus;
                OldDistToNearestFocusSqr = fd.DistToNearestFocusSqr;
                LastUpdateTick = ai.Session.Tick;
                return true;
            }

            return false;
        }

        internal void ServerAddFocus(MyEntity target, Ai ai)
        {
            var session = ai.Session;
            var fd = ai.Construct.Data.Repo.FocusData;
            if (fd.Target != target.EntityId)
            {
                fd.Target = target.EntityId;
                ai.Construct.RootAi.Construct.TargetResetTick = session.Tick + 1;
            }
            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(Ai.Constructs.UpdateType.Focus, ChangeDetected(ai));
        }

        internal void RequestAddFocus(MyEntity target, Ai ai)
        {
            if (ai.Session.IsServer)
                ServerAddFocus(target, ai);
            else
                ai.Session.SendFocusTargetUpdate(ai, target.EntityId);
        }

        internal void ServerCycleLock(Ai ai)
        {
            var session = ai.Session;
            var fd = ai.Construct.Data.Repo.FocusData;
            var modeCount = Enum.GetNames(typeof(LockModes)).Length;

            var nextMode = (int)fd.Locked + 1 < modeCount ? fd.Locked + 1 : 0;
            fd.Locked = nextMode;
            ai.Construct.RootAi.Construct.TargetResetTick = session.Tick + 1;
            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(Ai.Constructs.UpdateType.Focus, ChangeDetected(ai));
        }

        internal void RequestAddLock(Ai ai)
        {
            if (ai.Session.IsServer)
                ServerCycleLock(ai);
            else
                ai.Session.SendFocusLockUpdate(ai);
        }

        internal void ServerReleaseActive(Ai ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;

            fd.Target = -1;
            fd.Locked = LockModes.None;

            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(Ai.Constructs.UpdateType.Focus, ChangeDetected(ai));
        }

        internal void RequestReleaseActive(Ai ai)
        {
            if (ai.Session.IsServer)
                ServerReleaseActive(ai);
            else
                ai.Session.SendReleaseActiveUpdate(ai);

        }

        internal bool ServerIsFocused(Ai ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;

            if (fd.Target > 0 && MyEntities.GetEntityById(fd.Target) != null) {
                fd.HasFocus = true;
                return true;
            }

            fd.Target = -1;
            fd.Locked = LockModes.None;
            fd.HasFocus = false;

            return false;
        }

        internal bool ClientIsFocused(Ai ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;

            if (ai.Session.IsServer)
                return ServerIsFocused(ai);

            return fd.Target > 0 && MyEntities.GetEntityById(fd.Target) != null;
        }

        internal bool GetPriorityTarget(Ai ai, out MyEntity target)
        {
            var fd = ai.Construct.Data.Repo.FocusData;

            if (fd.Target > 0 && MyEntities.TryGetEntityById(fd.Target, out target, true))
                return true;

            if (MyEntities.TryGetEntityById(fd.Target, out target, true))
                return true;

            target = null;
            return false;
        }

        internal void ReassignTarget(MyEntity target, Ai ai)
        {
            var fd = ai.Construct.Data.Repo.FocusData;

            if (target == null || target.MarkedForClose) return;
            fd.Target = target.EntityId;
            ServerIsFocused(ai);

            ai.Construct.UpdateConstruct(Ai.Constructs.UpdateType.Focus, ChangeDetected(ai));
        }

        internal bool FocusInRange(Weapon w)
        {

            if (w.PosChangedTick != w.Comp.Session.Tick)
                w.UpdatePivotPos();

            var fd = w.Comp.Ai.Construct.Data.Repo.FocusData;
            
            fd.DistToNearestFocusSqr = float.MaxValue;
                if (fd.Target <= 0)
                    return false;

            MyEntity target;
            if (MyEntities.TryGetEntityById(fd.Target, out target))
            {
                var sphere = target.PositionComp.WorldVolume;
                var distSqr = (float)MyUtils.GetSmallestDistanceToSphere(ref w.MyPivotPos, ref sphere);
                distSqr *= distSqr;
                if (distSqr < fd.DistToNearestFocusSqr)
                    fd.DistToNearestFocusSqr = distSqr;
            }

            return fd.DistToNearestFocusSqr <= w.MaxTargetDistanceSqr;
        }

        internal bool EntityIsFocused(Ai ai, MyEntity entToCheck)
        {
            var targets = ai.Construct?.Data?.Repo?.FocusData?.Target;

            if (targets != null)
            {
                var tId = targets ?? 0;
                if (tId == 0)
                    return false;

                MyEntity target;
                if (MyEntities.TryGetEntityById(tId, out target) && target == entToCheck)
                    return true;
            }
            return false;
        }

        internal bool ValidFocusTarget(Weapon w)
        {
            var targets = w.Comp.Ai.Construct.Data.Repo.FocusData?.Target;

            var targetEnt = w.Target.TargetEntity;
            if (w.PosChangedTick != w.Comp.Session.Tick)
                w.UpdatePivotPos();

            if (targets != null && targetEnt != null)
            {
                var tId = targets ?? 0;
                if (tId == 0) return false;

                var block = targetEnt as MyCubeBlock;

                MyEntity target;
                if (MyEntities.TryGetEntityById(tId, out target) && (target == targetEnt || block != null && target == block.CubeGrid))
                {
                    var worldVolume = target.PositionComp.WorldVolume;
                    var targetPos = worldVolume.Center;
                    var tRadius = worldVolume.Radius;
                    var maxRangeSqr = tRadius + w.MaxTargetDistance;
                    var minRangeSqr = tRadius + w.MinTargetDistance;

                    maxRangeSqr *= maxRangeSqr;
                    minRangeSqr *= minRangeSqr;
                    double rangeToTarget;
                    Vector3D.DistanceSquared(ref targetPos, ref w.MyPivotPos, out rangeToTarget);
                    
                    if (rangeToTarget <= maxRangeSqr && rangeToTarget >= minRangeSqr)
                    {
                        var overrides = w.Comp.Ai.RootFixedWeaponComp?.PrimaryWeapon?.MasterComp != null ? w.Comp.Ai.RootFixedWeaponComp.PrimaryWeapon.MasterComp.Data.Repo.Values.Set.Overrides : w.Comp.Data.Repo.Values.Set.Overrides;

                        if (overrides.FocusSubSystem && overrides.SubSystem != WeaponDefinition.TargetingDef.BlockTypes.Any && block != null && !w.ValidSubSystemTarget(block, overrides.SubSystem))
                            return false;

                        if (w.System.LockOnFocus)
                        {
                            var targetSphere = targetEnt.PositionComp.WorldVolume;
                            targetSphere.Center = targetEnt.PositionComp.WorldAABB.Center;
                            w.AimCone.ConeDir = w.MyPivotFwd;
                            w.AimCone.ConeTip = w.BarrelOrigin;
                            return MathFuncs.TargetSphereInCone(ref targetSphere, ref w.AimCone);
                        }
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
