using System;
using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Projectiles;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using static CoreSystems.Support.CoreComponent.Trigger;
using static CoreSystems.Platform.CorePlatform.PlatformState;
using IMyProgrammableBlock = Sandbox.ModAPI.Ingame.IMyProgrammableBlock;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;

namespace CoreSystems.Api
{
    internal class ApiBackend
    {
        private readonly Session _session;
        internal readonly Dictionary<string, Delegate> ModApiMethods;
        internal Dictionary<string, Delegate> PbApiMethods;
        
        internal ApiBackend(Session session)
        {
            _session = session;

            ModApiMethods = new Dictionary<string, Delegate>
            {
                ["GetAllWeaponDefinitions"] = new Action<IList<byte[]>>(GetAllWeaponDefinitions),
                ["GetCoreWeapons"] = new Action<ICollection<MyDefinitionId>>(GetCoreWeapons),
                ["GetCoreStaticLaunchers"] = new Action<ICollection<MyDefinitionId>>(GetCoreStaticLaunchers),
                ["GetCoreTurrets"] = new Action<ICollection<MyDefinitionId>>(GetCoreTurrets),
                ["GetCorePhantoms"] = new Action<ICollection<MyDefinitionId>>(GetCorePhantoms),
                ["GetCoreRifles"] = new Action<ICollection<MyDefinitionId>>(GetCoreRifles),
                ["GetCoreArmors"] = new Action<IList<byte[]>>(GetCoreArmors),
                ["GetBlockWeaponMap"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, IDictionary<string, int>, bool>(GetBlockWeaponMap),
                ["GetProjectilesLockedOn"] = new Func<IMyEntity, MyTuple<bool, int, int>>(GetProjectilesLockedOn),
                ["GetSortedThreats"] = new Action<IMyEntity, ICollection<MyTuple<IMyEntity, float>>>(GetSortedThreats),
                ["GetObstructions"] = new Action<IMyEntity, ICollection<IMyEntity>>(GetObstructions),
                ["GetAiFocus"] = new Func<IMyEntity, int, IMyEntity>(GetAiFocus),
                ["SetAiFocus"] = new Func<IMyEntity, IMyEntity, int, bool>(SetAiFocus),
                ["GetWeaponTarget"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, int, MyTuple<bool, bool, bool, IMyEntity>>(GetWeaponTarget),
                ["SetWeaponTarget"] = new Action<Sandbox.ModAPI.IMyTerminalBlock, IMyEntity, int>(SetWeaponTarget),
                ["FireWeaponOnce"] = new Action<Sandbox.ModAPI.IMyTerminalBlock, bool, int>(FireWeaponBurst),
                ["ToggleWeaponFire"] = new Action<Sandbox.ModAPI.IMyTerminalBlock, bool, bool, int>(ToggleWeaponFire),
                ["IsWeaponReadyToFire"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, int, bool, bool, bool>(IsWeaponReadyToFire),
                ["GetMaxWeaponRange"] = new Func<Sandbox.ModAPI.IMyTerminalBlock,int, float>(GetMaxWeaponRange),
                ["GetTurretTargetTypes"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, ICollection<string>, int, bool>(GetTurretTargetTypes),
                ["SetTurretTargetTypes"] = new Action<Sandbox.ModAPI.IMyTerminalBlock, ICollection<string>, int>(SetTurretTargetTypes),
                ["SetBlockTrackingRange"] = new Action<Sandbox.ModAPI.IMyTerminalBlock, float>(SetBlockTrackingRange),
                ["IsTargetAligned"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, IMyEntity, int, bool>(IsTargetAligned),
                ["IsTargetAlignedExtended"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, IMyEntity, int, MyTuple<bool, Vector3D?>>(IsTargetAlignedExtended),
                ["CanShootTarget"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, IMyEntity, int, bool>(CanShootTarget),
                ["GetPredictedTargetPosition"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, IMyEntity, int, Vector3D?>(GetPredictedTargetPosition),
                ["GetHeatLevel"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, float>(GetHeatLevel),
                ["GetCurrentPower"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, float>(GetCurrentPower),
                ["GetMaxPower"] = new Func<MyDefinitionId, float>(GetMaxPower),
                ["DisableRequiredPower"] = new Action<Sandbox.ModAPI.IMyTerminalBlock>(ModOverride),
                ["HasGridAi"] = new Func<IMyEntity, bool>(HasGridAi),
                ["HasCoreWeapon"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, bool>(HasCoreWeapon),
                ["GetOptimalDps"] = new Func<IMyEntity, float>(GetOptimalDps),
                ["GetActiveAmmo"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, int, string>(GetActiveAmmo),
                ["SetActiveAmmo"] = new Action<Sandbox.ModAPI.IMyTerminalBlock, int, string>(SetActiveAmmo),
                ["RegisterProjectileAdded"] = new Action<Action<Vector3, float>>(RegisterProjectileAddedCallback),
                ["UnRegisterProjectileAdded"] = new Action<Action<Vector3, float>>(UnRegisterProjectileAddedCallback),
                ["UnMonitorProjectile"] = new Action<Sandbox.ModAPI.IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>>(UnMonitorProjectileCallbackLegacy),
                ["MonitorProjectile"] = new Action<Sandbox.ModAPI.IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>>(MonitorProjectileCallbackLegacy),
                ["RemoveMonitorProjectile"] = new Action<MyEntity, int, Action<long, int, ulong, long, Vector3D, bool>>(UnMonitorProjectileCallback),
                ["AddMonitorProjectile"] = new Action<MyEntity, int, Action<long, int, ulong, long, Vector3D, bool>>(MonitorProjectileCallback),
                ["GetProjectileState"] = new Func<ulong, MyTuple<Vector3D, Vector3D, float, float, long, string>>(GetProjectileState),
                ["GetConstructEffectiveDps"] = new Func<IMyEntity, float>(GetConstructEffectiveDps),
                ["GetPlayerController"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, long>(GetPlayerController),
                ["GetWeaponAzimuthMatrix"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, int, Matrix>(GetWeaponAzimuthMatrix),
                ["GetWeaponElevationMatrix"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, int, Matrix>(GetWeaponElevationMatrix),
                ["IsTargetValid"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, IMyEntity, bool, bool, bool>(IsTargetValid),
                ["GetWeaponScope"] = new Func<Sandbox.ModAPI.IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>>(GetWeaponScope),
                ["IsInRange"] = new Func<IMyEntity, MyTuple<bool, bool>>(IsInRange),

                // Phantoms
                ["GetTargetAssessment"] = new Func<MyEntity, MyEntity, int, bool, bool, MyTuple<bool, bool, Vector3D?>>(GetPhantomTargetAssessment),
                //["GetPhantomInfo"] = new Action<string, ICollection<MyTuple<MyEntity, long, int, float, uint, long>>>(GetPhantomInfo),
                ["SetTriggerState"] = new Action<MyEntity, int>(SetPhantomTriggerState),
                ["AddMagazines"] = new Action<MyEntity, int, long>(AddPhantomMagazines),
                ["SetAmmo"] = new Action<MyEntity, int, string>(SetPhantomAmmo),
                ["ClosePhantom"] = new Func<MyEntity, bool>(ClosePhantom),
                ["SetFocusTarget"] = new Func<MyEntity, MyEntity, int, bool>(SetPhantomFocusTarget),
                ["SpawnPhantom"] = new Func<string, uint, bool, long, string, int, float?, MyEntity, bool, bool, long, bool, MyEntity>(SpawnPhantom),
                ["ToggleDamageEvents"] = new Action<Dictionary<MyEntity, MyTuple<Vector3D, Dictionary<MyEntity, List<MyTuple<int, float, Vector3I>>>>>>(ToggleDamageEvents),
            };
        }


        internal void PbInit()
        {
            PbApiMethods = new Dictionary<string, Delegate>
            {
                ["GetCoreWeapons"] = new Action<ICollection<MyDefinitionId>>(GetCoreWeapons),
                ["GetCoreStaticLaunchers"] = new Action<ICollection<MyDefinitionId>>(GetCoreStaticLaunchers),
                ["GetCoreTurrets"] = new Action<ICollection<MyDefinitionId>>(GetCoreTurrets),
                ["GetBlockWeaponMap"] = new Func<IMyTerminalBlock, IDictionary<string, int>, bool>(PbGetBlockWeaponMap),
                ["GetProjectilesLockedOn"] = new Func<long, MyTuple<bool, int, int>>(PbGetProjectilesLockedOn),
                ["GetSortedThreats"] = new Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>>(PbGetSortedThreats),
                ["GetObstructions"] = new Action<IMyTerminalBlock, ICollection<MyDetectedEntityInfo>>(PbGetObstructions),
                ["GetAiFocus"] = new Func<long, int, MyDetectedEntityInfo>(PbGetAiFocus),
                ["SetAiFocus"] = new Func<IMyTerminalBlock, long, int, bool>(PbSetAiFocus),
                ["GetWeaponTarget"] = new Func<IMyTerminalBlock, int, MyDetectedEntityInfo>(PbGetWeaponTarget),
                ["SetWeaponTarget"] = new Action<IMyTerminalBlock, long, int>(PbSetWeaponTarget),
                ["FireWeaponOnce"] = new Action<IMyTerminalBlock, bool, int>(PbFireWeaponBurst),
                ["ToggleWeaponFire"] = new Action<IMyTerminalBlock, bool, bool, int>(PbToggleWeaponFire),
                ["IsWeaponReadyToFire"] = new Func<IMyTerminalBlock, int, bool, bool, bool>(PbIsWeaponReadyToFire),
                ["GetMaxWeaponRange"] = new Func<IMyTerminalBlock, int, float>(PbGetMaxWeaponRange),
                ["GetTurretTargetTypes"] = new Func<IMyTerminalBlock, ICollection<string>, int, bool>(PbGetTurretTargetTypes),
                ["SetTurretTargetTypes"] = new Action<IMyTerminalBlock, ICollection<string>, int>(PbSetTurretTargetTypes),
                ["SetBlockTrackingRange"] = new Action<IMyTerminalBlock, float>(PbSetBlockTrackingRange),
                ["IsTargetAligned"] = new Func<IMyTerminalBlock, long, int, bool>(PbIsTargetAligned),
                ["IsTargetAlignedExtended"] = new Func<IMyTerminalBlock, long, int, MyTuple<bool, Vector3D?>>(PbIsTargetAlignedExtended),
                ["CanShootTarget"] = new Func<IMyTerminalBlock, long, int, bool>(PbCanShootTarget),
                ["GetPredictedTargetPosition"] = new Func<IMyTerminalBlock, long, int, Vector3D?>(PbGetPredictedTargetPosition),
                ["GetHeatLevel"] = new Func<IMyTerminalBlock, float>(PbGetHeatLevel),
                ["GetCurrentPower"] = new Func<IMyTerminalBlock, float>(PbGetCurrentPower),
                ["GetMaxPower"] = new Func<MyDefinitionId, float>(GetMaxPower),
                ["HasGridAi"] = new Func<long, bool>(PbHasGridAi),
                ["HasCoreWeapon"] = new Func<IMyTerminalBlock, bool>(PbHasCoreWeapon),
                ["GetOptimalDps"] = new Func<long, float>(PbGetOptimalDps),
                ["GetActiveAmmo"] = new Func<IMyTerminalBlock, int, string>(PbGetActiveAmmo),
                ["SetActiveAmmo"] = new Action<IMyTerminalBlock, int, string>(PbSetActiveAmmo),
                ["RegisterProjectileAdded"] = new Action<Action<Vector3, float>>(RegisterProjectileAddedCallback),
                ["UnRegisterProjectileAdded"] = new Action<Action<Vector3, float>>(UnRegisterProjectileAddedCallback),
                ["UnMonitorProjectile"] = new Action<IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>>(PbUnMonitorProjectileCallback),
                ["MonitorProjectile"] = new Action<IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>>(PbMonitorProjectileCallback),
                ["GetProjectileState"] = new Func<ulong, MyTuple<Vector3D, Vector3D, float, float, long, string>>(GetProjectileState),
                ["GetConstructEffectiveDps"] = new Func<long, float>(PbGetConstructEffectiveDps),
                ["GetPlayerController"] = new Func<IMyTerminalBlock, long>(PbGetPlayerController),
                ["GetWeaponAzimuthMatrix"] = new Func<IMyTerminalBlock, int, Matrix>(PbGetWeaponAzimuthMatrix),
                ["GetWeaponElevationMatrix"] = new Func<IMyTerminalBlock, int, Matrix>(PbGetWeaponElevationMatrix),
                ["IsTargetValid"] = new Func<IMyTerminalBlock, long, bool, bool, bool>(PbIsTargetValid),
                ["GetWeaponScope"] = new Func<IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>>(PbGetWeaponScope),
                ["IsInRange"] = new Func<IMyTerminalBlock, MyTuple<bool, bool>>(PbIsInRange),
            };
            var pb = MyAPIGateway.TerminalControls.CreateProperty<IReadOnlyDictionary<string, Delegate>, Sandbox.ModAPI.IMyTerminalBlock>("WcPbAPI");
            pb.Getter = b => PbApiMethods;
            MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(pb);
            _session.PbApiInited = true;
        }

        private void GetObstructions(IMyEntity shooter, ICollection<IMyEntity> collection)
        {
            var grid = shooter?.GetTopMostParent() as MyCubeGrid;
            Ai gridAi;
            if (grid != null && collection != null && _session.EntityAIs.TryGetValue(grid, out gridAi))
            {
                for (int i = 0; i < gridAi.Obstructions.Count; i++)
                    collection.Add(gridAi.Obstructions[i]);
            }
        }

        private readonly ICollection<IMyEntity> _tmpPbGetObstructions = new List<IMyEntity>();
        private void PbGetObstructions(IMyTerminalBlock shooter, ICollection<MyDetectedEntityInfo> collection)
        {
            if (shooter != null && collection != null)
            {
                collection.Clear();
                GetObstructions((IMyEntity)shooter, _tmpPbGetObstructions);
                foreach (var i in _tmpPbGetObstructions)
                    collection.Add(GetDetailedEntityInfo(new MyTuple<bool, bool, bool, IMyEntity>(true, false, false, i), (MyEntity)shooter));

                _tmpPbGetObstructions.Clear();
            }
        }

        private float PbGetConstructEffectiveDps(long arg)
        {
            return GetConstructEffectiveDps(MyEntities.GetEntityById(arg));
        }

        private void PbSetActiveAmmo(object arg1, int arg2, string arg3)
        {
            SetActiveAmmo((Sandbox.ModAPI.IMyTerminalBlock) arg1, arg2, arg3);
        }

        private string PbGetActiveAmmo(object arg1, int arg2)
        {
            return GetActiveAmmo((Sandbox.ModAPI.IMyTerminalBlock) arg1, arg2);
        }

        private float PbGetOptimalDps(long arg)
        {
            return GetOptimalDps(MyEntities.GetEntityById(arg));
        }

        private bool PbHasCoreWeapon(object arg)
        {
            return HasCoreWeapon((Sandbox.ModAPI.IMyTerminalBlock) arg);
        }

        private bool PbHasGridAi(long arg)
        {
            return HasGridAi(MyEntities.GetEntityById(arg));
        }

        private float PbGetCurrentPower(object arg)
        {
            return GetCurrentPower((Sandbox.ModAPI.IMyTerminalBlock) arg);
        }

        private float PbGetHeatLevel(object arg)
        {
            return GetHeatLevel((Sandbox.ModAPI.IMyTerminalBlock) arg);
        }

        private Vector3D? PbGetPredictedTargetPosition(object arg1, long arg2, int arg3)
        {
            var block = arg1 as IMyTerminalBlock;
            var target = MyEntities.GetEntityById(arg2);
            Ai ai;
            if (block != null && target != null && _session.EntityToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey(target))
                return null;

            return GetPredictedTargetPosition((Sandbox.ModAPI.IMyTerminalBlock) block, target, arg3);
        }

        private bool PbCanShootTarget(object arg1, long arg2, int arg3)
        {
            var block = arg1 as IMyTerminalBlock;
            var target = MyEntities.GetEntityById(arg2);
            Ai ai;
            if (block != null && target != null && _session.EntityToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey(target))
                return false;

            return CanShootTarget((Sandbox.ModAPI.IMyTerminalBlock) block, target, arg3);
        }

        private bool PbIsTargetAligned(object arg1, long arg2, int arg3)
        {
            var block = arg1 as IMyTerminalBlock;
            var target = MyEntities.GetEntityById(arg2);
            Ai ai;
            if (block != null && target != null && _session.EntityToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey(target))
                return false;

            return IsTargetAligned((Sandbox.ModAPI.IMyTerminalBlock) block, target, arg3);
        }

        private MyTuple<bool, Vector3D?> PbIsTargetAlignedExtended(object arg1, long arg2, int arg3)
        {
            var block = arg1 as IMyTerminalBlock;
            var target = MyEntities.GetEntityById(arg2);
            Ai ai;
            if (block != null && target != null && _session.EntityToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey(target))
                return new MyTuple<bool, Vector3D?>();

            return IsTargetAlignedExtended((Sandbox.ModAPI.IMyTerminalBlock) block, target, arg3);
        }

        private void PbSetBlockTrackingRange(object arg1, float arg2)
        {
            SetBlockTrackingRange((Sandbox.ModAPI.IMyTerminalBlock) arg1, arg2);
        }

        private void PbSetTurretTargetTypes(object arg1, object arg2, int arg3)
        {
            SetTurretTargetTypes((Sandbox.ModAPI.IMyTerminalBlock) arg1, (ICollection<string>) arg2, arg3);
        }

        private bool PbGetTurretTargetTypes(object arg1, object arg2, int arg3)
        {
            return GetTurretTargetTypes((Sandbox.ModAPI.IMyTerminalBlock) arg1, (ICollection<string>) arg2, arg3);
        }

        private float PbGetMaxWeaponRange(object arg1, int arg2)
        {
            return GetMaxWeaponRange((Sandbox.ModAPI.IMyTerminalBlock) arg1, arg2);
        }

        private bool PbIsWeaponReadyToFire(object arg1, int arg2, bool arg3, bool arg4)
        {
            return IsWeaponReadyToFire((Sandbox.ModAPI.IMyTerminalBlock) arg1, arg2, arg3, arg4);
        }

        private void PbToggleWeaponFire(object arg1, bool arg2, bool arg3, int arg4)
        {
            ToggleWeaponFire((Sandbox.ModAPI.IMyTerminalBlock) arg1, arg2, arg3, arg4);
        }

        private void PbFireWeaponBurst(object arg1, bool arg2, int arg3)
        {
            FireWeaponBurst((Sandbox.ModAPI.IMyTerminalBlock) arg1, arg2, arg3);
        }

        private void PbSetWeaponTarget(object arg1, long arg2, int arg3)
        {
            SetWeaponTarget((Sandbox.ModAPI.IMyTerminalBlock) arg1, MyEntities.GetEntityById(arg2), arg3);
        }

        private Sandbox.ModAPI.Ingame.MyDetectedEntityInfo PbGetWeaponTarget(object arg1, int arg2)
        {
            var block = arg1 as IMyTerminalBlock;
            var target = GetWeaponTarget((Sandbox.ModAPI.IMyTerminalBlock) block, arg2);

            Ai ai;
            if (block != null && target.Item4 != null && _session.EntityToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey((MyEntity)target.Item4))
                return new MyDetectedEntityInfo();

            var result = GetDetailedEntityInfo(target, (MyEntity)arg1);

            return result;
        }

        private bool PbSetAiFocus(object arg1, long arg2, int arg3)
        {
            return SetAiFocus((IMyEntity)arg1, MyEntities.GetEntityById(arg2), arg3);
        }

        private MyDetectedEntityInfo PbGetAiFocus(long arg1, int arg2)
        {
            var shooter = MyEntities.GetEntityById(arg1);
            return GetEntityInfo(GetAiFocus(shooter, arg2), shooter);
        }

        private MyDetectedEntityInfo GetDetailedEntityInfo(MyTuple<bool, bool, bool, IMyEntity> target, MyEntity shooter)
        {
            var e = target.Item4;
            var shooterGrid = shooter.GetTopMostParent() as MyCubeGrid;
            var topTarget = e?.GetTopMostParent() as MyEntity;
            var block = e as Sandbox.ModAPI.IMyTerminalBlock;
            var player = e as IMyCharacter;
            long entityId = 0;
            var relation = MyRelationsBetweenPlayerAndBlock.NoOwnership;
            var type = MyDetectedEntityType.Unknown;
            var name = string.Empty;

            Ai ai;
            Ai.TargetInfo info = null;

            if (shooterGrid != null && topTarget != null && _session.EntityToMasterAi.TryGetValue(shooterGrid, out ai) && ai.Targets.TryGetValue(topTarget, out info)) {
                relation = info.EntInfo.Relationship;
                type = info.EntInfo.Type;
                var maxDist = ai.MaxTargetingRange + shooterGrid.PositionComp.WorldAABB.Extents.Max();
                if (Vector3D.DistanceSquared(e.PositionComp.WorldMatrixRef.Translation, shooterGrid.PositionComp.WorldMatrixRef.Translation) > (maxDist * maxDist))
                {
                    return new MyDetectedEntityInfo();
                }
            }

            if (!target.Item1 || e == null || topTarget?.Physics == null) {
                var projectile = target.Item2;
                var fake = target.Item3;
                if (fake) {
                    name = "ManualTargeting";
                    type = MyDetectedEntityType.None;
                    entityId = -2;
                }
                else if (projectile) {
                    name = "Projectile";
                    type = MyDetectedEntityType.Missile;
                    entityId = -1;
                }
                return new MyDetectedEntityInfo(entityId, name, type, info?.TargetPos, MatrixD.Zero, info != null ? (Vector3)info.Velocity : Vector3.Zero, relation, BoundingBoxD.CreateInvalid(), _session.Tick);
            }
            entityId = e.EntityId;
            var grid = topTarget as MyCubeGrid;
            if (grid != null) name = block != null ? block.CustomName : grid.DisplayName;
            else if (player != null) name = player.GetFriendlyName();
            else name = e.GetFriendlyName();

            return new MyDetectedEntityInfo(entityId, name, type, e.PositionComp.WorldAABB.Center, e.PositionComp.WorldMatrixRef, topTarget.Physics.LinearVelocity, relation, e.PositionComp.WorldAABB, _session.Tick);
        }

        private MyDetectedEntityInfo GetEntityInfo(IMyEntity target, MyEntity shooter)
        {
            var e = target;
            if (e?.Physics == null)
                return new MyDetectedEntityInfo();

            var shooterGrid = shooter.GetTopMostParent() as MyCubeGrid;

            Ai ai;
            if (shooterGrid != null && _session.EntityToMasterAi.TryGetValue(shooterGrid, out ai))
            {
                var maxDist = ai.MaxTargetingRangeSqr + target.PositionComp.WorldAABB.Extents.Max();
                if (Vector3D.DistanceSquared(target.PositionComp.WorldMatrixRef.Translation, shooterGrid.PositionComp.WorldMatrixRef.Translation) > (maxDist * maxDist))
                {
                    return new MyDetectedEntityInfo();
                }
            }

            var grid = e.GetTopMostParent() as MyCubeGrid;
            var block = e as Sandbox.ModAPI.IMyTerminalBlock;
            var player = e as IMyCharacter;

            string name;
            MyDetectedEntityType type;
            var relation = MyRelationsBetweenPlayerAndBlock.Enemies;

            if (grid != null) {
                name = block != null ? block.CustomName : grid.DisplayName;
                type = grid.GridSizeEnum == MyCubeSize.Large ? MyDetectedEntityType.LargeGrid : MyDetectedEntityType.SmallGrid;
            }
            else if (player != null) {
                type = MyDetectedEntityType.CharacterOther;
                name = player.GetFriendlyName();

            }
            else {
                type = MyDetectedEntityType.Unknown;
                name = e.GetFriendlyName();
            }
            return new MyDetectedEntityInfo(e.EntityId, name, type, e.PositionComp.WorldAABB.Center, e.PositionComp.WorldMatrixRef, e.Physics.LinearVelocity, relation, e.PositionComp.WorldAABB, _session.Tick);
        }

        private readonly List<MyTuple<IMyEntity, float>> _tmpTargetList = new List<MyTuple<IMyEntity, float>>();
        private void PbGetSortedThreats(object arg1, object arg2)
        {
            var shooter = (Sandbox.ModAPI.IMyTerminalBlock)arg1;
            GetSortedThreats(shooter, _tmpTargetList);
            
            var dict = (IDictionary<MyDetectedEntityInfo, float>) arg2;
            
            foreach (var i in _tmpTargetList)
                dict[GetDetailedEntityInfo(new MyTuple<bool, bool, bool, IMyEntity>(true, false, false , i.Item1), (MyEntity)shooter)] = i.Item2;

            _tmpTargetList.Clear();

        }

        private MyTuple<bool, int, int> PbGetProjectilesLockedOn(long arg)
        {
            return GetProjectilesLockedOn(MyEntities.GetEntityById(arg));
        }

        private bool PbGetBlockWeaponMap(object arg1, object arg2)
        {
            return GetBlockWeaponMap((Sandbox.ModAPI.IMyTerminalBlock) arg1, (IDictionary<string, int>)arg2);
        }

        private long PbGetPlayerController(object arg1)
        {
            return GetPlayerController((Sandbox.ModAPI.IMyTerminalBlock)arg1);
        }

        private Matrix PbGetWeaponAzimuthMatrix(IMyTerminalBlock arg1, int arg2)
        {
            return GetWeaponAzimuthMatrix((Sandbox.ModAPI.IMyTerminalBlock)arg1, arg2);
        }

        private Matrix PbGetWeaponElevationMatrix(IMyTerminalBlock arg1, int arg2)
        {
            return GetWeaponElevationMatrix((Sandbox.ModAPI.IMyTerminalBlock)arg1, arg2);
        }

        private bool PbIsTargetValid(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, long arg2, bool arg3, bool arg4)
        {

            var block = arg1 as IMyTerminalBlock;
            var target = MyEntities.GetEntityById(arg2);
            Ai ai;
            if (block != null && target != null && _session.EntityToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey(target))
                return false;

            return IsTargetValid((Sandbox.ModAPI.IMyTerminalBlock) block, target, arg3, arg4);
        }

        private MyTuple<Vector3D, Vector3D> PbGetWeaponScope(IMyTerminalBlock arg1, int arg2)
        {
            return GetWeaponScope((Sandbox.ModAPI.IMyTerminalBlock)arg1, arg2);
        }

        // Block EntityId, PartId, ProjectileId, LastHitId, LastPos, Start 
        internal static void PbMonitorProjectileCallback(IMyTerminalBlock weaponBlock, int weaponId, Action<long, int, ulong, long, Vector3D, bool> callback)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>();
            if (comp?.Platform != null && comp.Platform.Weapons.Count > weaponId)
                comp.Monitors[weaponId].Add(callback);
        }

        // Block EntityId, PartId, ProjectileId, LastHitId, LastPos, Start 
        internal static void PbUnMonitorProjectileCallback(IMyTerminalBlock weaponBlock, int weaponId, Action<long, int, ulong, long, Vector3D, bool> callback)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>();
            if (comp?.Platform != null && comp.Platform.Weapons.Count > weaponId)
                comp.Monitors[weaponId].Remove(callback);
        }

        // terminalBlock, Threat, Other, Something 
        private MyTuple<bool, bool> PbIsInRange(object arg1)
        {
            var tBlock = arg1 as Sandbox.ModAPI.IMyTerminalBlock;
            
            return tBlock != null ? IsInRange(MyEntities.GetEntityById(tBlock.EntityId)) : new MyTuple<bool, bool>();
        }
        
        // Non-PB Methods
        private void GetAllWeaponDefinitions(IList<byte[]> collection)
        {
            foreach (var wepDef in _session.WeaponDefinitions)
                collection.Add(MyAPIGateway.Utilities.SerializeToBinary(wepDef));
        }

        private void GetCoreWeapons(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.CoreSystemsDefs.Values)
                collection.Add(def);
        }

        private void GetCoreStaticLaunchers(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.CoreSystemsFixedBlockDefs)
                collection.Add(def);
        }

        private void GetCorePhantoms(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.CoreSystemsPhantomDefs)
                collection.Add(def);
        }

        private void GetCoreRifles(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.CoreSystemsRifleDefs)
                collection.Add(def);
        }

        private void GetCoreArmors(IList<byte[]> collection)
        {
            foreach (var def in _session.CoreSystemsArmorDefs)
                collection.Add(MyAPIGateway.Utilities.SerializeToBinary(def));
        }
        private void GetCoreTurrets(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.CoreSystemsTurretBlockDefs)
                collection.Add(def);
        }

        internal long GetPlayerController(Sandbox.ModAPI.IMyTerminalBlock weaponBlock)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready)
                return comp.Data.Repo.Values.State.PlayerId;

            return -1;
        }

        internal Matrix GetWeaponAzimuthMatrix(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready)
            {
                var weapon = comp.Platform.Weapons[weaponId];
                var matrix = weapon.AzimuthPart?.Entity?.PositionComp.LocalMatrixRef ?? Matrix.Zero;
                return matrix;
            }

            return Matrix.Zero;
        }

        internal Matrix GetWeaponElevationMatrix(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready)
            {
                var weapon = comp.Platform.Weapons[weaponId];
                var matrix = weapon.ElevationPart?.Entity?.PositionComp.LocalMatrixRef ?? Matrix.Zero;
                return matrix;
            }

            return Matrix.Zero;
        }

        private bool GetBlockWeaponMap(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, IDictionary<string, int> collection)
        {
            CoreStructure coreStructure;
            if (_session.PartPlatforms.TryGetValue(weaponBlock.SlimBlock.BlockDefinition.Id, out coreStructure) && (coreStructure is WeaponStructure))
            {
                foreach (var weaponSystem in coreStructure.PartSystems.Values)
                {
                    var system = weaponSystem;
                    if (!collection.ContainsKey(system.PartName))
                        collection.Add(system.PartName, ((WeaponSystem)system).WeaponId);
                }
                return true;
            }
            return false;
        }

        private MyTuple<bool, int, int> GetProjectilesLockedOn(IMyEntity victim)
        {
            var grid = victim.GetTopMostParent() as MyCubeGrid;
            Ai ai;
            MyTuple<bool, int, int> tuple;
            if (grid != null && _session.EntityAIs.TryGetValue(grid, out ai))
            {
                var count = ai.LiveProjectile.Count;
                tuple = count > 0 ? new MyTuple<bool, int, int>(true, count, (int) (_session.Tick - ai.LiveProjectileTick)) : new MyTuple<bool, int, int>(false, 0, -1);
            }
            else tuple = new MyTuple<bool, int, int>(false, 0, -1);
            return tuple;
        }

        private void GetSortedThreats(IMyEntity shooter, ICollection<MyTuple<IMyEntity, float>> collection)
        {
            var grid = shooter.GetTopMostParent() as MyCubeGrid;
            Ai ai;
            if (grid != null && _session.EntityAIs.TryGetValue(grid, out ai))
            {
                for (int i = 0; i < ai.SortedTargets.Count; i++)
                {
                    var targetInfo = ai.SortedTargets[i];
                    collection.Add(new MyTuple<IMyEntity, float>(targetInfo.Target, targetInfo.OffenseRating));
                }
            }
        }

        private IMyEntity GetAiFocus(IMyEntity shooter, int priority = 0)
        {
            var shootingGrid = shooter.GetTopMostParent() as MyCubeGrid;

            if (shootingGrid != null)
            {
                Ai ai;
                if (_session.EntityToMasterAi.TryGetValue(shootingGrid, out ai))
                    return MyEntities.GetEntityById(ai.Construct.Data.Repo.FocusData.Target);
            }
            return null;
        }

        private bool SetAiFocus(IMyEntity shooter, IMyEntity target, int priority = 0)
        {
            var shootingGrid = shooter.GetTopMostParent() as MyCubeGrid;

            if (shootingGrid != null)
            {
                Ai ai;
                if (_session.EntityToMasterAi.TryGetValue(shootingGrid, out ai))
                {
                    if (!ai.Session.IsServer)
                        return false;

                    ai.Construct.Focus.ReassignTarget((MyEntity)target, ai);
                    return true;
                }
            }
            return false;
        }

        private static MyTuple<bool, bool, bool, IMyEntity> GetWeaponTarget(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {
                var weapon = comp.Platform.Weapons[weaponId];
                if (weapon.Target.TargetState == Target.TargetStates.IsFake)
                    return new MyTuple<bool, bool, bool, IMyEntity>(true, false, true, null);
                if (weapon.Target.TargetState == Target.TargetStates.IsProjectile)
                    return new MyTuple<bool, bool, bool, IMyEntity>(true, true, false, null);
                return new MyTuple<bool, bool, bool, IMyEntity>(weapon.Target.TargetState == Target.TargetStates.IsEntity, false, false, weapon.Target.TargetEntity);
            }

            return new MyTuple<bool, bool, bool, IMyEntity>(false, false, false, null);
        }


        private static void SetWeaponTarget(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, IMyEntity target, int weaponId = 0)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
                Ai.AcquireTarget(comp.Platform.Weapons[weaponId], false, (MyEntity)target);
        }

        private static void FireWeaponBurst(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, bool allWeapons = true, int weaponId = 0)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {
                comp.ShootManager.RequestShootSync(comp.Session.PlayerId, Weapon.ShootManager.RequestType.Once, Weapon.ShootManager.Signals.Once);
            }
        }

        private static void ToggleWeaponFire(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, bool on, bool allWeapons = true, int weaponId = 0)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {

                comp.ShootManager.RequestShootSync(0, on ? Weapon.ShootManager.RequestType.On : Weapon.ShootManager.RequestType.Off, Weapon.ShootManager.Signals.On);
            }
        }

        private static bool IsWeaponReadyToFire(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, int weaponId = 0, bool anyWeaponReady = true, bool shotReady = false)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId && comp.IsWorking)
            {
                for (int i = 0; i < comp.Platform.Weapons.Count; i++)
                {
                    if (!anyWeaponReady && i != weaponId) continue;
                    var w = comp.Platform.Weapons[i];
                    if (w.ShotReady) return true;
                }
            }

            return false;
        }

