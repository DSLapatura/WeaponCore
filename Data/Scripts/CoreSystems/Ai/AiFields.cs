using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Projectiles;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Data.Scripts.CoreSystems.Ui.Targeting;

namespace CoreSystems.Support
{
    public partial class Ai
    {
        internal volatile bool AiInit;
        internal volatile uint AiSpawnTick;
        internal volatile uint AiCloseTick;
        internal volatile uint AiMarkedTick;
        internal volatile uint LastAiDataSave;
        internal readonly AiDetectionInfo DetectionInfo = new AiDetectionInfo();
        internal readonly Constructs Construct;
        internal readonly FastResourceLock DbLock = new FastResourceLock();

        internal readonly Dictionary<MyEntity, CoreComponent> CompBase = new Dictionary<MyEntity, CoreComponent>();
        internal readonly Dictionary<Weapon.WeaponComponent, int> WeaponIdx = new Dictionary<Weapon.WeaponComponent, int>(32);
        internal readonly Dictionary<Weapon.WeaponComponent, int> WeaponTrackIdx = new Dictionary<Weapon.WeaponComponent, int>(32);

        internal readonly Dictionary<Upgrade.UpgradeComponent, int> UpgradeIdx = new Dictionary<Upgrade.UpgradeComponent, int>(32);
        internal readonly Dictionary<SupportSys.SupportComponent, int> SupportIdx = new Dictionary<SupportSys.SupportComponent, int>(32);
        internal readonly Dictionary<ControlSys.ControlComponent, int> ControlIdx = new Dictionary<ControlSys.ControlComponent, int>(32);
        internal readonly Dictionary<Weapon.WeaponComponent, int> PhantomIdx = new Dictionary<Weapon.WeaponComponent, int>(32);

        internal readonly Dictionary<Vector3I, IMySlimBlock> AddedBlockPositions = new Dictionary<Vector3I, IMySlimBlock>(Vector3I.Comparer);
        internal readonly Dictionary<Vector3I, IMySlimBlock> RemovedBlockPositions = new Dictionary<Vector3I, IMySlimBlock>(Vector3I.Comparer);

        internal readonly Dictionary<MyStringHash, PartCounter> PartCounting = new Dictionary<MyStringHash, PartCounter>(MyStringHash.Comparer);
        internal readonly Dictionary<MyEntity, TargetInfo> Targets = new Dictionary<MyEntity, TargetInfo>(32);
        internal readonly Dictionary<long, PlayerControllerEntity> PlayerControl = new Dictionary<long, PlayerControllerEntity>();
        internal readonly Dictionary<IMyMotorStator, StatorMap> TopStators = new Dictionary<IMyMotorStator, StatorMap>();

        internal readonly ConcurrentDictionary<MyEntity, MyInventory> InventoryMonitor = new ConcurrentDictionary<MyEntity, MyInventory>();
        internal readonly ConcurrentDictionary<MyEntity, uint> NoTargetLos = new ConcurrentDictionary<MyEntity, uint>();
        internal readonly HashSet<MyEntity> ValidGrids = new HashSet<MyEntity>();
        internal readonly HashSet<MyBatteryBlock> Batteries = new HashSet<MyBatteryBlock>();
        internal readonly HashSet<MyCubeGrid> SubGridCache = new HashSet<MyCubeGrid>();
        internal readonly HashSet<Projectile> LiveProjectile = new HashSet<Projectile>();


        internal readonly ConcurrentDictionary<MyCubeGrid, byte> SubGridsRegistered = new ConcurrentDictionary<MyCubeGrid, byte>();

        internal readonly List<Weapon.WeaponComponent> TrackingComps = new List<Weapon.WeaponComponent>();
        internal readonly List<Weapon.WeaponComponent> WeaponComps = new List<Weapon.WeaponComponent>(32);
        internal readonly List<Upgrade.UpgradeComponent> UpgradeComps = new List<Upgrade.UpgradeComponent>(32);
        internal readonly List<SupportSys.SupportComponent> SupportComps = new List<SupportSys.SupportComponent>(32);
        internal readonly List<ControlSys.ControlComponent> ControlComps = new List<ControlSys.ControlComponent>(32);
        internal readonly List<Weapon.WeaponComponent> PhantomComps = new List<Weapon.WeaponComponent>(32);
        internal readonly List<Projectile> DeadProjectiles = new List<Projectile>();
        internal readonly List<Ai> TargetAisTmp = new List<Ai>();
        internal readonly List<Shields> NearByShieldsTmp = new List<Shields>();
        internal readonly List<MyEntity> NearByFriendlyShields = new List<MyEntity>();
        internal readonly List<MyEntity> TestShields = new List<MyEntity>();
        internal readonly List<MyEntity> EntitiesInRange = new List<MyEntity>();
        internal readonly List<MyEntity> ObstructionsTmp = new List<MyEntity>();
        internal readonly List<MyEntity> StaticsInRangeTmp = new List<MyEntity>();
        internal readonly List<Projectile> ProjetileCache = new List<Projectile>();
        internal readonly List<MyEntity> StaticsInRange = new List<MyEntity>();
        internal readonly List<MyEntity> Obstructions = new List<MyEntity>();
        internal readonly List<Ai> TargetAis = new List<Ai>(32);
        internal readonly List<TargetInfo> SortedTargets = new List<TargetInfo>();
        internal readonly List<DetectInfo> NewEntities = new List<DetectInfo>();

