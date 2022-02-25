using System;
using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Projectiles;
using Sandbox.Engine.Physics;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static CoreSystems.Support.HitEntity.Type;
using static CoreSystems.Support.WeaponDefinition;
using static CoreSystems.Support.Ai;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
namespace CoreSystems.Support
{
    public class ProInfo
    {
        internal readonly Target Target = new Target(null, true);
        internal readonly SmartStorage Storage = new SmartStorage();
        internal readonly List<HitEntity> HitList = new List<HitEntity>(4);

        internal AvShot AvShot;
        internal Weapon Weapon;
        internal Ai Ai;
        internal MyEntity PrimeEntity;
        internal MyEntity TriggerEntity;
        internal AmmoDef AmmoDef;
        internal MyPlanet MyPlanet;
        internal MyEntity MyShield;
        internal VoxelCache VoxelCache;
        internal int[] PatternShuffle;
        internal Vector3D ShooterVel;
        internal Vector3D Origin;
        internal Vector3D OriginUp;
        internal Vector3D Direction;
        internal Vector3D PrevTargetPos;
        internal Hit Hit;
        internal XorShiftRandomStruct Random;
        internal FakeTargets DummyTargets;
        internal int TriggerGrowthSteps;
        internal int MuzzleId;
        internal int ObjectsHit;
        internal int Age;
        internal int FireCounter;
        internal int SpawnDepth;
        internal int Frags;
        internal int LastFragTime;
        internal int CompSceneVersion;
        internal ulong UniqueMuzzleId;
        internal ulong Id;
        internal long SyncId;
        internal double DistanceTraveled;
        internal double PrevDistanceTraveled;
        internal double ProjectileDisplacement;
        internal double ClosestDistSqrToTarget = double.MaxValue;
        internal double TracerLength;
        internal double MaxTrajectory;
        internal double DamageDone;
        internal float ShotFade;
        internal float BaseDamagePool;
        internal float BaseHealthPool;
        internal float BaseEwarPool;
        internal bool IsFragment;
        internal bool EwarAreaPulse;
        internal bool EwarActive;
        internal bool LockOnFireState;
        internal bool AimedShot;
        internal bool DoDamage;
        internal bool InPlanetGravity;
        internal bool ShieldBypassed;
        internal bool ShieldKeepBypass;
        internal bool ShieldInLine;
        internal bool SmartReady;
        internal float ShieldResistMod = 1f;
        internal float ShieldBypassMod = 1f;

        internal MatrixD TriggerMatrix = MatrixD.Identity;

        internal void InitVirtual(Weapon weapon, AmmoDef ammodef, MyEntity primeEntity, MyEntity triggerEntity, Weapon.Muzzle muzzle, double maxTrajectory, float shotFade)
        {
            Weapon = weapon;
            Ai = weapon.BaseComp.Ai;
            MyPlanet = weapon.BaseComp.Ai.MyPlanet;
            MyShield = weapon.BaseComp.Ai.MyShield;
            InPlanetGravity = weapon.BaseComp.Ai.InPlanetGravity;
            AmmoDef = ammodef;
            PrimeEntity = primeEntity;
            TriggerEntity = triggerEntity;
            Target.TargetEntity = weapon.Target.TargetEntity;
            Target.Projectile = weapon.Target.Projectile;
            Target.CoreEntity = weapon.Target.CoreEntity;
            Target.CoreCube = weapon.Target.CoreCube;
            Target.CoreParent = weapon.Target.CoreParent;
            Target.CoreIsCube = weapon.Target.CoreIsCube;
            MuzzleId = muzzle.MuzzleId;
            UniqueMuzzleId = muzzle.UniqueId;
            Direction = muzzle.DeviatedDir;
            Origin = muzzle.Position;
            MaxTrajectory = maxTrajectory;
            ShotFade = shotFade;
        }

