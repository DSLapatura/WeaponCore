using System;
using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Projectiles;
using Sandbox.Game.Entities;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static CoreSystems.Support.HitEntity.Type;
using static CoreSystems.Support.WeaponDefinition;
using static CoreSystems.Support.Ai;
using System.Runtime.CompilerServices;
using VRage.Utils;

namespace CoreSystems.Support
{
    public class ProInfo
    {
        internal readonly Target Target = new Target();
        internal readonly SmartStorage Storage = new SmartStorage();
        internal readonly List<HitEntity> HitList = new List<HitEntity>();
        internal List<MyTuple<Vector3D, object, float>> ProHits;
        internal int[] PatternShuffle;

        internal AvShot AvShot;
        internal Weapon Weapon;
        internal Ai Ai;
        internal AmmoDef AmmoDef;
        internal MyPlanet MyPlanet;
        internal VoxelCache VoxelCache;
        internal Vector3D ShooterVel;
        internal Vector3D Origin;
        internal Vector3D OriginUp;
        internal Vector3D Direction;
        internal Vector3D TotalAcceleration;
        internal Hit Hit;
        internal XorShiftRandomStruct Random;
        internal int Age = -1;
        internal int TriggerGrowthSteps;
        internal int MuzzleId;
        internal int ObjectsHit;
        internal int Frags;
        internal int CompSceneVersion;
        internal ulong UniqueMuzzleId;
        internal ulong Id;
        internal double DistanceTraveled;
        internal double PrevDistanceTraveled;
        internal double ProjectileDisplacement;
        internal double TracerLength;
        internal double MaxTrajectory;
        internal double LastFragTime;
        internal double ShotFade;
        internal double RelativeAge = -1;
        internal double PrevRelativeAge = -1;
        internal long DamageDonePri;
        internal long DamageDoneAoe;
        internal long DamageDoneShld;
        internal long DamageDoneProj;

        internal float BaseDamagePool;
        internal float BaseHealthPool;
        internal float BaseEwarPool;
        internal bool IsFragment;
        internal bool EwarAreaPulse;
        internal bool EwarActive;
        internal bool AcquiredEntity;
        internal bool AimedShot;
        internal bool DoDamage;
        internal bool ShieldBypassed;
        internal bool ShieldKeepBypass;
        internal bool ShieldInLine;
        internal uint FirstWaterHitTick;
        internal float ShieldResistMod = 1f;
        internal float ShieldBypassMod = 1f;
        internal ushort SyncedFrags;
        internal ushort SpawnDepth;
        internal MatrixD TriggerMatrix = MatrixD.Identity;

        internal void InitVirtual(Weapon weapon, AmmoDef ammodef,  Weapon.Muzzle muzzle, double maxTrajectory, double shotFade)
        {
            Weapon = weapon;
            Ai = weapon.BaseComp.MasterAi;
            MyPlanet = Ai.MyPlanet;
            AmmoDef = ammodef;
            Target.TargetObject = weapon.Target.TargetObject;
            MuzzleId = muzzle.MuzzleId;
            UniqueMuzzleId = muzzle.UniqueId;
            Direction = muzzle.DeviatedDir;
            Origin = muzzle.Position;
            MaxTrajectory = maxTrajectory;
            ShotFade = shotFade;
        }