        private static float GetMaxWeaponRange(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
                return (float)comp.Platform.Weapons[weaponId].MaxTargetDistance;

            return 0f;
        }

        private static bool GetTurretTargetTypes(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, ICollection<string> collection, int weaponId = 0)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {
                var weapon = comp.Platform.Weapons[weaponId];
                var threats = weapon.System.Values.Targeting.Threats;
                for (int i = 0; i < threats.Length; i++) collection.Add(threats[i].ToString());
                return true;
            }
            return false;
        }

        private static void SetTurretTargetTypes(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, ICollection<string> collection, int weaponId = 0)
        {

        }

        private static void SetBlockTrackingRange(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, float range)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready)
            {
                double maxBlockRange = 0d;
                for (int i = 0; i < comp.Platform.Weapons.Count; i++) {
                    var w = comp.Platform.Weapons[i];
                    
                    if (w.ActiveAmmoDef == null)
                        return;

                    var hardPointMax = w.System.WConst.MaxTargetDistance;
                    var ammoMax = w.ActiveAmmoDef.AmmoDef.Const.MaxTrajectory;
                    var weaponRange = Math.Min(hardPointMax, ammoMax);

                    if (weaponRange > maxBlockRange)
                        maxBlockRange = weaponRange;
                }
                range = (float)(maxBlockRange > range ? range : maxBlockRange);

                BlockUi.RequestSetRange(weaponBlock, range);
            }
        }

        private static bool IsTargetAligned(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];

                w.NewTarget.TargetEntity = (MyEntity) targetEnt;
                var dist = Vector3D.DistanceSquared(comp.CoreEntity.PositionComp.WorldMatrixRef.Translation, targetEnt.PositionComp.WorldMatrixRef.Translation);
                if (dist > w.MaxTargetDistanceSqr)
                {
                    return false;
                }

                Vector3D targetPos;
                return Weapon.TargetAligned(w, w.NewTarget, out targetPos);
            }
            return false;
        }

        private static MyTuple<bool, Vector3D?> IsTargetAlignedExtended(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];

                w.NewTarget.TargetEntity = (MyEntity)targetEnt;

                Vector3D targetPos;
                var targetAligned = Weapon.TargetAligned(w, w.NewTarget, out targetPos);
                
                return new MyTuple<bool, Vector3D?>(targetAligned, targetAligned ? targetPos : (Vector3D?)null);
            }
            return new MyTuple<bool, Vector3D?>(false, null);
        }

        private static bool CanShootTarget(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {

                var w = comp.Platform.Weapons[weaponId];
                var dist = Vector3D.DistanceSquared(comp.CoreEntity.PositionComp.WorldMatrixRef.Translation, targetEnt.PositionComp.WorldMatrixRef.Translation);
                
                if (dist > w.MaxTargetDistanceSqr)
                {
                    return false;
                }

                var topMost = targetEnt.GetTopMostParent();
                var targetVel = topMost.Physics?.LinearVelocity ?? Vector3.Zero;
                var targetAccel = topMost.Physics?.AngularAcceleration ?? Vector3.Zero;
                Vector3D predictedPos;
                return Weapon.CanShootTargetObb(w, (MyEntity)targetEnt, targetVel, targetAccel, out predictedPos);
            }
            return false;
        }

        private static Vector3D? GetPredictedTargetPosition(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];
                w.NewTarget.TargetEntity = (MyEntity)targetEnt;

                Vector3D targetPos;
                Weapon.TargetAligned(w, w.NewTarget, out targetPos);
                return targetPos;
            }
            return null;
        }

        private static float GetHeatLevel(Sandbox.ModAPI.IMyTerminalBlock weaponBlock)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.MaxHeat > 0)
            {
                return comp.CurrentHeat;
            }
            return 0f;
        }

        private static float GetCurrentPower(Sandbox.ModAPI.IMyTerminalBlock weaponBlock)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready)
                return comp.SinkPower;

            return 0f;
        }

        private float GetMaxPower(MyDefinitionId weaponDef)
        {
            return 0f; //Need to implement
        }

        private static void ModOverride(Sandbox.ModAPI.IMyTerminalBlock weaponBlock)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready)
            {
                comp.ModOverride = true;
                if (comp.Ai != null)
                    comp.Ai.ModOverride = true;
            }
        }

        private bool HasGridAi(IMyEntity entity)
        {
            var grid = entity?.GetTopMostParent() as MyCubeGrid;

            return grid != null && _session.EntityAIs.ContainsKey(grid);
        }

        private static bool HasCoreWeapon(Sandbox.ModAPI.IMyTerminalBlock weaponBlock)
        {
            return weaponBlock.Components.Has<CoreComponent>();
        }

        private float GetOptimalDps(IMyEntity entity)
        {
            var weaponBlock = entity as Sandbox.ModAPI.IMyTerminalBlock;
            if (weaponBlock != null)
            {
                var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
                if (comp?.Platform != null && comp.Platform.State == Ready)
                    return comp.PeakDps;
            }
            else
            {
                var grid = entity.GetTopMostParent() as MyCubeGrid;
                Ai ai;
                if (grid != null && _session.EntityAIs.TryGetValue(grid, out ai))
                    return ai.OptimalDps;
            }
            return 0f;
        }

        private static string GetActiveAmmo(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, int weaponId)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
                return comp.Platform.Weapons[weaponId].ActiveAmmoDef.AmmoDef.AmmoRound;

            return null;
        }

        private static void SetActiveAmmo(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, int weaponId, string ammoTypeStr)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Session.IsServer && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];
                for (int i = 0; i < w.System.AmmoTypes.Length; i++)
                {
                    var ammoType = w.System.AmmoTypes[i];
                    if (ammoType.AmmoName == ammoTypeStr && ammoType.AmmoDef.Const.IsTurretSelectable)
                    {
                        if (comp.Session.IsServer) {
                            w.Reload.AmmoTypeId = i;
                            if (comp.Session.MpActive)
                                comp.Session.SendWeaponReload(w);
                        }


                        break;
                    }
                }
            }
        }

        private void RegisterProjectileAddedCallback(Action<Vector3, float> callback)
        {
            _session.ProjectileAddedCallback += callback;
        }

        private void UnRegisterProjectileAddedCallback(Action<Vector3, float> callback)
        {
            try
            {
                _session.ProjectileAddedCallback -= callback;
            }
            catch (Exception e)
            {
                Log.Line($"Cannot remove Action, Action is not registered: {e}");
            }
        }

        // Block EntityId, PartId, ProjectileId, LastHitId, LastPos, Start 
        internal static void MonitorProjectileCallbackLegacy(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, int weaponId, Action<long, int, ulong, long, Vector3D, bool> callback)
        {
            MonitorProjectileCallback(weaponBlock as MyEntity, weaponId, callback);
        }

        // Block EntityId, PartId, ProjectileId, LastHitId, LastPos, Start 
        internal static void UnMonitorProjectileCallbackLegacy(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, int weaponId, Action<long, int, ulong, long, Vector3D, bool> callback)
        {
            UnMonitorProjectileCallback(weaponBlock as MyEntity, weaponId, callback);
        }

        // Block EntityId, PartId, ProjectileId, LastHitId, LastPos, Start 
        internal static void MonitorProjectileCallback(MyEntity entity, int weaponId, Action<long, int, ulong, long, Vector3D, bool> callback)
        {
            var comp = entity.Components.Get<CoreComponent>();
            if (comp?.Platform != null && comp.Platform.Weapons.Count > weaponId)
                comp.Monitors[weaponId]?.Add(callback);
        }

        // Block EntityId, PartId, ProjectileId, LastHitId, LastPos, Start 
        internal static void UnMonitorProjectileCallback(MyEntity entity, int weaponId, Action<long, int, ulong, long, Vector3D, bool> callback)
        {
            var comp = entity.Components.Get<CoreComponent>();
            if (comp?.Platform != null && comp.Platform.Weapons.Count > weaponId)
                comp.Monitors[weaponId]?.Remove(callback);
        }

        // POs, Dir, baseDamageLeft, HealthLeft, TargetEntityId, AmmoName 
        private MyTuple<Vector3D, Vector3D, float, float, long, string> GetProjectileState(ulong projectileId)
        {
            Projectile p;
            if (_session.MonitoredProjectiles.TryGetValue(projectileId, out p))
                return new MyTuple<Vector3D, Vector3D, float, float, long, string>(p.Position, p.Info.Direction, p.Info.BaseDamagePool, p.Info.BaseHealthPool, p.Info.Target.TargetId, p.Info.AmmoDef.AmmoRound);

            return new MyTuple<Vector3D, Vector3D, float, float, long, string>();
        }

        private float GetConstructEffectiveDps(IMyEntity entity)
        {
            var grid = entity.GetTopMostParent() as MyCubeGrid;
            Ai ai;
            if (grid != null && _session.EntityToMasterAi.TryGetValue(grid, out ai))
                return ai.EffectiveDps;

            return 0;
        }
        
        private bool IsTargetValid(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, IMyEntity targetEntity, bool onlyThreats, bool checkRelations)
        {

            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready) {
                
                var ai = comp.Ai;
                
                Ai.TargetInfo targetInfo;
                if (ai.Targets.TryGetValue((MyEntity)targetEntity, out targetInfo)) {
                    var marked = targetInfo.Target?.MarkedForClose;
                    if (!marked.HasValue || marked.Value)
                        return false;
                    
                    if (!onlyThreats && !checkRelations)
                        return true;
                    
                    var isThreat = targetInfo.OffenseRating > 0;
                    var relation = targetInfo.EntInfo.Relationship;

                    var o = comp.Data.Repo.Values.Set.Overrides;
                    var shootNoOwners = o.Unowned && relation == MyRelationsBetweenPlayerAndBlock.NoOwnership;
                    var shootNeutrals = o.Neutrals && relation == MyRelationsBetweenPlayerAndBlock.Neutral;
                    var shootFriends = o.Friendly && relation == MyRelationsBetweenPlayerAndBlock.Friends;
                    var shootEnemies = relation == MyRelationsBetweenPlayerAndBlock.Enemies;
                    
                    if (onlyThreats && checkRelations)
                        return isThreat && (shootEnemies || shootNoOwners || shootNeutrals || shootFriends);

                    if (onlyThreats)
                        return isThreat;

                    if (shootEnemies || shootNoOwners || shootNeutrals || shootFriends)
                        return true;
                }
            }
            return false;
        }

        internal MyTuple<Vector3D, Vector3D> GetWeaponScope(Sandbox.ModAPI.IMyTerminalBlock weaponBlock, int weaponId)
        {
            var comp = weaponBlock.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];
                var info = w.GetScope.Info;
                return new MyTuple<Vector3D, Vector3D>(info.Position, info.Direction);
            }
            return new MyTuple<Vector3D, Vector3D>();
        }
        
        // block/grid entityId, Threat, Other 
        private MyTuple<bool, bool> IsInRange(IMyEntity entity)
        {
            var grid = entity?.GetTopMostParent() as MyCubeGrid;
            Ai ai;
            if (grid != null && _session.EntityAIs.TryGetValue(grid, out ai))
            {
                return new MyTuple<bool, bool>(ai.DetectionInfo.PriorityInRange, ai.DetectionInfo.OtherInRange);
            }
            return new MyTuple<bool, bool>();
        }
        ///
        /// Phantoms
        /// 
        private static MyTuple<bool, bool, Vector3D?> GetPhantomTargetAssessment(MyEntity phantom, MyEntity target, int weaponId = 0, bool mustBeInRange = false, bool checkTargetObb = false)
        {
            var result = new MyTuple<bool, bool, Vector3D?>(false, false, null);
            var comp = phantom.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform != null && target != null && comp.Platform.State == Ready && comp.Platform.Weapons.Count > weaponId)
            {
                var w = comp.Collection[weaponId];

                var dist = Vector3D.DistanceSquared(comp.CoreEntity.PositionComp.WorldMatrixRef.Translation, target.PositionComp.WorldMatrixRef.Translation);
                var topMost = target.GetTopMostParent();
                var inRange = dist <= w.MaxTargetDistanceSqr;

                if (!inRange && mustBeInRange || topMost?.Physics == null)
                    return result;

                Vector3D targetPos;
                bool targetAligned;
                if (checkTargetObb) {

                    var targetVel = topMost.Physics.LinearVelocity;
                    var targetAccel = topMost.Physics.AngularAcceleration;
                    targetAligned =  Weapon.CanShootTargetObb(w, target, targetVel, targetAccel, out targetPos);
                }
                else
                {
                    targetAligned = Weapon.TargetAligned(w, w.NewTarget, out targetPos);
                }

                return new MyTuple<bool, bool, Vector3D?>(targetAligned, inRange, targetAligned ? targetPos : (Vector3D?)null);
            }
            return result;
        }


        private bool SetPhantomFocusTarget(MyEntity phantom, MyEntity target, int focusId)
        {
            Ai ai;
            if (target != null && !target.MarkedForClose && _session.EntityToMasterAi.TryGetValue(phantom, out ai))
            {
                if (!ai.Session.IsServer)
                    return false;

                ai.Construct.Focus.ReassignTarget(target, ai);
                return true;
            }

            return false;
        }
        private MyEntity SpawnPhantom(string phantomType, uint maxAge, bool closeWhenOutOfAmmo, long defaultReloads, string ammoOverideName, int trigger, float? modelScale, MyEntity parnet, bool addToPrunning, bool shadows, long identityId = 0, bool sync = false)
        {
            var ent = _session.CreatePhantomEntity(phantomType, maxAge, closeWhenOutOfAmmo, defaultReloads, ammoOverideName, (CoreComponent.Trigger)trigger, modelScale, parnet, addToPrunning, shadows, identityId, sync);
            return ent;
        }

        private bool ClosePhantom(MyEntity phantom)
        {
            Ai ai;
            CoreComponent comp;
            if (_session.EntityAIs.TryGetValue(phantom, out ai) && ai.CompBase.TryGetValue(phantom, out comp) && !comp.CloseCondition)
            {
                comp.ForceClose(comp.SubtypeName);
                return true;
            }
            return false;
        }

        private void SetPhantomAmmo(MyEntity phantom, int weaponId, string ammoName)
        {
            Ai ai;
            CoreComponent comp;
            if (_session.EntityAIs.TryGetValue(phantom, out ai) && ai.CompBase.TryGetValue(phantom, out comp) && comp is Weapon.WeaponComponent)
            {
                var wComp = (Weapon.WeaponComponent)comp;
                if (weaponId < wComp.Collection.Count)
                {
                    var w = wComp.Collection[weaponId];
                    foreach (var ammoType in w.System.AmmoTypes)
                    {
                        if (ammoType.AmmoName == ammoName)
                        {
                            w.QueueAmmoChange(ammoType.AmmoDef.Const.AmmoIdxPos);
                            break;
                        }
                    }
                }
            }
        }

        private void AddPhantomMagazines(MyEntity phantom, int weaponId, long magCount)
        {
            Ai ai;
            CoreComponent comp;
            if (_session.EntityAIs.TryGetValue(phantom, out ai) && ai.CompBase.TryGetValue(phantom, out comp) && comp is Weapon.WeaponComponent)
            {
                var wComp = (Weapon.WeaponComponent)comp;
                if (weaponId < wComp.Collection.Count)
                {
                    var w = wComp.Collection[weaponId];
                    w.Reload.CurrentMags = (int)magCount;
                }
            }
        }

        private void SetPhantomTriggerState(MyEntity phantom, int trigger)
        {
            Ai ai;
            CoreComponent comp;
            if (_session.IsServer && _session.EntityAIs.TryGetValue(phantom, out ai) && ai.CompBase.TryGetValue(phantom, out comp) && comp is Weapon.WeaponComponent)
            {
                var wComp = (Weapon.WeaponComponent)comp;
                wComp.ResetShootState((CoreComponent.Trigger) trigger, ai.AiOwner);
            }
            else 
                Log.Line($"failed to set phantom trigger: {(CoreComponent.Trigger)trigger} - isServer:{_session.IsServer}");
        }

        private void GetPhantomInfo(string phantomSubtypeId, ICollection<MyTuple<MyEntity, long, int, float, uint, long>> collection)
        {
        }

        private void ToggleDamageEvents(Dictionary<MyEntity, MyTuple<Vector3D, Dictionary<MyEntity, List<MyTuple<int, float, Vector3I>>>>> obj)
        {
        }


    }
}