        internal void Clean(bool usesStorage = false)
        {
            if (Weapon.Monitors?.Count > 0) {
                for (int i = 0; i < Weapon.Monitors.Count; i++)
                    Weapon.Monitors[i].Invoke(Target.CoreEntity.EntityId, Weapon.PartId,Id, Target.TargetId, Hit.LastHit, false);

                Weapon.System.Session.MonitoredProjectiles.Remove(Id);
            }

            Target.Reset(Weapon.System.Session.Tick, Target.States.ProjectileClosed);
            HitList.Clear();
            if (IsFragment)
            {
                if (VoxelCache != null && Weapon.System.Session != null)
                {
                    Weapon.System.Session.UniqueMuzzleId = VoxelCache;
                }
                else Log.Line("HasFragment voxelcache return failure");
            }

            if (PrimeEntity != null)
            {
                AmmoDef.Const.PrimeEntityPool.Return(PrimeEntity);
                PrimeEntity = null;
            }

            if (TriggerEntity != null)
            {
                Weapon.System.Session.TriggerEntityPool.Return(TriggerEntity);
                TriggerEntity = null;
            }

            if (PatternShuffle != null)
            {
                for (int i = 0; i < PatternShuffle.Length; i++)
                    PatternShuffle[i] = 0;

                AmmoDef.Const.PatternShuffleArray.Push(PatternShuffle);
                PatternShuffle = null;
            }

            if (usesStorage)
                Storage.Clean();

            AvShot = null;
            Ai = null;
            MyPlanet = null;
            MyShield = null;
            AmmoDef = null;
            VoxelCache = null;
            Weapon = null;
            IsFragment = false;
            EwarAreaPulse = false;
            EwarActive = false;
            LockOnFireState = false;
            AimedShot = false;
            DoDamage = false;
            InPlanetGravity = false;
            ShieldBypassed = false;
            ShieldInLine = false;
            ShieldKeepBypass = false;
            SmartReady = false;
            TriggerGrowthSteps = 0;
            SpawnDepth = 0;
            Frags = 0;
            MuzzleId = 0;
            Age = 0;
            SyncId = -1;
            DamageDone = 0;
            ProjectileDisplacement = 0;
            MaxTrajectory = 0;
            ShotFade = 0;
            TracerLength = 0;
            FireCounter = 0;
            UniqueMuzzleId = 0;
            LastFragTime = 0;
            ClosestDistSqrToTarget = double.MinValue; 
            ShieldResistMod = 1f;
            ShieldBypassMod = 1f;
            Hit = new Hit();
            Direction = Vector3D.Zero;
            Origin = Vector3D.Zero;
            ShooterVel = Vector3D.Zero;
            TriggerMatrix = MatrixD.Identity;
            PrevTargetPos = Vector3D.Zero;
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
        internal DroneStatus DroneStat;
        internal DroneMission DroneMsn;
        internal Vector3D TaskPosition;
        internal MyEntity NavTargetEnt;
        internal Target ShootTarget;
        internal BoundingSphereD NavTargetBound;
        internal bool IsFriend;

        internal void Clean()
        {
            DroneStat = DroneStatus.Launch;
            DroneMsn = DroneMission.Attack;
            TaskPosition = Vector3D.Zero;
            NavTargetEnt = null;
            ShootTarget = null;
            NavTargetBound = new BoundingSphereD(Vector3D.Zero,0);
            IsFriend = false;
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
        }

        public readonly List<RootBlocks> Blocks = new List<RootBlocks>(16);
        public readonly List<Vector3I> Vector3ICache = new List<Vector3I>(16);
        public MyEntity Entity;
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
        internal IMySlimBlock Block;
        internal MyEntity Entity;
        internal Vector3D SurfaceHit;
        internal Vector3D LastHit;
        internal Vector3D HitVelocity;
        internal uint HitTick;
    }

    internal class VoxelParallelHits
    {
        internal uint RequestTick;
        internal uint ResultTick;
        internal uint LastTick;
        internal IHitInfo HitInfo;
        private bool _start;
        private uint _startTick;
        private int _miss;
        private int _maxDelay;
        private bool _idle;
        private Vector3D _endPos = Vector3D.MinValue;

        internal bool Cached(LineD lineTest, ProInfo i)
        {
            double dist;
            Vector3D.DistanceSquared(ref _endPos, ref lineTest.To, out dist);

            _maxDelay = i.MuzzleId == -1 ? i.Weapon.System.Muzzles.Length : 1;

            var thisTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);
            _start = thisTick - LastTick > _maxDelay || dist > 5;

            LastTick = thisTick;