        internal void Clean(Projectile p)
        {
            var aConst = AmmoDef.Const;
            var monitor = Weapon.Comp.ProjectileMonitors[Weapon.PartId];
            if (monitor?.Count > 0) {
                for (int i = 0; i < monitor.Count; i++)
                    monitor[i].Invoke(Weapon.Comp.CoreEntity.EntityId, Weapon.PartId, Id, Target.TargetId, Hit.LastHit, false);

                Weapon.System.Session.MonitoredProjectiles.Remove(Id);
            }

            if (ProHits != null) {
                ProHits.Clear();
                Weapon.System.Session.ProHitPool.Push(ProHits);
            }

            Target.Reset(Weapon.System.Session.Tick, Target.States.ProjectileClean);
            HitList.Clear();
            if (aConst.IsSmart || aConst.IsDrone)
                Storage.Clean(p);
            if (IsFragment)
            {
                if (VoxelCache != null && Weapon.System.Session != null)
                {
                    Weapon.System.Session.UniqueMuzzleId = VoxelCache;
                }
            }

            if (PatternShuffle != null)
            {
                for (int i = 0; i < PatternShuffle.Length; i++)
                    PatternShuffle[i] = i;

                AmmoDef.Const.PatternShuffleArray.Push(PatternShuffle);
                PatternShuffle = null;
            }

            AvShot = null;
            Ai = null;
            MyPlanet = null;
            AmmoDef = null;
            VoxelCache = null;
            Weapon = null;
            IsFragment = false;
            EwarAreaPulse = false;
            EwarActive = false;
            AcquiredEntity = false;
            AimedShot = false;
            DoDamage = false;
            ShieldBypassed = false;
            ShieldInLine = false;
            ShieldKeepBypass = false;
            FirstWaterHitTick = 0;
            TriggerGrowthSteps = 0;
            SpawnDepth = 0;
            Frags = 0;
            MuzzleId = 0;
            Age = -1;
            RelativeAge = -1;
            PrevRelativeAge = -1;
            DamageDonePri = 0;
            DamageDoneAoe = 0;
            DamageDoneShld = 0;
            DamageDoneProj = 0;
            SyncedFrags = 0;
            ProjectileDisplacement = 0;
            MaxTrajectory = 0;
            ShotFade = 0;
            TracerLength = 0;
            UniqueMuzzleId = 0;
            LastFragTime = 0;
            ShieldResistMod = 1f;
            ShieldBypassMod = 1f;
            Hit = new Hit();
            Direction = Vector3D.Zero;
            Origin = Vector3D.Zero;
            ShooterVel = Vector3D.Zero;
            TriggerMatrix = MatrixD.Identity;
            TotalAcceleration = Vector3D.Zero;

        }
    }

    internal enum DroneStatus
    {
        Launch,
        Transit, //Movement from/to target area
        Approach, //Final transition between transit and orbit
        Orbit, //Orbit & shoot
        Strafe, //Nose at target movement, for PointType = direct and PointAtTarget = false
        Escape, //Move away from imminent collision
        Kamikaze,
        Return, //Return to "base"
        Dock,
    }

    internal enum DroneMission
    {
        Attack,
        Defend,
        Rtb,
    }

    internal class SmartStorage
    {
        internal readonly Vector3D[] PastProInfos = new Vector3D[30];
        internal readonly ProNavGuidanceInlined Navigation = new ProNavGuidanceInlined(60);
        internal readonly ClosestObstacles Obstacle = new ClosestObstacles();

        internal DroneStatus DroneStat;
        internal DroneMission DroneMsn;
        internal Vector3D SetTargetPos;
        internal Vector3D RandOffsetDir;
        internal Vector3D OffsetDir;
        internal Vector3D LookAtPos;
        internal FakeTargets DummyTargets;
        internal MyEntity NavTargetEnt;
        internal BoundingSphereD NavTargetBound;
        internal bool SmartReady;
        internal bool WasTracking;
        internal bool Sleep;
        internal bool PickTarget;
        internal int ProSyncPosMissCount;
        internal int ChaseAge;
        internal int LastOffsetTime;
        internal int SmartSlot;
        internal int LastActivatedStage = -1;
        internal int RequestedStage = -1;
        internal ulong SyncId = ulong.MaxValue;
        internal double StartDistanceTraveled;
        internal double ZombieLifeTime;
        internal double PrevZombieLifeTime;

        internal void Clean(Projectile p)
        {
            if (p != null)
            {
                if (p.Info.AmmoDef.Const.FullSync)
                {
                    for (int i = 0; i < PastProInfos.Length; i++)
                        PastProInfos[i] = Vector3D.Zero;
                }
                else if (SyncId != ulong.MaxValue)
                {
                    p.Info.Weapon.ProjectileSyncMonitor.Remove(SyncId);
                }
            }
            
            SyncId = ulong.MaxValue;
            ProSyncPosMissCount = 0;
            ChaseAge = 0;
            ZombieLifeTime = 0;
            PrevZombieLifeTime = 0;
            LastOffsetTime = 0;
            DroneStat = DroneStatus.Launch;
            DroneMsn = DroneMission.Attack;
            SetTargetPos = Vector3D.Zero;
            RandOffsetDir = Vector3D.Zero;
            OffsetDir = Vector3D.Zero;
            LookAtPos = Vector3D.Zero;
            NavTargetEnt = null;
            Obstacle.Entity = null;
            Obstacle.LastSeenTick = uint.MaxValue;
            DummyTargets = null;
            NavTargetBound = new BoundingSphereD(Vector3D.Zero,0);
            SmartReady = false;
            WasTracking = false;
            PickTarget = false;
            SmartSlot = 0;
            StartDistanceTraveled = 0;
            LastActivatedStage = -1;
            RequestedStage = -1;
            Sleep = false;

            Navigation.ClearAcceleration();
        }