        internal readonly MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;
        internal readonly AiData Data = new AiData();
        internal TargetStatus TargetState = new TargetStatus();
        internal readonly AiComponent AiComp;
        internal readonly AiCharger Charger;
        internal readonly Session Session;
        internal MyEntity TopEntity;
        internal MyCubeGrid GridEntity;
        internal GridMap GridMap;
        internal IMyCubeGrid ImyGridEntity;
        internal MyCubeBlock PowerBlock;
        internal MyCubeGrid.MyCubeGridHitInfo GridHitInfo = new MyCubeGrid.MyCubeGridHitInfo();
        internal uint CreatedTick;
        internal Vector3 GridVel;
        internal IMyGridTerminalSystem TerminalSystem;
        internal IMyTerminalBlock LastTerminal;
        internal MyEntity MyShield;
        internal IMyTerminalBlock ShieldBlock;
        internal MyPlanet MyPlanetTmp;
        internal MyPlanet MyPlanet;
        internal Vector3D PlanetClosestPoint;
        internal Vector3D ClosestPlanetCenter;
        internal BoundingSphereD NearByEntitySphere;
        internal BoundingSphereD TopEntityVolume;
        internal BoundingSphereD ScanVolume;
        internal BoundingSphereD WaterVolume;
        internal BoundingBox BlockChangeArea = BoundingBox.CreateInvalid();
        internal AiTypes AiType;
        internal long AiOwner;
        internal bool BlockMonitoring;
        internal bool AiSleep;
        internal bool Aiming;
        internal bool DbUpdated;
        internal bool DetectOtherSignals;
        internal bool PointDefense;
        internal bool SuppressMouseShoot;
        internal bool IsStatic;
        internal bool DbReady;
        internal bool UpdatePowerSources;
        internal bool StaticEntitiesInRange;
        internal bool StaticGridInRange;
        internal bool FriendlyShieldNear;
        internal bool ShieldNear;
        internal bool ShieldFortified;
        internal bool HasPower;
        internal bool HadPower;
        internal bool CheckProjectiles;
        internal bool FadeOut;
        internal bool Concealed;
        internal bool RamProtection = true; 
        internal bool PlanetSurfaceInRange;
        internal bool InPlanetGravity;
        internal bool FirstRun = true;
        internal bool CanShoot = true;
        internal bool Registered;
        internal bool MarkedForClose;
        internal bool Closed;
        internal bool ScanInProgress;
        internal bool TouchingWater;
        internal bool IsGrid;
        internal bool SmartHandheld;
        internal bool ModOverride;
        internal bool RotorTurretAimed;
        internal uint TargetsUpdatedTick;
        internal uint VelocityUpdateTick;
        internal uint NewProjectileTick;
        internal uint LiveProjectileTick;
        internal uint ProjectileTicker;
        internal uint LastDetectEvent;
        internal uint LastBlockChangeTick;
        internal int SleepingComps;
        internal int AwakeComps;
        internal int SourceCount;
        internal int BlockCount;
        internal int PartCount;
        internal int Version;
        internal int MyProjectiles;
        internal int NearByEntities;
        internal int NearByEntitiesTmp;
        internal int WeaponsTracking;

        internal double MaxTargetingRange;
        internal double MaxTargetingRangeSqr;
        internal double DeadSphereRadius;
        internal double ClosestStaticSqr = double.MaxValue;
        internal double ClosestPlanetSqr = double.MaxValue;
        internal float GridMaxPower;
        internal float GridCurrentPower;
        internal float GridAvailablePower;
        internal float GridAssignedPower;
        internal float BatteryMaxPower;
        internal float BatteryCurrentOutput;
        internal float BatteryCurrentInput;
        internal float OptimalDps;
        internal float EffectiveDps;
        internal float PerfectDps;
        internal enum TargetType
        {
            Projectile,
            Other,
            None,
        }

        internal enum AiTypes
        {
            Grid,
            Player,
            Phantom,
        }

        private readonly List<MyEntity> _possibleTargets = new List<MyEntity>();
        private uint _pCacheTick;

        public Ai(Session session)
        {
            Session = session;
            AiComp = new AiComponent(this);
            Charger = new AiCharger(this);
            Construct = new Constructs(this);
        }

        internal void Init(MyEntity topEntity, Session session, CoreComponent.CompTypeSpecific type)
        {
            try
            {
                TopEntity = topEntity;
                GridEntity = topEntity as MyCubeGrid;
                ImyGridEntity = topEntity as IMyCubeGrid;
                AiType = GridEntity != null ? AiTypes.Grid : type == CoreComponent.CompTypeSpecific.Rifle ? AiTypes.Player : AiTypes.Phantom;
                IsGrid = AiType == AiTypes.Grid;
                DeadSphereRadius = GridEntity?.GridSizeHalf + 0.1 ?? 1.35;

                if (IsGrid)
                    session.GridToInfoMap.TryGetValue(topEntity, out GridMap);

                topEntity.Flags |= (EntityFlags)(1 << 31);
                Closed = false;
                MarkedForClose = false;

                MaxTargetingRange = session.Settings.Enforcement.MinHudFocusDistance;
                MaxTargetingRangeSqr = MaxTargetingRange * MaxTargetingRange;

                if (CreatedTick == 0)
                    CreatedTick = session.Tick;

                AiMarkedTick = uint.MaxValue;
                RegisterMyGridEvents(true);
                AiSpawnTick = Session.Tick;

                topEntity.Components.Add(AiComp);


                Data.Init(this);
                Construct.Init(this);

                if (Session.IsClient)
                    Session.SendUpdateRequest(TopEntity.EntityId, PacketType.ClientAiAdd);
            }
            catch (Exception ex) { Log.Line($"Exception in AiInit - TopEntityNull:{topEntity == null} - enforcementNull:{session?.Settings?.Enforcement == null} - sessionNull: {session == null} - GridEntityNull:{GridEntity == null} - AiType:{AiType}: {ex}", null, true); }
        }
    }
}