            if (_start) {
                _startTick = thisTick;
                _endPos = lineTest.To;
            }

            var runTime = thisTick - _startTick;

            var fastPath = runTime > (_maxDelay * 3) + 1;
            var useCache = runTime > (_maxDelay * 3) + 2;
            if (fastPath) {
                if (_miss > 1) {
                    if (_idle && _miss % 120 == 0) _idle = false;
                    else _idle = true;

                    if (_idle) return true;
                }
                RequestTick = thisTick;
                MyAPIGateway.Physics.CastRayParallel(ref lineTest.From, ref lineTest.To, CollisionLayers.VoxelCollisionLayer, Results);
            }
            return useCache;
        }

        internal void Results(IHitInfo info)
        {
            ResultTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);
            if (info == null)
            {
                _miss++;
                HitInfo = null;
                return;
            }

            var voxel = info.HitEntity as MyVoxelBase;
            if (voxel?.RootVoxel is MyPlanet)
            {
                HitInfo = info;
                _miss = 0;
                return;
            }
            _miss++;
            HitInfo = null;
        }

        internal bool NewResult(out IHitInfo cachedPlanetResult)
        {
            cachedPlanetResult = null;
            var thisTick = (uint)(MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds * Session.TickTimeDiv);

            if (HitInfo == null)
            {
                _miss++;
                return false;
            }

            if (thisTick > RequestTick + _maxDelay)
                return false;

            //Log.Line($"newResult: {thisTick} - {RequestTick} - {_maxDelay} - {RequestTick + _maxDelay} - {thisTick - (RequestTick + _maxDelay)}");
            cachedPlanetResult = HitInfo;
            return true;
        }
    }

    internal class WeaponFrameCache
    {
        internal bool VirtualHit;
        internal int Hits;
        internal double HitDistance;
        internal HitEntity HitEntity = new HitEntity();
        internal IMySlimBlock HitBlock;
        internal int VirutalId = -1;
        internal VoxelParallelHits[] VoxelHits;
        internal double MissDistance;

        internal WeaponFrameCache(int size)
        {
            VoxelHits = new VoxelParallelHits[size];
            for (int i = 0; i < size; i++) VoxelHits[i] = new VoxelParallelHits();
        }
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
        internal void Init(Projectile p, MyConcurrentPool<Fragment> fragPool, AmmoDef ammoDef, ref Vector3D newOrigin, ref Vector3D pointDir)
        {
            var info = p.Info;
            var target = info.Target;
            var aConst = info.AmmoDef.Const;

            for (int i = 0; i < p.Info.AmmoDef.Fragment.Fragments; i++)
            {
                var frag = fragPool.Get();
                frag.Weapon = info.Weapon;
                frag.Ai = info.Ai;
                frag.AmmoDef = ammoDef;
                
                frag.Depth = info.SpawnDepth + 1;
                frag.TargetState = target.TargetState;
                frag.TargetEntity = target.TargetEntity;
                frag.TargetProjectile = target.Projectile;

                frag.MuzzleId = info.MuzzleId;
                frag.CoreEntity = target.CoreEntity;
                frag.CoreParent = target.CoreParent;
                frag.CoreCube = target.CoreCube;
                frag.CoreIsCube = target.CoreIsCube;
                frag.Radial = aConst.FragRadial;

                frag.Origin = newOrigin;
                frag.OriginUp = info.OriginUp;
                frag.Random = new XorShiftRandomStruct(info.Random.NextUInt64());
                frag.DoDamage = info.DoDamage;
                frag.PredictedTargetPos = p.PredictedTargetPos;
                frag.Velocity = !aConst.FragDropVelocity ? p.Velocity : Vector3D.Zero;
                frag.LockOnFireState = info.LockOnFireState;
                frag.IgnoreShield = info.ShieldBypassed && aConst.ShieldDamageBypassMod > 0;
                var posValue = aConst.FragDegrees;
                posValue *= 0.5f;
                var randomFloat1 = (float)(frag.Random.NextDouble() * posValue) + (frag.Radial);
                var randomFloat2 = (float)(frag.Random.NextDouble() * MathHelper.TwoPi);
                var mutli = aConst.FragReverse ? -1 : 1;

                var shrapnelDir = Vector3.TransformNormal(mutli  * -new Vector3(
                    MyMath.FastSin(randomFloat1) * MyMath.FastCos(randomFloat2),
                    MyMath.FastSin(randomFloat1) * MyMath.FastSin(randomFloat2),
                    MyMath.FastCos(randomFloat1)), Matrix.CreateFromDir(pointDir));

                frag.Direction = shrapnelDir;
                frag.PrimeEntity = null;
                frag.TriggerEntity = null;
                if (frag.AmmoDef.Const.PrimeModel && frag.AmmoDef.Const.PrimeEntityPool.Count > 0)
                    frag.PrimeEntity = frag.AmmoDef.Const.PrimeEntityPool.Get();

                if (frag.AmmoDef.Const.TriggerModel && info.Ai.Session.TriggerEntityPool.Count > 0)
                    frag.TriggerEntity = info.Ai.Session.TriggerEntityPool.Get();

                if (frag.AmmoDef.Const.PrimeModel && frag.PrimeEntity == null || frag.AmmoDef.Const.TriggerModel && frag.TriggerEntity == null)
                    info.Ai.Session.FragmentsNeedingEntities.Add(frag);

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
                info.PrimeEntity = frag.PrimeEntity;
                info.TriggerEntity = frag.TriggerEntity;
                var target = info.Target;
                target.TargetEntity = frag.TargetEntity;
                target.TargetState = frag.TargetState;
                target.Projectile = frag.TargetProjectile;
                target.CoreEntity = frag.CoreEntity;
                target.CoreParent = frag.CoreParent;
                target.CoreCube = frag.CoreCube;
                target.CoreIsCube = frag.CoreIsCube;

                target.TargetState = frag.TargetState;

                info.IsFragment = true;
                info.MuzzleId = frag.MuzzleId;
                info.UniqueMuzzleId = session.UniqueMuzzleId.Id;
                info.Origin = frag.Origin;
                info.OriginUp = frag.OriginUp;
                info.Random = frag.Random;
                info.DoDamage = frag.DoDamage;
                info.SpawnDepth = frag.Depth;
                info.BaseDamagePool = aConst.BaseDamage;
                p.PredictedTargetPos = frag.PredictedTargetPos;
                info.Direction = frag.Direction;
                p.StartSpeed = frag.Velocity;
                info.LockOnFireState = frag.LockOnFireState;
                info.MaxTrajectory = aConst.MaxTrajectory;
                info.ShotFade = 0;
                info.ShieldBypassed = frag.IgnoreShield;

                session.Projectiles.ActiveProjetiles.Add(p);
                p.Start();

                if (aConst.Health > 0 && !aConst.IsBeamWeapon)
                    session.Projectiles.AddTargets.Add(p);


                session.Projectiles.FragmentPool.Return(frag);
            }

            session?.Projectiles.ShrapnelPool.Return(this);
            Sharpnel.Clear();
        }
    }

    internal class Fragment
    {
        public Weapon Weapon;
        public Ai Ai;
        public AmmoDef AmmoDef;
        public MyEntity PrimeEntity;
        public MyEntity TriggerEntity;
        public MyEntity TargetEntity;
        public MyEntity CoreEntity;
        public MyEntity CoreParent;
        public MyCubeBlock CoreCube;
        public Projectile TargetProjectile;
        public Vector3D Origin;
        public Vector3D OriginUp;
        public Vector3D Direction;
        public Vector3D Velocity;
        public Vector3D PredictedTargetPos;
        public int MuzzleId;
        public int Depth;
        public XorShiftRandomStruct Random;
        public bool DoDamage;
        public bool LockOnFireState;
        public bool IgnoreShield;
        public bool CoreIsCube;
        public Target.TargetStates TargetState;
        public float Radial;
    }

    public class VoxelCache
    {
        internal BoundingSphereD HitSphere = new BoundingSphereD(Vector3D.Zero, 2f);
        internal BoundingSphereD MissSphere = new BoundingSphereD(Vector3D.Zero, 1.5f);
        internal BoundingSphereD PlanetSphere = new BoundingSphereD(Vector3D.Zero, 0.1f);
        //internal BoundingSphereD TestSphere = new BoundingSphereD(Vector3D.Zero, 5f);
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
                    //Log.Line("early planet reset");
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
