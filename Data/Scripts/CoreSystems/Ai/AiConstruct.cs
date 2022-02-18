using System;
using System.Collections.Generic;
using System.Security.Policy;
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
            if (GridMap.GroupMap == null)
            {
                Log.Line($"GridGroup null");
                return;
            }
            foreach (var grid in GridMap.GroupMap.Construct.Keys) {

                SubGridCache.Add(grid);
                if (grid == TopEntity || SubGridsRegistered.ContainsKey(grid)) continue;
                RegisterSubGrid(grid);

            }

            foreach (var sub in SubGridsRegistered.Keys) {

                if (sub == TopEntity)
                    continue;

                if (!GridMap.GroupMap.Construct.ContainsKey(sub))
                    UnRegisterSubGrid(sub);
            }

            foreach (var map in Construct.LocalStatorMaps) {
                map.Value.Clear();
                Session.StatorMapListPool.Push(map.Value);
            }
            
            Construct.LocalStatorMaps.Clear();
            Construct.ControllingPlayers.Clear();
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

                if (battery != null || cube.HasInventory || stator != null)
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

                if (InventoryMonitor.ContainsKey(cube) || battery != null && Batteries.Contains(battery) || stator != null)
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
            internal readonly List<Ai> RefreshedAis = new List<Ai>();
            internal readonly Dictionary<MyStringHash, int> Counter = new Dictionary<MyStringHash, int>(MyStringHash.Comparer);
            internal readonly Focus Focus = new Focus();
            internal readonly ConstructData Data = new ConstructData();
            internal readonly Dictionary<long, PlayerController> ControllingPlayers = new Dictionary<long, PlayerController>();
            internal readonly HashSet<MyEntity> PreviousTargets = new HashSet<MyEntity>();
            internal readonly RunningAverage DamageAverage = new RunningAverage(10);
            internal readonly Dictionary<MyCubeGrid, List<StatorMap>> LocalStatorMaps = new Dictionary<MyCubeGrid, List<StatorMap>>();
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
            internal bool DroneAlert;

            internal double TotalEffect;
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
                        if (Ai.Session.GridToInfoMap.ContainsKey(grid)) {
                            var blockCount = Ai.Session.GridToInfoMap[grid].MostBlocks;
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
                        Log.Line($"1 - does this ever get hit?");

                        //Log.Line($"[rootAi is null in Update] - caller:{caller}, forcing rootAi to caller - inGridTarget:{ai.Session.EntityAIs.ContainsKey(ai.TopEntity)} -  myGridMarked:{ai.TopEntity.MarkedForClose} - aiMarked:{ai.MarkedForClose} - inScene:{ai.TopEntity.InScene} - lastClosed:{ai.AiCloseTick} - aiSpawned:{ai.AiSpawnTick} - diff:{ai.AiSpawnTick - ai.AiCloseTick} - sinceSpawn:{ai.Session.Tick - ai.AiSpawnTick} - entId:{ai.TopEntity.EntityId}");
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
                    foreach (var sub in Ai.SubGridCache)
                        RootAi.Session.EntityToMasterAi[sub] = RootAi;
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
                foreach (var sub in rootAi.SubGridCache)
                {
                    GridMap gridMap;
                    if (s.GridToInfoMap.TryGetValue(sub, out gridMap))
                    {

                        foreach (var c in gridMap.PlayerControllers)
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
                        s.DeferredPlayerLock[playerId] = 5;
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
                        s.DeferredPlayerLock[playerId] = 5;
                }
                return false;
            }


            internal void UpdateStators()
            {
                foreach (var p in Ai.TopStators)
                {
                    var grid = (MyCubeGrid)p.Key.CubeGrid;
                    if (!RootAi.Construct.LocalStatorMaps.ContainsKey(grid))
                    {
                        var list = RootAi.Session.StatorMapListPool.Count > 0 ? RootAi.Session.StatorMapListPool.Pop() : new List<StatorMap>();
                        RootAi.Construct.LocalStatorMaps.Add(grid, list);
                    }
                    RootAi.Construct.LocalStatorMaps[grid].Add(p.Value);
                }
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
                cAi.Construct.RefreshedAis.Clear();
                cAi.Construct.RefreshedAis.Add(cAi);

                if (cAi.SubGridCache.Count > 1) {
                    foreach (var sub in cAi.SubGridCache) {
                        if (sub == null || sub == cAi.TopEntity)
                            continue;

                        Ai subAi;
                        if (cAi.Session.EntityAIs.TryGetValue(sub, out subAi))
                            cAi.Construct.RefreshedAis.Add(subAi);
                    }
                }

                for (int i = 0; i < cAi.Construct.RefreshedAis.Count; i++) {

                    var checkAi = cAi.Construct.RefreshedAis[i];
                    checkAi.Construct.Counter.Clear();

                    for (int x = 0; x < cAi.Construct.RefreshedAis.Count; x++) {
                        foreach (var wc in cAi.Construct.RefreshedAis[x].PartCounting)
                            checkAi.Construct.AddWeaponCount(wc.Key, wc.Value.Current);
                    }
                }
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

            public static bool ConstructSynced(Ai ai)
            {
                var tempSet = new HashSet<MyCubeGrid>();

                foreach (var g in ai.GridMap.GroupMap.Construct)
                    tempSet.Add(g.Key);

                var sameSet = tempSet.SetEquals(ai.SubGridCache);

                if (!sameSet)
                    Log.Line($"not same set");

                var sameRoot = true;
                foreach (var sub in ai.SubGridCache)
                {
                    Ai subAi;
                    if (ai.Session.EntityAIs.TryGetValue(sub, out subAi))
                    {
                        if (subAi.Construct.RootAi != ai.Construct.RootAi)
                            sameRoot = false;
                    }

                    Ai rootAi;
                    if (ai.Session.EntityToMasterAi.TryGetValue(sub, out rootAi))
                    {
                        if (rootAi != ai.Construct.RootAi)
                            sameRoot = false;
                    }
                }
                if (!sameRoot)
                    Log.Line($"not same root");
                var sameSubs = true;
                foreach (var sub in ai.SubGridCache)
                {
                    Ai subAi;
                    if (ai.Session.EntityAIs.TryGetValue(sub, out subAi))
                    {
                        if (!ai.SubGridCache.SetEquals(subAi.SubGridCache) || !subAi.SubGridCache.Contains(ai.GridEntity))
                            sameSubs = false;
                    }
                }

                if (!sameSubs)
                    Log.Line($"subs not same");

                return sameSet && sameRoot && sameSubs;
            }

            public static int ConstructPlayerCount(Ai ai)
            {
                int playerCount = 0;
                foreach (var sub in ai.SubGridCache)
                {
                    GridMap gridMap;
                    if (ai.Session.GridToInfoMap.TryGetValue(sub, out gridMap))
                    {
                        playerCount += gridMap.PlayerControllers.Count;
                    }
                }

                return playerCount;
            }

            public bool ValidateStatorAssignemnts(IMyMotorStator stator)
            {
                foreach (var ai in Ai.GridMap.GroupMap.Ais)
                {
                    if (stator.TopGrid == ai.TopEntity)
                        return true;
                }

                return false;
            }

            public static bool MatchPlayerId(Ai ai, long playerId)
            {
                foreach (var sub in ai.SubGridCache)
                {
                    GridMap gridMap;
                    if (ai.Session.GridToInfoMap.TryGetValue(sub, out gridMap))
                    {
                        foreach (var c in gridMap.PlayerControllers)
                        {
                            if (c.Key == playerId)
                            {
                                Log.Line($"wasRoot: {sub == ai.Construct.RootAi.TopEntity} rootConstructHasPlayer:{ai.Construct.RootAi.Construct.ControllingPlayers.ContainsKey(playerId)} - {sub.EntityId}");
                                return true;
                            }
                        }
                    }
                }

                return false;
            }


            internal void Init(Ai ai)
            {
                RootAi = ai;
                Data.Init(ai);
            }
            
            internal void Clean()
            {
                Data.Clean();
                OptimalDps = 0;
                BlockCount = 0;
                AverageEffect = 0;
                TotalEffect = 0;
                PreviousTotalEffect = 0;
                LastRefreshTick = 0;
                TargetResetTick = 0;
                RootAi = null;
                LargestAi = null;
                Counter.Clear();
                RefreshedAis.Clear();
                PreviousTargets.Clear();
                ControllingPlayers.Clear();
                LocalStatorMaps.Clear();
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
                        var overrides = w.Comp.Data.Repo.Values.Set.Overrides;
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