        internal class ClosestObstacles
        {
            internal MyEntity Entity;
            internal uint LastSeenTick = uint.MaxValue;
            internal BoundingSphereD AvoidSphere;
        }
    }

    internal struct DeferedVoxels
    {
        internal enum VoxelIntersectBranch
        {
            None,
            DeferedMissUpdate,
            DeferFullCheck,
            PseudoHit1,
            PseudoHit2,
        }

        internal Projectile Projectile;
        internal MyVoxelBase Voxel;
        internal VoxelIntersectBranch Branch;
    }

    public class HitEntity
    {
        public enum Type
        {
            Shield,
            Grid,
            Voxel,
            Destroyable,
            Stale,
            Projectile,
            Field,
            Effect,
            Water,
        }

        public readonly List<RootBlocks> Blocks = new List<RootBlocks>(16);
        public readonly List<Vector3I> Vector3ICache = new List<Vector3I>(16);
        public MyEntity Entity;
        public MyEntity ShieldEntity;
        internal Projectile Projectile;
        public ProInfo Info;
        public LineD Intersection;
        public bool Hit;
        public bool SphereCheck;
        public bool DamageOverTime;
        public bool PulseTrigger;
        public bool SelfHit;
        public BoundingSphereD PruneSphere;
        public Vector3D? HitPos;
        public double? HitDist;
        public Type EventType;
        public int DamageMulti = 1;

        public void Clean()
        {
            Vector3ICache.Clear();
            Entity = null;
            ShieldEntity = null;
            Projectile = null;
            Intersection.Length = 0;
            Intersection.Direction = Vector3D.Zero;
            Intersection.From = Vector3D.Zero;
            Intersection.To = Vector3D.Zero;
            Blocks.Clear();
            Hit = false;
            HitPos = null;
            HitDist = null;
            Info = null;
            EventType = Stale;
            PruneSphere = new BoundingSphereD();
            SphereCheck = false;
            DamageOverTime = false;
            PulseTrigger = false;
            SelfHit = false;
            DamageMulti = 1;
        }

        public struct RootBlocks
        {
            public IMySlimBlock Block;
            public Vector3I QueryPos;
        }
    }

    internal struct Hit
    {
        internal MyEntity Entity;
        internal HitEntity.Type EventType;
        internal Vector3D SurfaceHit;
        internal Vector3D LastHit;
        internal Vector3D HitVelocity;
        internal uint HitTick;
    }

    internal class WeaponFrameCache
    {
        internal int Hits;
        internal double HitDistance;
        internal int VirutalId = -1;
        internal uint FakeCheckTick;
        internal double FakeHitDistance;
    }

    internal struct NewVirtual
    {
        internal ProInfo Info;
        internal Weapon.Muzzle Muzzle;
        internal bool Rotate;
        internal int VirtualId;
    }

    internal struct NewProjectile
    {
        internal enum Kind
        {
            Normal,
            Virtual,
            Frag,
            Client
        }

        internal Weapon.Muzzle Muzzle;
        internal AmmoDef AmmoDef;
        internal MyEntity TargetEnt;
        internal List<NewVirtual> NewVirts;
        internal Vector3D Origin;
        internal Vector3D OriginUp;
        internal Vector3D Direction;
        internal Vector3D Velocity;
        internal long PatternCycle;
        internal float MaxTrajectory;
        internal Kind Type;
    }

    internal class Fragments
    {
        internal List<Fragment> Sharpnel = new List<Fragment>();
        internal void Init(Projectile p, Stack<Fragment> fragPool, AmmoDef ammoDef, bool timedSpawn, ref Vector3D newOrigin, ref Vector3D pointDir)
        {
            var info = p.Info;
            var target = info.Target;
            var aConst = info.AmmoDef.Const;
            var fragCount = p.Info.AmmoDef.Fragment.Fragments;
            var guidance = aConst.IsDrone || aConst.IsSmart;
            if (info.Ai.Session.IsClient && fragCount > 0 && info.AimedShot && aConst.ClientPredictedAmmo && !info.IsFragment)
            {
                Projectiles.Projectiles.SendClientHit(p, false);
            }

            for (int i = 0; i < fragCount; i++)
            {
                var frag = fragPool.Count > 0 ? fragPool.Pop() : new Fragment();

                frag.Weapon = info.Weapon;
                frag.Ai = info.Ai;
                frag.AmmoDef = ammoDef;
                if (guidance)
                {
                    frag.DummyTargets = info.Storage.DummyTargets;
                }
                frag.SyncId = info.Storage.SyncId;
                if (frag.SyncId != ulong.MaxValue)
                    frag.SyncedFrags = ++info.SyncedFrags;

                frag.Depth = (ushort) (info.SpawnDepth + 1);

                frag.TargetState = target.TargetState;
                frag.TargetEntity = target.TargetObject;

                frag.MuzzleId = info.MuzzleId;
                frag.Radial = aConst.FragRadial;
                frag.SceneVersion = info.CompSceneVersion;
                frag.Origin = newOrigin;
                frag.OriginUp = info.OriginUp;
                frag.Random = new XorShiftRandomStruct(info.Random.NextUInt64());
                frag.DoDamage = info.DoDamage;
                frag.PrevTargetPos = p.TargetPosition;
                frag.Velocity = !aConst.FragDropVelocity ? p.Velocity : Vector3D.Zero;
                frag.AcquiredEntity = info.AcquiredEntity;
                frag.IgnoreShield = info.ShieldBypassed && aConst.ShieldDamageBypassMod > 0;
                var posValue = aConst.FragDegrees;
                posValue *= 0.5f;
                var randomFloat1 = (float)(frag.Random.NextDouble() * posValue) + (frag.Radial);
                var randomFloat2 = (float)(frag.Random.NextDouble() * MathHelper.TwoPi);
                var mutli = aConst.FragReverse ? -1 : 1;

                var r1Sin = Math.Sin(randomFloat1);
                var r2Sin = Math.Sin(randomFloat2);
                var r1Cos = Math.Cos(randomFloat1);
                var r2Cos = Math.Cos(randomFloat2);

                var shrapnelDir = Vector3.TransformNormal(mutli  * -new Vector3(r1Sin * r2Cos, r1Sin * r2Sin, r1Cos), Matrix.CreateFromDir(pointDir));

                frag.Direction = shrapnelDir;
                Sharpnel.Add(frag);
            }
        }

        internal void Spawn(out int spawned)
        {
            Session session = null;
            spawned = Sharpnel.Count;
            for (int i = 0; i < spawned; i++)
            {
                var frag = Sharpnel[i];
                session = frag.Ai.Session;
                var p = session.Projectiles.ProjectilePool.Count > 0 ? session.Projectiles.ProjectilePool.Pop() : new Projectile();
                var info = p.Info;
                info.Weapon = frag.Weapon;

                info.Ai = frag.Ai;
                info.Id = session.Projectiles.CurrentProjectileId++;

                var aDef = frag.AmmoDef;
                var aConst = aDef.Const;
                info.AmmoDef = aDef;
                var target = info.Target;
                target.TargetObject = frag.TargetEntity;
                target.TargetState = frag.TargetState;
                target.TargetState = frag.TargetState;
                info.Target.TargetPos = frag.PrevTargetPos;
                info.IsFragment = true;
                info.MuzzleId = frag.MuzzleId;
                info.UniqueMuzzleId = session.UniqueMuzzleId.Id;
                info.Origin = frag.Origin;
                info.OriginUp = frag.OriginUp;
                info.Random = frag.Random;
                info.DoDamage = frag.DoDamage;
                info.SpawnDepth = frag.Depth;
                info.SyncedFrags = frag.SyncedFrags;
                info.BaseDamagePool = aConst.BaseDamage;
                p.TargetPosition = frag.PrevTargetPos;
                info.Direction = frag.Direction;
                info.ShooterVel = frag.Velocity;
                p.Gravity = aConst.FeelsGravity && info.Ai.InPlanetGravity ? frag.Weapon.GravityPoint * aConst.GravityMultiplier : Vector3D.Zero;
                info.AcquiredEntity = frag.AcquiredEntity;
                info.MaxTrajectory = aConst.MaxTrajectory;
                info.ShotFade = 0;
                info.ShieldBypassed = frag.IgnoreShield;
                info.CompSceneVersion = frag.SceneVersion;

                if (aConst.IsDrone || aConst.IsSmart)
                {
                    info.Storage.DummyTargets = frag.DummyTargets;

                    if (aConst.FullSync)
                        info.Storage.SyncId = frag.SyncId;
                }
                
                if (session.AdvSync && (aConst.PdDeathSync || aConst.OnHitDeathSync))
                {
                    var syncPart1 = (ushort)((frag.SyncId >> 48) & 0x000000000000FFFF);
                    var syncPart2 = (ushort)((frag.SyncId >> 32) & 0x000000000000FFFF);

                    info.Storage.SyncId = ((ulong)syncPart1 << 48) | ((ulong)syncPart2 << 32) | ((ulong)info.SyncedFrags << 16) | info.SpawnDepth;
                    //info.Storage.SyncId = ((ulong)syncPart1 & 0x00000000FFFFFFFF) << 32 | ((ulong)syncPart2 << 32) | ((ulong)info.SyncedFrags << 16) | info.SpawnDepth;
                    p.Info.Weapon.ProjectileSyncMonitor[info.Storage.SyncId] = p;
                }


                session.Projectiles.ActiveProjetiles.Add(p);
                p.Start();
                if (aConst.Health > 0 && !aConst.IsBeamWeapon)
                    session.Projectiles.AddTargets.Add(p);

                session.Projectiles.FragmentPool.Push(frag);
            }
            session?.Projectiles.ShrapnelPool.Push(this);
            Sharpnel.Clear();
        }
    }

    internal class Fragment
    {
        public Weapon Weapon;
        public Ai Ai;
        public AmmoDef AmmoDef;
        public object TargetEntity;
        public FakeTargets DummyTargets;
        public Vector3D Origin;
        public Vector3D OriginUp;
        public Vector3D Direction;
        public Vector3D Velocity;
        public Vector3D PrevTargetPos;
        public int MuzzleId;
        public ushort Depth;

        public XorShiftRandomStruct Random;
        public bool DoDamage;
        public bool AcquiredEntity;
        public bool IgnoreShield;
        public Target.TargetStates TargetState;
        public float Radial;
        internal int SceneVersion;
        internal ulong SyncId;
        internal ushort SyncedFrags;
    }

    public struct ApproachDebug
    {
        public ApproachConstants Approach;
        public ulong ProId;
        public uint LastTick;
        public int Stage;
        public bool Start1;
        public bool Start2;
        public bool End1;
        public bool End2;
    }

    public class VoxelCache
    {
        internal BoundingSphereD HitSphere = new BoundingSphereD(Vector3D.Zero, 2f);
        internal BoundingSphereD MissSphere = new BoundingSphereD(Vector3D.Zero, 1.5f);
        internal BoundingSphereD PlanetSphere = new BoundingSphereD(Vector3D.Zero, 0.1f);
        internal Vector3D FirstPlanetHit;

        internal uint HitRefreshed;
        internal ulong Id;

        internal void Update(MyVoxelBase voxel, ref Vector3D? hitPos, uint tick)
        {
            var hit = hitPos ?? Vector3D.Zero;
            HitSphere.Center = hit;
            HitRefreshed = tick;
            if (voxel is MyPlanet)
            {
                double dist;
                Vector3D.DistanceSquared(ref hit, ref FirstPlanetHit, out dist);
                if (dist > 625)
                {
                    FirstPlanetHit = hit;
                    PlanetSphere.Radius = 0.1f;
                }
            }
        }

        internal void GrowPlanetCache(Vector3D hitPos)
        {
            double dist;
            Vector3D.Distance(ref PlanetSphere.Center, ref hitPos, out dist);
            PlanetSphere = new BoundingSphereD(PlanetSphere.Center, dist);
        }

        internal void DebugDraw()
        {
            DsDebugDraw.DrawSphere(HitSphere, Color.Red);
        }
    }
}
