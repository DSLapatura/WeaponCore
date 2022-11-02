using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using CoreSystems.Support;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Session;
using static CoreSystems.Support.DroneStatus;
using static CoreSystems.Support.WeaponDefinition.AmmoDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.EwarDef.EwarType;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.FragmentDef.TimedSpawnDef;

namespace CoreSystems.Projectiles
{
    internal class Projectile
    {
        internal const float StepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;

        internal readonly ProInfo Info = new ProInfo();
        internal readonly List<MyLineSegmentOverlapResult<MyEntity>> MySegmentList = new List<MyLineSegmentOverlapResult<MyEntity>>();
        internal readonly List<MyEntity> MyEntityList = new List<MyEntity>();
        internal readonly List<ProInfo> VrPros = new List<ProInfo>();
        internal readonly List<Projectile> EwaredProjectiles = new List<Projectile>();
        internal readonly List<Ai> Watchers = new List<Ai>();
        internal readonly HashSet<Projectile> Seekers = new HashSet<Projectile>();
        internal ProjectileState State;
        internal EntityState ModelState;
        internal MyEntityQueryType PruneQuery;
        internal CheckTypes CheckType;
        internal HadTargetState HadTarget;
        internal Vector3D AccelDir;
        internal Vector3D Position;
        internal Vector3D OffsetDir;
        internal Vector3D LastPosition;
        internal Vector3D StartSpeed;
        internal Vector3D Velocity;
        internal Vector3D PrevVelocity;
        internal Vector3D InitalStep;
        internal Vector3D MaxAccelVelocity;
        internal Vector3D MaxVelocity;
        internal Vector3D TravelMagnitude;
        internal Vector3D LastEntityPos;
        internal Vector3D PrevTargetPos;
        internal Vector3D OffsetTarget;
        internal Vector3 PrevTargetVel;
        internal Vector3 Gravity;
        internal LineD Beam;
        internal BoundingSphereD PruneSphere;
        //internal MyOrientedBoundingBoxD ProjObb;
        internal double AccelInMetersPerSec;
        internal double DistanceToTravelSqr;
        internal double VelocityLengthSqr;
        internal double DistanceFromCameraSqr;
        internal double MaxSpeedSqr;
        internal double MaxSpeed;
        internal double MaxTrajectorySqr;
        internal double DistanceToSurfaceSqr;
        internal float DesiredSpeed;
        internal int DeaccelRate;
        internal int PruningProxyId = -1;
        internal int TargetsSeen;
        internal bool EnableAv;
        internal bool MoveToAndActivate;
        internal bool LockedTarget;
        internal bool LinePlanetCheck;
        internal bool AtMaxRange;
        internal bool EarlyEnd;
        internal bool LineOrNotModel;
        internal bool Intersecting;
        internal bool FinalizeIntersection;
        internal bool Asleep;
        internal enum CheckTypes
        {
            Ray,
            Sphere,
            CachedSphere,
            CachedRay,
        }

        internal enum ProjectileState
        {
            Alive,
            Detonate,
            Detonated,
            OneAndDone,
            Dead,
            Depleted,
            Destroy,
        }

        internal enum EntityState
        {
            Exists,
            None
        }


        public enum HadTargetState
        {
            None,
            Projectile,
            Entity,
            Fake,
            Other,
        }

        #region Start
        internal void Start()
        {
            var session = Info.Ai.Session;
            var ammoDef = Info.AmmoDef;
            var aConst = ammoDef.Const;

            if (aConst.FragmentPattern) {
                if (aConst.PatternShuffleArray.Count > 0)
                    Info.PatternShuffle = aConst.PatternShuffleArray.Pop();
                else
                {
                    Info.PatternShuffle = new int[aConst.FragPatternCount];
                    for (int i = 0; i < Info.PatternShuffle.Length; i++)
                        Info.PatternShuffle[i] = i;
                }
            }

            OffsetDir = Vector3D.Zero;
            Position = Info.Origin;
            AccelDir = Info.Direction;
            var cameraStart = session.CameraPos;
            Vector3D.DistanceSquared(ref cameraStart, ref Info.Origin, out DistanceFromCameraSqr);
            var probability = ammoDef.AmmoGraphics.VisualProbability;
            EnableAv = !aConst.VirtualBeams && !session.DedicatedServer && DistanceFromCameraSqr <= session.SyncDistSqr && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));
            ModelState = EntityState.None;
            LastEntityPos = Position;
            Info.AvShot = null;
            Info.Age = -1;

            TargetsSeen = 0;
            PruningProxyId = -1;

            LinePlanetCheck = false;
            AtMaxRange = false;
            Intersecting = false;
            Asleep = false;
            Info.PrevDistanceTraveled = 0;
            Info.DistanceTraveled = 0;
            DistanceToSurfaceSqr = double.MaxValue;
            var trajectory = ammoDef.Trajectory;
            var guidance = trajectory.Guidance;

            if (aConst.DynamicGuidance && session.AntiSmartActive) DynTrees.RegisterProjectile(this);

            Info.MyPlanet = Info.Ai.MyPlanet;
            
            if (!session.VoxelCaches.TryGetValue(Info.UniqueMuzzleId, out Info.VoxelCache))
                Info.VoxelCache = session.VoxelCaches[ulong.MaxValue];

            if (Info.MyPlanet != null)
                Info.VoxelCache.PlanetSphere.Center = Info.Ai.ClosestPlanetCenter;

            Info.MyShield = Info.Ai.MyShield;
            Info.Ai.ProjectileTicker = Info.Ai.Session.Tick;
            Info.ObjectsHit = 0;
            Info.BaseHealthPool = aConst.Health;
            Info.BaseEwarPool = aConst.Health;

            Info.Storage.IsSmart = aConst.IsSmart;
            Info.Storage.SmartSlot = aConst.IsSmart ? Info.Random.Range(0, 10) : 0;
            switch (Info.Target.TargetState)
            {
                case Target.TargetStates.WasProjectile:
                    HadTarget = HadTargetState.Projectile;
                    break;
                case Target.TargetStates.IsProjectile:
                    if (Info.Target.Projectile == null)
                    {
                        HadTarget = HadTargetState.None;
                        Info.Target.TargetState = Target.TargetStates.None;
                        PrevTargetPos = Vector3D.Zero;
                        Log.Line($"ProjectileStart had invalid Projectile target state");
                        break;
                    }
                    HadTarget = HadTargetState.Projectile;
                    PrevTargetPos = Info.Target.Projectile.Position;
                    Info.Target.Projectile.Seekers.Add(this);
                    break;
                case Target.TargetStates.IsFake:
                    PrevTargetPos = Info.IsFragment ? PrevTargetPos : Vector3D.Zero;
                    HadTarget = HadTargetState.Fake;
                    break;
                case Target.TargetStates.IsEntity:
                    if (Info.Target.TargetEntity == null)
                    {
                        HadTarget = HadTargetState.None;
                        Info.Target.TargetState = Target.TargetStates.None;
                        PrevTargetPos = Vector3D.Zero;
                        Log.Line($"ProjectileStart had invalid entity target state, isFragment: {Info.IsFragment}");
                        break;
                    }

                    if (aConst.IsDrone)
                    {
                        Info.Storage.DroneMsn = DroneMission.Attack;//TODO handle initial defensive assignment?
                        Info.Storage.DroneStat = Launch;
                        Info.Storage.NavTargetEnt = Info.Target.TargetEntity.GetTopMostParent();
                        Info.Storage.NavTargetBound = Info.Target.TargetEntity.PositionComp.WorldVolume;
                        Info.Storage.ShootTarget = Info.Target;
                        Info.Storage.UsesStrafe = Info.AmmoDef.Fragment.TimedSpawns.PointType == PointTypes.Direct && Info.AmmoDef.Fragment.TimedSpawns.PointAtTarget == false;
                    }

                    PrevTargetPos = Info.Target.TargetEntity.PositionComp.WorldAABB.Center;
                    HadTarget = HadTargetState.Entity;
                    break;
                default:
                    PrevTargetPos = Info.IsFragment ? PrevTargetPos : Vector3D.Zero;
                    break;
            }

            float variance = 0;
            if (aConst.RangeVariance)
            {
                var min = trajectory.RangeVariance.Start;
                var max = trajectory.RangeVariance.End;
                variance = (float)Info.Random.NextDouble() * (max - min) + min;
                Info.MaxTrajectory -= variance;
            }

            MaxTrajectorySqr = Info.MaxTrajectory * Info.MaxTrajectory;

            if (!Vector3D.IsZero(PrevTargetPos))
            {
                LockedTarget = true;
            }
            else
            {
                PrevTargetPos = Position + (AccelDir * Info.MaxTrajectory);
            }

            MoveToAndActivate = LockedTarget && !aConst.IsBeamWeapon && guidance == TrajectoryDef.GuidanceType.TravelTo;

            if (MoveToAndActivate)
            {
                if (!MyUtils.IsZero(PrevTargetPos))
                {
                    PrevTargetPos -= (AccelDir * variance);
                }
                Vector3D.DistanceSquared(ref Info.Origin, ref PrevTargetPos, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = MaxTrajectorySqr;

            PrevTargetVel = Vector3D.Zero;

            var targetSpeed = (float)(!aConst.IsBeamWeapon ? aConst.DesiredProjectileSpeed : Info.MaxTrajectory * MyEngineConstants.UPDATE_STEPS_PER_SECOND);

            if (aConst.SpeedVariance && !aConst.IsBeamWeapon)
            {
                var min = trajectory.SpeedVariance.Start;
                var max = trajectory.SpeedVariance.End;
                var speedVariance = (float)Info.Random.NextDouble() * (max - min) + min;
                DesiredSpeed = targetSpeed + speedVariance;
            }
            else DesiredSpeed = targetSpeed;

            if (aConst.IsSmart && aConst.TargetOffSet && (LockedTarget || Info.Target.TargetState == Target.TargetStates.IsFake))
            {
                OffSetTarget();
            }
            else
            {
                OffsetTarget = Vector3D.Zero;
            }

            Info.Storage.PickTarget = (aConst.OverrideTarget || Info.Weapon.Comp.ModOverride && !LockedTarget) && Info.Target.TargetState != Target.TargetStates.IsFake;
            if (Info.Storage.PickTarget || LockedTarget && !Info.IsFragment) TargetsSeen++;
            if (!Info.IsFragment) StartSpeed = Info.ShooterVel;

            Info.TracerLength = aConst.TracerLength <= Info.MaxTrajectory ? aConst.TracerLength : Info.MaxTrajectory;


            var staticIsInRange = Info.Ai.ClosestStaticSqr * 0.5 < MaxTrajectorySqr;
            var pruneStaticCheck = Info.Ai.ClosestPlanetSqr * 0.5 < MaxTrajectorySqr || Info.Ai.StaticGridInRange;
            PruneQuery = (aConst.DynamicGuidance && pruneStaticCheck) || aConst.FeelsGravity && staticIsInRange || !aConst.DynamicGuidance && !aConst.FeelsGravity && staticIsInRange ? MyEntityQueryType.Both : MyEntityQueryType.Dynamic;

            if (Info.Ai.PlanetSurfaceInRange && Info.Ai.ClosestPlanetSqr <= MaxTrajectorySqr)
            {
                LinePlanetCheck = true;
                PruneQuery = MyEntityQueryType.Both;
            }

            if (aConst.DynamicGuidance && PruneQuery == MyEntityQueryType.Dynamic && staticIsInRange) CheckForNearVoxel(60);

            var accelPerSec = trajectory.AccelPerSec;
            AccelInMetersPerSec = !aConst.AmmoSkipAccel ? accelPerSec : DesiredSpeed;
            var desiredSpeed = (AccelDir * DesiredSpeed);
            var relativeSpeedCap = StartSpeed + desiredSpeed;
            MaxVelocity = relativeSpeedCap;
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            MaxAccelVelocity = (AccelDir * aConst.DeltaVelocityPerTick);
            if (aConst.AmmoSkipAccel)
            {
                Velocity = MaxVelocity;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = StartSpeed + MaxAccelVelocity;

            if (Info.IsFragment)
                Vector3D.Normalize(ref Velocity, out Info.Direction);

            InitalStep = !Info.IsFragment && aConst.AmmoSkipAccel ? desiredSpeed * StepConst : Velocity * StepConst;

            TravelMagnitude = Velocity * StepConst;
            DeaccelRate = aConst.Ewar || aConst.IsMine ? trajectory.DeaccelTime : aConst.IsDrone ? 100: 0;
            State = !aConst.IsBeamWeapon ? ProjectileState.Alive : ProjectileState.OneAndDone;

            if (EnableAv)
            {
                var originDir = !Info.IsFragment ? AccelDir : Info.Direction;
                Info.AvShot = session.Av.AvShotPool.Get();
                Info.AvShot.Init(Info, aConst.IsSmart || aConst.IsDrone, AccelInMetersPerSec * StepConst, MaxSpeed, ref originDir);
                Info.AvShot.SetupSounds(DistanceFromCameraSqr); //Pool initted sounds per Projectile type... this is expensive
                if (aConst.HitParticle && !aConst.IsBeamWeapon || aConst.EndOfLifeAoe && !ammoDef.AreaOfDamage.EndOfLife.NoVisuals)
                {
                    var hitPlayChance = Info.AmmoDef.AmmoGraphics.Particles.Hit.Extras.HitPlayChance;
                    Info.AvShot.HitParticleActive = hitPlayChance >= 1 || hitPlayChance >= MyUtils.GetRandomDouble(0.0f, 1f);
                }
            }

            if (!aConst.PrimeModel && !aConst.TriggerModel) ModelState = EntityState.None;
            else
            {
                if (EnableAv)
                {
                    ModelState = EntityState.Exists;

                    double triggerModelSize = 0;
                    double primeModelSize = 0;
                    if (aConst.TriggerModel) triggerModelSize = Info.AvShot.TriggerEntity.PositionComp.WorldVolume.Radius;
                    if (aConst.PrimeModel) primeModelSize = Info.AvShot.PrimeEntity.PositionComp.WorldVolume.Radius;
                    var largestSize = triggerModelSize > primeModelSize ? triggerModelSize : primeModelSize;

                    Info.AvShot.ModelSphereCurrent.Radius = largestSize * 2;
                }
            }

            if (EnableAv)
            {
                LineOrNotModel = aConst.DrawLine || ModelState == EntityState.None && aConst.AmmoParticle;
                Info.AvShot.ModelOnly = !LineOrNotModel && ModelState == EntityState.Exists;
            }
        }

        #endregion

        #region End

        internal void DestroyProjectile()
        {
            Info.Hit = new Hit { Block = null, Entity = null, SurfaceHit = Position, LastHit = Position, HitVelocity = !Vector3D.IsZero(Gravity) ? Velocity * 0.33f : Velocity, HitTick = Info.Ai.Session.Tick };
            if (EnableAv || Info.AmmoDef.Const.VirtualBeams)
            {
                Info.AvShot.ForceHitParticle = true;
                Info.AvShot.Hit = Info.Hit;
            }

            Intersecting = true;

            State = ProjectileState.Depleted;
        }

        internal void ProjectileClose()
        {
            var aConst = Info.AmmoDef.Const;
            var session = Info.Ai.Session;
            if ((aConst.FragOnEnd && aConst.FragIgnoreArming || Info.Age >= aConst.MinArmingTime && (aConst.FragOnEnd || aConst.FragOnArmed && Info.ObjectsHit > 0)) && Info.SpawnDepth < aConst.FragMaxChildren)
                SpawnShrapnel(false);

            for (int i = 0; i < Watchers.Count; i++) Watchers[i].DeadProjectiles.Add(this);
            Watchers.Clear();

            foreach (var seeker in Seekers)
            {
                if (seeker.Info.Target.Projectile == this)
                    seeker.Info.Target.Reset(session.Tick, Target.States.ProjectileClose);
            }
            Seekers.Clear();

            if (EnableAv && Info.AvShot.ForceHitParticle)
                Info.AvShot.HitEffects(true);

            State = ProjectileState.Dead;

            var detExp = aConst.EndOfLifeAv && (!aConst.ArmOnlyOnHit || Info.ObjectsHit > 0);

            if (EnableAv)
            {
                if (ModelState == EntityState.Exists)
                    ModelState = EntityState.None;
                if (!Info.AvShot.Active)
                    session.Av.AvShotPool.Return(Info.AvShot);
                else Info.AvShot.EndState = new AvClose { EndPos = Position, Dirty = true, DetonateEffect = detExp };
            }
            else if (Info.AmmoDef.Const.VirtualBeams)
            {
                for (int i = 0; i < VrPros.Count; i++)
                {
                    var vp = VrPros[i];
                    if (!vp.AvShot.Active)
                        session.Av.AvShotPool.Return(vp.AvShot);
                    else vp.AvShot.EndState = new AvClose { EndPos = Position, Dirty = true, DetonateEffect = detExp };

                    session.Projectiles.VirtInfoPool.Return(vp);
                }
                VrPros.Clear();
            }

            if (aConst.DynamicGuidance && session.AntiSmartActive)
                DynTrees.UnregisterProjectile(this);

            var target = Info.Target;
            var dmgTotal = Info.DamageDoneAoe + Info.DamageDonePri + Info.DamageDoneShld + Info.DamageDoneProj;

            if (dmgTotal > 0 && Info.Ai?.Construct.RootAi != null && target.CoreEntity != null && !Info.Ai.MarkedForClose && !target.CoreEntity.MarkedForClose)
            {
                var comp = Info.Weapon.Comp;
                var construct = Info.Ai.Construct.RootAi.Construct;
                construct.TotalEffect += dmgTotal;
                comp.TotalEffect += dmgTotal;
                comp.TotalPrimaryEffect += Info.DamageDonePri;
                comp.TotalAOEEffect += Info.DamageDoneAoe;
                comp.TotalShieldEffect += Info.DamageDoneShld;
                comp.TotalProjectileEffect += Info.DamageDoneProj;
                construct.TotalPrimaryEffect += Info.DamageDonePri;
                construct.TotalAoeEffect += Info.DamageDoneAoe;
                construct.TotalShieldEffect += Info.DamageDoneShld;
                construct.TotalProjectileEffect += Info.DamageDoneProj;
            }

            if (aConst.IsDrone) Info.Weapon.LiveDrones--;

            if (aConst.ProjectileSync && session.IsServer)
                SyncStateServerProjectile(ProtoProStateSync.ProSyncState.Dead);

            PruningProxyId = -1;
            HadTarget = HadTargetState.None;
            
            Info.Clean(aConst.IsSmart||aConst.IsDrone);

        }
        #endregion

        #region Smart / Drones
        internal void RunDrone()
        {
            var aConst = Info.AmmoDef.Const;
            var s = Info.Storage;
            var newVel = new Vector3D();
            var parentEnt = Info.Target.CoreParent;

            if (s.DroneStat == Launch)
            {
                if (s.DroneStat == Launch && Info.DistanceTraveled * Info.DistanceTraveled >= aConst.SmartsDelayDistSqr && Info.Target.CoreIsCube && parentEnt != null)//Check for LOS & delaytrack after launch
                {
                    var lineCheck = new LineD(Position, LockedTarget ? PrevTargetPos : Position + (Info.Direction * 10000f));
                    var startTrack = !new MyOrientedBoundingBoxD(parentEnt.PositionComp.LocalAABB, parentEnt.PositionComp.WorldMatrixRef).Intersects(ref lineCheck).HasValue;
                    if (startTrack) s.DroneStat = Transit;
                }
                else if (parentEnt == null || !Info.Target.CoreIsCube)
                {
                    s.DroneStat = Transit;
                }
            }

            if (s.DroneStat != Launch)//Start of main nav after clear of launcher
            {
                var tasks = Info.Weapon.Comp.Data.Repo.Values.State.Tasks;
                var updateTask = tasks.UpdatedTick == Info.Ai.Session.Tick-1;
                var fragProx = Info.AmmoDef.Const.FragProximity;
                var tracking = aConst.DeltaVelocityPerTick <= 0 || (s.DroneStat == Dock || Vector3D.DistanceSquared(Info.Origin, Position) >= aConst.SmartsDelayDistSqr);
                var parentPos = Vector3D.Zero;
                var hasTarget = false;
                s.ShootTarget = Info.Target;
                var hasParent = parentEnt != null && Info.CompSceneVersion == Info.Weapon.Comp.SceneVersion;
                if (hasParent) parentEnt = parentEnt.GetTopMostParent();
                var closestObstacle = Info.Target.ClosestObstacle;
                Info.Target.ClosestObstacle = null;
                var hasObstacle = closestObstacle != parentEnt && closestObstacle != null;
                var hasStrafe = s.UsesStrafe;
                try
                {
                    if (!updateTask)//Top level check for a current target or update to tasks
                    {
                        switch (HadTarget)//Internal drone target reassignment
                        {
                            case HadTargetState.Entity:
                                if (s.ShootTarget.TargetEntity != null && !s.ShootTarget.TargetEntity.MarkedForClose)
                                {
                                    hasTarget = true;
                                }
                                else
                                {
                                    NewTarget();
                                    if(Info.Target.TargetEntity!=null)
                                    {
                                        s.NavTargetEnt = Info.Target.TargetEntity.GetTopMostParent();
                                        s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;
                                        hasTarget = true;
                                    }
                                    else
                                    {
                                        hasTarget = false;
                                    }

                                }
                                break;
                            case HadTargetState.Projectile: //TODO evaluate whether TargetBound should remain unchanged (ie, keep orbiting assigned target but shoot at projectile)
                                if (s.ShootTarget.TargetState == Target.TargetStates.IsProjectile || NewTarget())
                                {
                                    //s.NavTargetBound = new BoundingSphereD(target.Projectile.Position, fragProx * 0.5f);
                                    hasTarget = false;//Temp set to false, need to hash out anti-projectile behavior
                                }
                                else
                                {
                                    //TODO evaluate if this is needed, IE do nothing (keep prior orbit behavior)
                                }
                                break;
                            case HadTargetState.Fake:
                                if (Info.Storage.DummyTargets != null)
                                {
                                    var fakeTarget = Info.Storage.DummyTargets.PaintedTarget.EntityId != 0 ? Info.Storage.DummyTargets.PaintedTarget : Info.Storage.DummyTargets.ManualTarget;
                                    if (fakeTarget == Info.Storage.DummyTargets.PaintedTarget)
                                    {
                                        MyEntities.TryGetEntityById(fakeTarget.EntityId, out s.NavTargetEnt);
                                        if (s.NavTargetEnt.PositionComp.WorldVolume.Radius <= 0)
                                        {
                                            NewTarget();
                                        }
                                    }
                                    else
                                    {
                                        s.NavTargetBound = new BoundingSphereD(fakeTarget.FakeInfo.WorldPosition, fragProx * 0.5f);
                                        s.TaskPosition = fakeTarget.FakeInfo.WorldPosition;
                                        hasTarget = true;
                                    }
                                }
                                else
                                    NewTarget();
                                break;
                        }
                        if (s.NavTargetEnt != null && hasTarget) s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;//Refresh position info
                        //Logic to handle loss of target and reassigment to come home
                        if (!hasTarget && hasParent && s.DroneMsn == DroneMission.Attack)
                        {
                                s.DroneMsn = DroneMission.Defend;//Try to return to parent in defensive state
                                s.NavTargetBound = parentEnt.PositionComp.WorldVolume;
                                s.NavTargetEnt = parentEnt;
                        }
                        else if (s.DroneMsn == DroneMission.Rtb || s.DroneMsn == DroneMission.Defend)
                        {
                            if (hasParent && s.DroneMsn == DroneMission.Rtb || tasks.FriendId == 0)
                            {
                                s.NavTargetBound = parentEnt.PositionComp.WorldVolume;
                                s.NavTargetEnt = parentEnt;
                            }
                            else if (tasks.Friend != null && s.DroneMsn != DroneMission.Rtb && tasks.Friend != null)//If all else fails, try to protect a friendly
                            {
                                s.NavTargetBound = tasks.Friend.PositionComp.WorldVolume;
                                s.NavTargetEnt = tasks.Friend;
                            }
                            else
                            {
                                Log.Line($"Orphaned drone w/ no parent or friend ent.  {Info.AmmoDef.AmmoRound} Msn:{s.DroneMsn} Stat:{s.DroneStat}");
                            }

                        }
                        else if (!hasTarget && !hasParent && s.DroneMsn == DroneMission.Attack)
                        {
                            Log.Line($"Orphaned drone w/ no target, no parent or friend ent. {Info.AmmoDef.AmmoRound}  Msn:{s.DroneMsn} Stat:{s.DroneStat} Nav Target:{s.NavTargetEnt} TargetDeckLen:{Info.Target.TargetDeck.Length}");
                        }
                    }
                    else
                    {
                        switch(tasks.Task)
                        {
                            case ProtoWeaponCompTasks.Tasks.Attack:
                                s.DroneMsn = DroneMission.Attack;
                                s.NavTargetEnt = tasks.Enemy;
                                s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;
                                var tTargetDist = Vector3D.Distance(Position, tasks.Enemy.PositionComp.WorldVolume.Center);
                                s.ShootTarget.Set(tasks.Enemy, tasks.Enemy.PositionComp.WorldVolume.Center, tTargetDist, tTargetDist, tasks.EnemyId);
                                s.IsFriend = false;
                                break;
                            case ProtoWeaponCompTasks.Tasks.Defend:
                                s.DroneMsn = DroneMission.Defend;
                                s.NavTargetEnt = tasks.Friend;
                                s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;
                                s.IsFriend = true;
                                break;
                            case ProtoWeaponCompTasks.Tasks.Screen:
                                if (hasParent)
                                {
                                    s.DroneMsn = DroneMission.Defend;
                                    s.NavTargetEnt = parentEnt;
                                    s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;
                                    s.IsFriend = false;
                                }
                                else
                                    Log.Line($"Drone Screen failed, no parent");
                                break;
                            case ProtoWeaponCompTasks.Tasks.Recall:
                                if (hasParent)
                                {
                                    s.DroneMsn = DroneMission.Rtb;
                                    s.NavTargetEnt = parentEnt;
                                    s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;
                                    s.IsFriend = false;
                                }
                                else
                                    Log.Line($"Drone Recall failed, no parent");
                                break;
                            case ProtoWeaponCompTasks.Tasks.RoamAtPoint:
                                s.DroneMsn = DroneMission.Defend;
                                s.NavTargetEnt = null;
                                s.NavTargetBound = new BoundingSphereD(tasks.Position, fragProx * 0.5f);
                                s.IsFriend = false;
                                break;
                            case ProtoWeaponCompTasks.Tasks.None:
                                Log.Line($"Drone has no task  enemy-{tasks.Enemy} friend-{tasks.Friend} parent?{hasParent} target?{hasTarget}");
                                break;
                            default:
                                Log.Line($"Drone defaulted on task  enemy-{tasks.Enemy} friend-{tasks.Friend} parent?{hasParent} target?{hasTarget}");
                                break;
                        }
                        s.DroneStat = Transit;
                    }

                    //Hard break, everything below sorts out the best navigation action to conduct based on the drone position and target/status/mission info from above

                    //General use vars
                    var targetSphere = s.NavTargetBound;
                    var orbitSphere = targetSphere; //desired orbit dist
                    var orbitSphereFar = orbitSphere; //Indicates start of approach
                    var orbitSphereClose = targetSphere; //"Too close" or collision imminent
                    var hasKamikaze = Info.AmmoDef.AreaOfDamage.ByBlockHit.Enable || (Info.AmmoDef.AreaOfDamage.EndOfLife.Enable && Info.Age >= Info.AmmoDef.AreaOfDamage.EndOfLife.MinArmingTime); //check for explosive payload on drone
                    var maxLife = aConst.MaxLifeTime;
                    switch (s.DroneMsn)
                    {
                        case DroneMission.Attack:

                            orbitSphere.Radius += fragProx;
                            orbitSphereFar.Radius += fragProx + AccelInMetersPerSec + MaxSpeed; //first whack at dynamic setting   
                            orbitSphereClose.Radius += MaxSpeed * 0.3f + Info.AmmoDef.Shape.Diameter; //Magic number, needs logical work?
                            if (hasObstacle && orbitSphereClose.Contains(closestObstacle.PositionComp.GetPosition()) != ContainmentType.Contains && s.DroneStat != Kamikaze)
                            {
                                orbitSphereClose = closestObstacle.PositionComp.WorldVolume;
                                orbitSphereClose.Radius = closestObstacle.PositionComp.WorldVolume.Radius + MaxSpeed * 0.3f;
                                s.DroneStat = Escape;
                                break;
                            }

                            if (s.DroneStat != Transit && orbitSphereFar.Contains(Position) == ContainmentType.Disjoint)
                            {
                                s.DroneStat = Transit;
                                break;
                            }
                            if (s.DroneStat != Kamikaze && s.DroneStat != Return && s.DroneStat != Escape)
                            {
                                if (orbitSphere.Contains(Position) != ContainmentType.Disjoint)
                                {
                                    if (orbitSphereClose.Contains(Position) != ContainmentType.Disjoint)
                                    {
                                        s.DroneStat = Escape;
                                    }
                                    else if (s.DroneStat!= Escape)
                                    {
                                        if (!hasStrafe)
                                        {
                                            s.DroneStat= Orbit;
                                        }

                                        if (hasStrafe)
                                        {
                                            var fragInterval = aConst.FragInterval;
                                            var fragGroupDelay = aConst.FragGroupDelay;
                                            var timeSinceLastFrag = Info.Age - Info.LastFragTime;

                                            if (fragGroupDelay == 0 && timeSinceLastFrag >= fragInterval)
                                                s.DroneStat= Strafe;//TODO incorporate group delays
                                            else if (fragGroupDelay > 0 && (timeSinceLastFrag >= fragGroupDelay || timeSinceLastFrag <= fragInterval))
                                                s.DroneStat= Strafe;
                                            else s.DroneStat= Orbit;
                                        }
                                    }
                                }
                                else if (s.DroneStat== Transit && orbitSphereFar.Contains(Position) != ContainmentType.Disjoint)
                                {
                                    s.DroneStat= Approach;
                                }
                            }
                            else if (s.DroneStat== Escape)
                            {
                                if (orbitSphere.Contains(Position) == ContainmentType.Disjoint)
                                    s.DroneStat= Orbit;
                            }

                            if ((hasKamikaze || !hasParent) && s.DroneStat!= Kamikaze && maxLife > 0)//Parenthesis for everyone!
                            {
                                var kamiFlightTime = orbitSphere.Radius / MaxSpeed * 60 * 1.05; //time needed for final dive into target
                                if (maxLife - Info.Age <= kamiFlightTime || (Info.Frags >= aConst.MaxFrags))
                                {
                                    s.DroneStat= Kamikaze;
                                }
                            }
                            else if (!hasKamikaze && s.NavTargetEnt != parentEnt && hasParent)
                            {
                                parentPos = Info.Target.CoreEntity.PositionComp.WorldAABB.Center;
                                if (parentPos != Vector3D.Zero && s.DroneStat!= Return)
                                {
                                    var rtbFlightTime = Vector3D.Distance(Position, parentPos) / MaxSpeed * 60 * 1.1d;//added multiplier to ensure final docking time?
                                    if ((maxLife > 0 && maxLife - Info.Age <= rtbFlightTime) || (Info.Frags >= aConst.MaxFrags))
                                    {
                                        var rayTestPath = new RayD(Position, Vector3D.Normalize(parentPos - Position));//Check for clear LOS home
                                        if (rayTestPath.Intersects(orbitSphereClose) == null)
                                        {
                                            s.DroneMsn = DroneMission.Rtb;
                                            s.DroneStat= Transit;
                                        }
                                    }
                                }
                            }
                            break;
                        case DroneMission.Defend:
                            orbitSphere.Radius += fragProx / 2;
                            orbitSphereFar.Radius += AccelInMetersPerSec + MaxSpeed;
                            orbitSphereClose.Radius += MaxSpeed * 0.3f + Info.AmmoDef.Shape.Diameter;
                            if (hasObstacle)
                            {
                                orbitSphereClose = closestObstacle.PositionComp.WorldVolume;
                                orbitSphereClose.Radius = closestObstacle.PositionComp.WorldVolume.Radius + MaxSpeed * 0.3f;
                                s.DroneStat= Escape;
                                break;
                            }
                            else if (s.DroneStat== Escape) s.DroneStat= Transit;

                            if (s.DroneStat!= Transit && orbitSphereFar.Contains(Position) == ContainmentType.Disjoint)
                            {
                                s.DroneStat= Transit;
                                break;
                            }

                            if (s.DroneStat!= Transit)
                            {
                                if (orbitSphere.Contains(Position) != ContainmentType.Disjoint)
                                {
                                    if (orbitSphereClose.Contains(Position) != ContainmentType.Disjoint)
                                    {
                                        s.DroneStat= Escape;
                                    }
                                    else
                                    {
                                        s.DroneStat= Orbit;
                                    }
                                }
                            }
                            else if (orbitSphereFar.Contains(Position) != ContainmentType.Disjoint && (s.DroneStat== Transit || s.DroneStat== Orbit))
                            {
                                s.DroneStat= Approach;
                            }

                            if (hasParent) parentPos = Info.Target.CoreEntity.PositionComp.WorldAABB.Center;
                            if (parentPos != Vector3D.Zero && s.DroneStat != Return && !hasKamikaze)//TODO kamikaze return suppressed to prevent damaging parent, until docking mechanism developed
                            {
                                var rtbFlightTime = Vector3D.Distance(Position, parentPos) / MaxSpeed * 60 * 1.05d;//added multiplier to ensure final docking time
                                if ((maxLife > 0 && maxLife - Info.Age <= rtbFlightTime) || (Info.Frags >= Info.AmmoDef.Fragment.TimedSpawns.MaxSpawns))
                                {
                                    if (s.NavTargetEnt != parentEnt)
                                    {
                                        var rayTestPath = new RayD(Position, Vector3D.Normalize(parentPos - Position));//Check for clear LOS home
                                        if (rayTestPath.Intersects(orbitSphereClose) == null)
                                        {
                                            s.DroneMsn = DroneMission.Rtb;
                                            s.DroneStat = Transit;
                                        }
                                    }
                                    else//already orbiting parent, head in to dock
                                    {
                                        s.DroneMsn = DroneMission.Rtb;
                                        s.DroneStat = Transit;
                                    }
                                }
                            }

                            break;
                        case DroneMission.Rtb:

                            orbitSphere.Radius += MaxSpeed;
                            orbitSphereFar.Radius += MaxSpeed * 2;
                            orbitSphereClose.Radius = targetSphere.Radius;

                            if (hasObstacle && s.DroneStat!= Dock)
                            {
                                orbitSphereClose = closestObstacle.PositionComp.WorldVolume;
                                orbitSphereClose.Radius = closestObstacle.PositionComp.WorldVolume.Radius + MaxSpeed * 0.3f;
                                s.DroneStat= Escape;
                                break;
                            }
                            else if (s.DroneStat== Escape) s.DroneStat= Transit;

                            if (s.DroneStat!= Return && s.DroneStat!= Dock)
                            {
                                if (orbitSphere.Contains(Position) != ContainmentType.Disjoint)
                                {
                                    s.DroneStat = orbitSphereClose.Contains(Position) != ContainmentType.Disjoint ? Escape : Return;
                                }
                                else if (orbitSphereFar.Contains(Position) != ContainmentType.Disjoint && (s.DroneStat== Transit || s.DroneStat== Orbit))
                                {
                                    s.DroneStat= Approach;
                                }
                            }

                            //if (s.DroneStat== Orbit || s.DroneStat== Return || s.DroneStat== Dock) 
                            //    Info.Age -= 1;
                            break;

                        default:
                            break;
                    }

                    //debug line draw stuff
                    if (Info.Weapon.System.WConst.DebugMode && !Info.Weapon.System.Session.DedicatedServer)
                    {
                        if (orbitSphere.Center != Vector3D.Zero)
                        {
                            var debugLine = new LineD(Position, orbitSphere.Center);
                            if (s.DroneStat == Transit) DsDebugDraw.DrawLine(debugLine, Color.Blue, 0.5f);
                            if (s.DroneStat == Approach) DsDebugDraw.DrawLine(debugLine, Color.Cyan, 0.5f);
                            if (s.DroneStat == Kamikaze) DsDebugDraw.DrawLine(debugLine, Color.White, 0.5f);
                            if (s.DroneStat == Return) DsDebugDraw.DrawLine(debugLine, Color.Yellow, 0.5f);
                            if (s.DroneStat == Dock) DsDebugDraw.DrawLine(debugLine, Color.Purple, 0.5f);
                            if (s.DroneStat == Strafe) DsDebugDraw.DrawLine(debugLine, Color.Pink, 0.5f);
                            if (s.DroneStat == Escape) DsDebugDraw.DrawLine(debugLine, Color.Red, 0.5f);
                            if (s.DroneStat == Orbit) DsDebugDraw.DrawLine(debugLine, Color.Green, 0.5f);
                        }

                        switch (s.DroneMsn)
                        {
                            case DroneMission.Attack:
                                DsDebugDraw.DrawSphere(new BoundingSphereD(Position, 10), Color.Red);
                                break;
                            case DroneMission.Defend:
                                DsDebugDraw.DrawSphere(new BoundingSphereD(Position, 10), Color.Blue);
                                break;
                            case DroneMission.Rtb:
                                DsDebugDraw.DrawSphere(new BoundingSphereD(Position, 10), Color.Green);
                                break;
                        }
                    }

                    if (tracking && s.DroneMsn != DroneMission.Rtb)
                    {
                        var validEntity = Info.Target.TargetState == Target.TargetStates.IsEntity && !Info.Target.TargetEntity.MarkedForClose;
                        var timeSlot = (Info.Age + s.SmartSlot) % 30 == 0;
                        var hadTarget = HadTarget != HadTargetState.None;
                        var overMaxTargets = hadTarget && TargetsSeen > aConst.MaxTargets && aConst.MaxTargets != 0;
                        var fake = Info.Target.TargetState == Target.TargetStates.IsFake;
                        var validTarget = fake || Info.Target.TargetState == Target.TargetStates.IsProjectile || validEntity && !overMaxTargets;
                        var seekFirstTarget = !hadTarget && !validTarget && s.PickTarget && (Info.Age > 120 && timeSlot || Info.Age % 30 == 0 && Info.IsFragment);
                        var gaveUpChase = !fake && Info.Age - s.ChaseAge > aConst.MaxChaseTime && hadTarget;
                        var isZombie = aConst.CanZombie && hadTarget && !fake && !validTarget && s.ZombieLifeTime > 0 && (s.ZombieLifeTime + s.SmartSlot) % 30 == 0;
                        var seekNewTarget = timeSlot && hadTarget && !validEntity && !overMaxTargets;
                        var needsTarget = (s.PickTarget && timeSlot || seekNewTarget || gaveUpChase && validTarget || isZombie || seekFirstTarget);

                        if (needsTarget && NewTarget() || validTarget)
                        {
                            TrackSmartTarget(fake);
                        }
                        else if (!SmartRoam())
                            return;
                        ComputeSmartVelocity(ref orbitSphere, ref orbitSphereClose, ref targetSphere, ref parentPos, out newVel);
                    }
                    else if (s.DroneMsn == DroneMission.Rtb)
                    {
                        ComputeSmartVelocity(ref orbitSphere, ref orbitSphereClose, ref targetSphere, ref parentPos, out newVel);
                    }

                }
                catch (Exception ex) {Log.Line($"Exception in RunDrones: {Info.AmmoDef.AmmoRound} {ex}", null, true); }
                UpdateSmartVelocity(newVel, tracking);
            }
            else
            {
                newVel = Velocity + MaxAccelVelocity;
                VelocityLengthSqr = newVel.LengthSquared();

                if (VelocityLengthSqr > MaxSpeedSqr) newVel = Info.Direction * 1.05f * MaxSpeed;
                
                Velocity = newVel;
            }
        }

        private void OffsetSmartVelocity(ref Vector3D commandedAccel)
        {
            var smarts = Info.AmmoDef.Trajectory.Smarts;
            var offsetTime = smarts.OffsetTime;
            var revCmdAccel = -commandedAccel / AccelInMetersPerSec;
            var revOffsetDir = MyUtils.IsZero(OffsetDir.X - revCmdAccel.X, 1E-03f) && MyUtils.IsZero(OffsetDir.Y - revCmdAccel.Y, 1E-03f) && MyUtils.IsZero(OffsetDir.Z - revCmdAccel.Z, 1E-03f);

            if (Info.Age % offsetTime == 0 || revOffsetDir)
            {

                double angle = Info.Random.NextDouble() * MathHelper.TwoPi;
                var up = Vector3D.CalculatePerpendicularVector(Info.Direction);
                var right = Vector3D.Cross(Info.Direction, up);
                OffsetDir = Math.Sin(angle) * up + Math.Cos(angle) * right;
                OffsetDir *= smarts.OffsetRatio;
            }

            commandedAccel += AccelInMetersPerSec * OffsetDir;
            commandedAccel = Vector3D.Normalize(commandedAccel) * AccelInMetersPerSec;
        }

        private void ComputeSmartVelocity(ref BoundingSphereD orbitSphere, ref BoundingSphereD orbitSphereClose, ref BoundingSphereD targetSphere, ref Vector3D parentPos, out Vector3D newVel)
        {
            var s = Info.Storage;
            var smarts = Info.AmmoDef.Trajectory.Smarts;
            var droneNavTarget = Vector3D.Zero;
            var parentCubePos = Info.Target.CoreCube.PositionComp.GetPosition();
            var parentCubeOrientation = Info.Target.CoreCube.PositionComp.GetOrientation();
            var droneSize = Math.Max(Info.AmmoDef.Shape.Diameter,5);//Minimum drone "size" clamped to 5m for nav purposes, prevents chasing tiny points in space
            switch (s.DroneStat)
            {
                case Transit:
                    droneNavTarget = Vector3D.Normalize(targetSphere.Center - Position);
                    break;
                case Approach:
                    if (s.DroneMsn == DroneMission.Rtb)//Check for LOS to docking target
                    {
                        var returnTargetTest = new Vector3D(parentCubePos + parentCubeOrientation.Forward * orbitSphere.Radius);
                        var droneNavTargetAim = Vector3D.Normalize(returnTargetTest - Position);
                        var testPathRayCheck = new RayD(returnTargetTest, -droneNavTargetAim);//Ray looking out from dock approach point

                        if (testPathRayCheck.Intersects(orbitSphereClose)==null)
                        {                            
                            s.DroneStat= Return;
                            break;
                        }
                    }
                    //tangential tomfoolery
                    var lineToCenter = new LineD(Position, orbitSphere.Center);
                    var distToCenter = lineToCenter.Length; 
                    var radius = orbitSphere.Radius * 0.99;//Multiplier to ensure drone doesn't get "stuck" on periphery
                    var centerOffset = distToCenter - Math.Sqrt((distToCenter * distToCenter) - (radius * radius));//TODO Chase down the boogey-NaN here
                    var offsetDist = Math.Sqrt((radius * radius) - (centerOffset * centerOffset));
                    var offsetPoint = new Vector3D(orbitSphere.Center + (centerOffset * -lineToCenter.Direction));
                    var angleQuat = Vector3D.CalculatePerpendicularVector(lineToCenter.Direction); //TODO placeholder for a possible rand-rotated quat.  Should be 90*, rand*, 0* 
                    var tangentPoint = new Vector3D(offsetPoint + offsetDist * angleQuat);
                    droneNavTarget = Vector3D.Normalize(tangentPoint - Position);
                    if (double.IsNaN(droneNavTarget.X)) droneNavTarget = Info.Direction; //Error catch
                    break;

                case Orbit://Orbit & shoot behavior
                    var insideOrbitSphere = new BoundingSphereD(orbitSphere.Center, orbitSphere.Radius * 0.90);
                    if (insideOrbitSphere.Contains(Position) != ContainmentType.Disjoint)
                    {
                        droneNavTarget = Vector3D.Normalize(Info.Direction + new Vector3D(0,0.5,0));//Strafe or too far inside sphere recovery
                    }
                    else
                    {
                        var noseOffset = new Vector3D(Position + (Info.Direction * (AccelInMetersPerSec)));
                        double length;
                        Vector3D.Distance(ref orbitSphere.Center, ref noseOffset, out length);
                        var dir = (noseOffset - orbitSphere.Center) / length;
                        var deltaDist = length - orbitSphere.Radius * 0.95; //0.95 modifier for hysterisis, keeps target inside dronesphere
                        var navPoint = noseOffset + (-dir * deltaDist);
                        droneNavTarget = Vector3D.Normalize(navPoint - Position);
                    }
                    break;
               
                case Strafe:
                    droneNavTarget = Vector3D.Normalize(PrevTargetPos - Position);
                    break;
                case Escape:
                    var metersInSideOrbit = MyUtils.GetSmallestDistanceToSphere(ref Position, ref orbitSphereClose);
                    if (metersInSideOrbit < 0)
                    {
                        var futurePos = (Position + (TravelMagnitude * Math.Abs(metersInSideOrbit*0.5)));
                        var dirToFuturePos = Vector3D.Normalize(futurePos - orbitSphereClose.Center);
                        var futureSurfacePos = orbitSphereClose.Center + (dirToFuturePos * orbitSphereClose.Radius);
                        droneNavTarget = Vector3D.Normalize(futureSurfacePos - Position);
                    }
                    else
                    {
                        droneNavTarget = Info.Direction;
                    }
                    break;

                case Kamikaze:
                    droneNavTarget = Vector3D.Normalize(PrevTargetPos - Position);
                    break;
                case Return:
                    var returnTarget = new Vector3D(parentCubePos + parentCubeOrientation.Forward * orbitSphere.Radius);
                    droneNavTarget = Vector3D.Normalize(returnTarget - Position);
                    DeaccelRate = 30;
                    if (Vector3D.Distance(Position, returnTarget) <= droneSize) s.DroneStat= Dock;
                    break;
                case Dock: //This is ugly and I hate it...
                    var maxLife = Info.AmmoDef.Const.MaxLifeTime;
                    var sphereTarget = new Vector3D(parentCubePos + parentCubeOrientation.Forward * (orbitSphereClose.Radius+MaxSpeed/2));

                    if (Vector3D.Distance(sphereTarget, Position) >= droneSize)
                    {
                        if (DeaccelRate >= 25)//Final Approach
                        {
                            droneNavTarget = Vector3D.Normalize(sphereTarget - Position);
                            DeaccelRate = 25;
                        }

                    }
                    else if (DeaccelRate >=25)
                    {
                        DeaccelRate = 15;
                    }

                    if (DeaccelRate <=15)
                    {
                        if (Vector3D.Distance(parentCubePos, Position) >= droneSize)
                        {
                            droneNavTarget = Vector3D.Normalize(parentCubePos - Position);
                        }
                        else//docked TODO despawn and restock ammo?
                        {
                            Info.Age = int.MaxValue;
                        }
                    }
                    break;
            }

            
            var missileToTarget = droneNavTarget;
            var relativeVelocity = PrevTargetVel - Velocity;
            var normalMissileAcceleration = (relativeVelocity - (relativeVelocity.Dot(missileToTarget) * missileToTarget)) * smarts.Aggressiveness;

            Vector3D commandedAccel;
            if (Vector3D.IsZero(normalMissileAcceleration)) 
            {
                commandedAccel = (missileToTarget * AccelInMetersPerSec);
            }
            else
            {
                var maxLateralThrust = AccelInMetersPerSec * Math.Min(1, Math.Max(0, Info.AmmoDef.Const.MaxLateralThrust));
                if (normalMissileAcceleration.LengthSquared() > maxLateralThrust * maxLateralThrust)
                {
                    Vector3D.Normalize(ref normalMissileAcceleration, out normalMissileAcceleration);
                    normalMissileAcceleration *= maxLateralThrust;
                }
                commandedAccel = Math.Sqrt(Math.Max(0, AccelInMetersPerSec * AccelInMetersPerSec - normalMissileAcceleration.LengthSquared())) * missileToTarget + normalMissileAcceleration;
            }
            if (smarts.OffsetTime > 0 && s.DroneStat!= Strafe && s.DroneStat!=Return && s.DroneStat!= Dock) // suppress offsets when strafing or docking
                OffsetSmartVelocity(ref commandedAccel);

            newVel = Velocity + (commandedAccel * StepConst);
            var accelDir = commandedAccel / AccelInMetersPerSec;

            AccelDir = accelDir;
            Vector3D.Normalize(ref newVel, out Info.Direction);
        }

        private bool SmartRoam()
        {
            var smarts = Info.AmmoDef.Trajectory.Smarts;
            var roam = smarts.Roam;
            var hadTaret = HadTarget != HadTargetState.None;
            PrevTargetPos = roam ? PrevTargetPos : Position + (Info.Direction * Info.MaxTrajectory);

            if (Info.Storage.ZombieLifeTime++ > Info.AmmoDef.Const.TargetLossTime && !smarts.KeepAliveAfterTargetLoss && (smarts.NoTargetExpire || hadTaret))
            {
                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                EarlyEnd = true;
            }

            if (roam && Info.Age - Info.Storage.LastOffsetTime > 300 && hadTaret)
            {

                double dist;
                Vector3D.DistanceSquared(ref Position, ref PrevTargetPos, out dist);
                if (dist < Info.AmmoDef.Const.SmartOffsetSqr + VelocityLengthSqr && Vector3.Dot(Info.Direction, Position - PrevTargetPos) > 0)
                {
                    OffSetTarget(true);
                    PrevTargetPos += OffsetTarget;
                }
            }
            else if (Info.Storage.MineSeeking)
            {
                ResetMine();
                return false;
            }

            return true;
        }

        private void UpdateSmartVelocity(Vector3D newVel, bool tracking)
        {
            if (!tracking)
                newVel = Velocity += (Info.Direction * Info.AmmoDef.Const.DeltaVelocityPerTick);
            VelocityLengthSqr = newVel.LengthSquared();

            if (VelocityLengthSqr > MaxSpeedSqr || (DeaccelRate <100 && Info.AmmoDef.Const.IsDrone)) newVel = Info.Direction * MaxSpeed*DeaccelRate/100;

            Velocity = newVel;
        }

        private void TrackSmartTarget(bool fake)
        {
            var aConst = Info.AmmoDef.Const;
            if (Info.Storage.ZombieLifeTime > 0)
            {
                Info.Storage.ZombieLifeTime = 0;
                OffSetTarget();
            }

            var targetPos = Vector3D.Zero;

            Ai.FakeTarget.FakeWorldTargetInfo fakeTargetInfo = null;
            MyPhysicsComponentBase physics = null;
            if (fake && Info.Storage.DummyTargets != null)
            {
                var fakeTarget = Info.Storage.DummyTargets.PaintedTarget.EntityId != 0 ? Info.Storage.DummyTargets.PaintedTarget : Info.Storage.DummyTargets.ManualTarget;
                fakeTargetInfo = fakeTarget.LastInfoTick != Info.Ai.Session.Tick ? fakeTarget.GetFakeTargetInfo(Info.Ai) : fakeTarget.FakeInfo;
                targetPos = fakeTargetInfo.WorldPosition;
                HadTarget = HadTargetState.Fake;
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsProjectile)
            {
                targetPos = Info.Target.Projectile.Position;
                HadTarget = HadTargetState.Projectile;
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsEntity)
            {
                targetPos = Info.Target.TargetEntity.PositionComp.WorldAABB.Center;
                HadTarget = HadTargetState.Entity;
                physics = Info.Target.TargetEntity.Physics;

            }
            else
                HadTarget = HadTargetState.Other;

            if (aConst.TargetOffSet && Info.Storage.WasTracking)
            {

                if (Info.Age - Info.Storage.LastOffsetTime > 300)
                {

                    double dist;
                    Vector3D.DistanceSquared(ref Position, ref targetPos, out dist);
                    if (dist < aConst.SmartOffsetSqr + VelocityLengthSqr && Vector3.Dot(Info.Direction, Position - targetPos) > 0)
                        OffSetTarget();
                }
                targetPos += OffsetTarget;
            }

            PrevTargetPos = targetPos;

            var tVel = Vector3.Zero;
            if (fake && fakeTargetInfo != null)
            {
                tVel = fakeTargetInfo.LinearVelocity;
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsProjectile)
            {
                tVel = Info.Target.Projectile.Velocity;
            }
            else if (physics != null)
            {
                tVel = physics.LinearVelocity;
            }

            if (aConst.TargetLossDegree > 0 && Vector3D.DistanceSquared(Info.Origin, Position) >= aConst.SmartsDelayDistSqr)
                SmartTargetLoss(targetPos);

            PrevTargetVel = tVel;
        }

        private void SmartTargetLoss(Vector3D targetPos)
        {

            if (Info.Storage.WasTracking && (Info.Ai.Session.Tick20 || Vector3.Dot(Info.Direction, Position - targetPos) > 0) || !Info.Storage.WasTracking)
            {
                var targetDir = -Info.Direction;
                var refDir = Vector3D.Normalize(Position - targetPos);
                if (!MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref refDir, Info.AmmoDef.Const.TargetLossDegree))
                {
                    if (Info.Storage.WasTracking)
                        Info.Storage.PickTarget = true;
                }
                else if (!Info.Storage.WasTracking)
                    Info.Storage.WasTracking = true;
            }
        }

        internal void RunSmart()
        {
            Vector3D newVel;
            var aConst = Info.AmmoDef.Const;
            var s = Info.Storage;
            var startTrack = Info.Storage.SmartReady || Info.Target.CoreParent == null || Info.Target.CoreParent.MarkedForClose;

            if (!startTrack && Info.DistanceTraveled * Info.DistanceTraveled >= aConst.SmartsDelayDistSqr) {
                var lineCheck = new LineD(Position, LockedTarget ? PrevTargetPos : Position + (Info.Direction * 10000f));
                startTrack = !new MyOrientedBoundingBoxD(Info.Target.CoreParent.PositionComp.LocalAABB, Info.Target.CoreParent.PositionComp.WorldMatrixRef).Intersects(ref lineCheck).HasValue;
            }

            if (startTrack)
            {
                s.SmartReady = true;
                var smarts = Info.AmmoDef.Trajectory.Smarts;
                var fake = Info.Target.TargetState == Target.TargetStates.IsFake;
                var hadTarget = HadTarget != HadTargetState.None;

                var gaveUpChase = !fake && Info.Age - s.ChaseAge > aConst.MaxChaseTime && hadTarget;
                var overMaxTargets = hadTarget && TargetsSeen > aConst.MaxTargets && aConst.MaxTargets != 0;
                var validEntity = Info.Target.TargetState == Target.TargetStates.IsEntity && !Info.Target.TargetEntity.MarkedForClose;
                var validTarget = fake || Info.Target.TargetState == Target.TargetStates.IsProjectile || validEntity && !overMaxTargets;
                var checkTime = HadTarget != HadTargetState.Projectile ? 30 : 10;
                var isZombie = aConst.CanZombie && hadTarget && !fake && !validTarget && s.ZombieLifeTime > 0 && (s.ZombieLifeTime + s.SmartSlot) % checkTime == 0;
                var timeSlot = (Info.Age + s.SmartSlot) % checkTime == 0;
                var seekNewTarget = timeSlot && hadTarget && !validTarget && !overMaxTargets;
                var seekFirstTarget = !hadTarget && !validTarget && s.PickTarget && (Info.Age > 120 && timeSlot || Info.Age % checkTime == 0 && Info.IsFragment);

                if ((s.PickTarget && timeSlot || seekNewTarget || gaveUpChase && validTarget || isZombie || seekFirstTarget) && NewTarget() || validTarget)
                {

                    if (s.ZombieLifeTime > 0)
                    {
                        s.ZombieLifeTime = 0;
                        OffSetTarget();
                    }
                    var targetPos = Vector3D.Zero;

                    Ai.FakeTarget.FakeWorldTargetInfo fakeTargetInfo = null;
                    if (fake && s.DummyTargets != null)
                    {
                        var fakeTarget = s.DummyTargets.PaintedTarget.EntityId != 0 ? s.DummyTargets.PaintedTarget : s.DummyTargets.ManualTarget;
                        fakeTargetInfo = fakeTarget.LastInfoTick != Info.Ai.Session.Tick ? fakeTarget.GetFakeTargetInfo(Info.Ai) : fakeTarget.FakeInfo;
                        targetPos = fakeTargetInfo.WorldPosition;
                        HadTarget = HadTargetState.Fake;
                    }
                    else if (Info.Target.TargetState == Target.TargetStates.IsProjectile)
                    {
                        targetPos = Info.Target.Projectile.Position;
                        HadTarget = HadTargetState.Projectile;
                    }
                    else if (Info.Target.TargetState == Target.TargetStates.IsEntity)
                    {
                        targetPos = Info.Target.TargetEntity.PositionComp.WorldAABB.Center;
                        HadTarget = HadTargetState.Entity;
                    }
                    else
                        HadTarget = HadTargetState.Other;

                    if (aConst.TargetOffSet && s.WasTracking)
                    {
                        if (Info.Age - s.LastOffsetTime > 300)
                        {
                            double dist;
                            Vector3D.DistanceSquared(ref Position, ref targetPos, out dist);
                            if (dist < aConst.SmartOffsetSqr + VelocityLengthSqr && Vector3.Dot(Info.Direction, Position - targetPos) > 0)
                                OffSetTarget();
                        }
                        targetPos += OffsetTarget;
                    }

                    PrevTargetPos = targetPos;

                    var physics = Info.Target.TargetEntity?.Physics ?? Info.Target.TargetEntity?.Parent?.Physics;

                    var tVel = Vector3.Zero;
                    if (fake && fakeTargetInfo != null) tVel = fakeTargetInfo.LinearVelocity;
                    else if (Info.Target.TargetState == Target.TargetStates.IsProjectile) tVel = Info.Target.Projectile.Velocity;
                    else if (physics != null) tVel = physics.LinearVelocity;


                    if (aConst.TargetLossDegree > 0 && Vector3D.DistanceSquared(Info.Origin, Position) >= aConst.SmartsDelayDistSqr)
                    {

                        if (s.WasTracking && (Info.Ai.Session.Tick20 || Vector3.Dot(Info.Direction, Position - targetPos) > 0) || !s.WasTracking)
                        {
                            var targetDir = -Info.Direction;
                            var refDir = Vector3D.Normalize(Position - targetPos);
                            if (!MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref refDir, aConst.TargetLossDegree))
                            {
                                if (s.WasTracking)
                                    s.PickTarget = true;
                            }
                            else if (!s.WasTracking)
                                s.WasTracking = true;
                        }
                    }

                    PrevTargetVel = tVel;
                }
                else
                {
                    var roam = smarts.Roam;
                    PrevTargetPos = roam ? PrevTargetPos : Position + (Info.Direction * Info.MaxTrajectory);

                    if (s.ZombieLifeTime++ > aConst.TargetLossTime && !smarts.KeepAliveAfterTargetLoss && (smarts.NoTargetExpire || hadTarget))
                    {
                        DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                        EarlyEnd = true;
                    }

                    if (roam && Info.Age - s.LastOffsetTime > 300 && hadTarget)
                    {

                        double dist;
                        Vector3D.DistanceSquared(ref Position, ref PrevTargetPos, out dist);
                        if (dist < aConst.SmartOffsetSqr + VelocityLengthSqr && Vector3.Dot(Info.Direction, Position - PrevTargetPos) > 0)
                        {

                            OffSetTarget(true);
                            PrevTargetPos += OffsetTarget;
                        }
                    }
                    else if (s.MineSeeking)
                    {
                        ResetMine();
                        return;
                    }
                }

                var commandedAccel = s.Navigation.Update(Position, Velocity, AccelInMetersPerSec, PrevTargetPos, PrevTargetVel, Gravity, smarts.Aggressiveness);
                /*

                var missileToTarget = Vector3D.Normalize(PrevTargetPos - Position);
                var askewMissileToTarget = missileToTarget + (offset ? OffsetDir : Vector3D.Zero);
                var relativeVelocity = PrevTargetVel - Velocity;
                var normalMissileAcceleration = (relativeVelocity - (relativeVelocity.Dot(missileToTarget) * missileToTarget)) * smarts.Aggressiveness;
                
                Vector3D commandedAccel;
                if (Vector3D.IsZero(normalMissileAcceleration)) commandedAccel = (askewMissileToTarget * AccelInMetersPerSec);
                else
                {

                    var maxLateralThrust = AccelInMetersPerSec * Math.Min(1, Math.Max(0, aConst.MaxLateralThrust));
                    if (normalMissileAcceleration.LengthSquared() > maxLateralThrust * maxLateralThrust)
                    {
                        Vector3D.Normalize(ref normalMissileAcceleration, out normalMissileAcceleration);
                        normalMissileAcceleration *= maxLateralThrust;
                    }
                    commandedAccel = Math.Sqrt(Math.Max(0, AccelInMetersPerSec * AccelInMetersPerSec - normalMissileAcceleration.LengthSquared())) * askewMissileToTarget + normalMissileAcceleration;
                }
                */

                if (smarts.OffsetTime > 0)
                {
                    if (Info.Age % smarts.OffsetTime == 0)
                    {
                        var angle = Info.Random.NextDouble() * MathHelper.TwoPi;
                        var up = Vector3D.CalculatePerpendicularVector(Info.Direction);
                        var right = Vector3D.Cross(Info.Direction, up);
                        OffsetDir = Math.Sin(angle) * up + Math.Cos(angle) * right;
                        OffsetDir *= smarts.OffsetRatio;
                    }

                    double dist;
                    Vector3D.DistanceSquared(ref PrevTargetPos, ref Position, out dist);
                    var offset = dist > VelocityLengthSqr * 2;

                    if (offset)
                    {
                        commandedAccel += AccelInMetersPerSec * OffsetDir;
                        commandedAccel = Vector3D.Normalize(commandedAccel) * AccelInMetersPerSec;
                    }
                }

                newVel = Velocity + (commandedAccel * StepConst);
                
                var accelDir = commandedAccel / AccelInMetersPerSec;

                AccelDir = accelDir;

                Vector3D.Normalize(ref newVel, out Info.Direction);
            }
            else
                newVel = Velocity + MaxAccelVelocity;
            VelocityLengthSqr = newVel.LengthSquared();

            if (VelocityLengthSqr > MaxSpeedSqr) newVel = Info.Direction * MaxSpeed;

            PrevVelocity = Velocity;
            Velocity = newVel;
        }

        internal void OffSetTarget(bool roam = false)
        {
            var randAzimuth = (Info.Random.NextDouble() * 1) * 2 * Math.PI;
            var randElevation = ((Info.Random.NextDouble() * 1) * 2 - 1) * 0.5 * Math.PI;
            var offsetAmount = roam ? 100 : Info.AmmoDef.Trajectory.Smarts.Inaccuracy;
            Vector3D randomDirection;
            Vector3D.CreateFromAzimuthAndElevation(randAzimuth, randElevation, out randomDirection); // this is already normalized
            OffsetTarget = (randomDirection * offsetAmount);
            if (Info.Age != 0)
            {
                Info.Storage.LastOffsetTime = Info.Age;
            }
        }

        internal bool NewTarget()
        {
            var giveUp = HadTarget != HadTargetState.None && ++TargetsSeen > Info.AmmoDef.Const.MaxTargets && Info.AmmoDef.Const.MaxTargets != 0;
            Info.Storage.ChaseAge = Info.Age;
            Info.Storage.PickTarget = false;

            var newTarget = true;

            var oldTarget = Info.Target.TargetEntity;
            if (HadTarget != HadTargetState.Projectile)
            {
                if (giveUp || !Ai.ReacquireTarget(this))
                {
                    var activeEntity = Info.Target.TargetState == Target.TargetStates.IsEntity;
                    var badEntity = !Info.LockOnFireState && activeEntity && Info.Target.TargetEntity.MarkedForClose || Info.LockOnFireState && activeEntity && (Info.Target.TargetEntity.GetTopMostParent()?.MarkedForClose ?? true);
                    if (!giveUp && !Info.LockOnFireState || Info.LockOnFireState && giveUp || !Info.AmmoDef.Trajectory.Smarts.NoTargetExpire || badEntity)
                    {
                        if (Info.Target.TargetState == Target.TargetStates.IsEntity)
                            Info.Target.Reset(Info.Ai.Session.Tick, Target.States.ProjectileNewTarget);
                    }
                    newTarget = false;
                }
            }
            else
            {

                if (Info.Target.TargetState == Target.TargetStates.IsProjectile)
                    Info.Target.Projectile?.Seekers.Remove(this);

                if (giveUp || !Ai.ReAcquireProjectile(this))
                {
                    if (Info.Target.TargetState == Target.TargetStates.IsProjectile) 
                        Info.Target.Reset(Info.Ai.Session.Tick, Target.States.ProjectileNewTarget);

                    newTarget = false;
                }
            }

            if (newTarget && Info.AmmoDef.Const.Health > 0 && !Info.AmmoDef.Const.IsBeamWeapon && (Info.Target.TargetState == Target.TargetStates.IsFake || Info.Target.TargetEntity != null && oldTarget != Info.Target.TargetEntity))
                Info.Ai.Session.Projectiles.AddProjectileTargets(this);

            if (Info.AmmoDef.Const.ProjectileSync && Info.Weapon.System.Session.IsServer && (Info.Target.TargetState != Target.TargetStates.IsFake || Info.Target.TargetState != Target.TargetStates.IsProjectile))
            {
                Info.Storage.LastProSyncStateAge = Info.Age;
            }

            return newTarget;
        }

        internal void ForceNewTarget()
        {
            Info.Storage.ChaseAge = Info.Age;
            Info.Storage.PickTarget = false;
        }

        internal bool TrajectoryEstimation(WeaponDefinition.AmmoDef ammoDef, ref Vector3D shooterPos, out Vector3D targetDirection)
        {
            var aConst = Info.AmmoDef.Const;
            if (Info.Target.TargetEntity.GetTopMostParent()?.Physics?.LinearVelocity == null)
            {
                targetDirection = Vector3D.Zero;
                return false;
            }

            var targetPos = Info.Target.TargetEntity.PositionComp.WorldAABB.Center;

            if (aConst.FragPointType == PointTypes.Direct)
            {
                targetDirection = Vector3D.Normalize(targetPos - Position);
                return true;
            }


            var targetVel = Info.Target.TargetEntity.GetTopMostParent().Physics.LinearVelocity;
            var shooterVel = !Info.AmmoDef.Const.FragDropVelocity ? Velocity : Vector3D.Zero;

            var projectileMaxSpeed = ammoDef.Const.DesiredProjectileSpeed;
            Vector3D deltaPos = targetPos - shooterPos;
            Vector3D deltaVel = targetVel - shooterVel;
            Vector3D deltaPosNorm;
            if (Vector3D.IsZero(deltaPos)) deltaPosNorm = Vector3D.Zero;
            else if (Vector3D.IsUnit(ref deltaPos)) deltaPosNorm = deltaPos;
            else Vector3D.Normalize(ref deltaPos, out deltaPosNorm);

            double closingSpeed;
            Vector3D.Dot(ref deltaVel, ref deltaPosNorm, out closingSpeed);

            Vector3D closingVel = closingSpeed * deltaPosNorm;
            Vector3D lateralVel = deltaVel - closingVel;
            double projectileMaxSpeedSqr = projectileMaxSpeed * projectileMaxSpeed;
            double ttiDiff = projectileMaxSpeedSqr - lateralVel.LengthSquared();

            if (ttiDiff < 0)
            {
                targetDirection = Info.Direction;
                return aConst.FragPointType == PointTypes.Direct;
            }

            double projectileClosingSpeed = Math.Sqrt(ttiDiff) - closingSpeed;

            double closingDistance;
            Vector3D.Dot(ref deltaPos, ref deltaPosNorm, out closingDistance);

            double timeToIntercept = ttiDiff < 0 ? 0 : closingDistance / projectileClosingSpeed;

            if (timeToIntercept < 0)
            {
                
                if (aConst.FragPointType == PointTypes.Lead)
                {
                    targetDirection = Vector3D.Normalize((targetPos + timeToIntercept * (targetVel - shooterVel)) - shooterPos);
                    return true;
                }
                
                targetDirection = Info.Direction;
                return false;
            }

            targetDirection = Vector3D.Normalize(targetPos + timeToIntercept * (targetVel - shooterVel * 1) - shooterPos);
            return true;
        }
        #endregion

        #region Mines
        internal void ActivateMine()
        {
            var ent = Info.Target.TargetEntity;
            Info.Storage.MineActivated = true;
            AtMaxRange = false;
            var targetPos = ent.PositionComp.WorldAABB.Center;
            var deltaPos = targetPos - Position;
            var targetVel = ent.Physics?.LinearVelocity ?? Vector3.Zero;
            var deltaVel = targetVel - Vector3.Zero;
            var timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, DesiredSpeed);
            var predictedPos = targetPos + (float)timeToIntercept * deltaVel;
            PrevTargetPos = predictedPos;
            LockedTarget = true;

            if (Info.AmmoDef.Trajectory.Guidance == TrajectoryDef.GuidanceType.DetectFixed) return;
            Vector3D.DistanceSquared(ref Info.Origin, ref predictedPos, out DistanceToTravelSqr);
            Info.DistanceTraveled = 0;
            Info.PrevDistanceTraveled = 0;

            Info.Direction = Vector3D.Normalize(predictedPos - Position);
            AccelDir = Info.Direction;
            VelocityLengthSqr = 0;

            MaxVelocity = (Info.Direction * DesiredSpeed);
            MaxSpeed = MaxVelocity.Length();
            MaxSpeedSqr = MaxSpeed * MaxSpeed;
            MaxAccelVelocity = (Info.Direction * Info.AmmoDef.Const.DeltaVelocityPerTick);

            if (Info.AmmoDef.Const.AmmoSkipAccel)
            {
                Velocity = MaxVelocity;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = MaxAccelVelocity;

            if (Info.AmmoDef.Trajectory.Guidance == TrajectoryDef.GuidanceType.DetectSmart)
            {
                Info.Storage.IsSmart = true;

                if (Info.AmmoDef.Const.TargetOffSet && LockedTarget)
                {
                    OffSetTarget();
                }
                else
                {
                    OffsetTarget = Vector3D.Zero;
                }
            }

            TravelMagnitude = Velocity * StepConst;
        }


        internal void SeekEnemy()
        {
            var mineInfo = Info.AmmoDef.Trajectory.Mines;
            var detectRadius = mineInfo.DetectRadius;
            var deCloakRadius = mineInfo.DeCloakRadius;

            var wakeRadius = detectRadius > deCloakRadius ? detectRadius : deCloakRadius;
            PruneSphere = new BoundingSphereD(Position, wakeRadius);
            var inRange = false;
            var activate = false;
            var minDist = double.MaxValue;
            if (!Info.Storage.MineActivated)
            {
                MyEntity closestEnt = null;
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref PruneSphere, MyEntityList, MyEntityQueryType.Dynamic);
                for (int i = 0; i < MyEntityList.Count; i++)
                {
                    var ent = MyEntityList[i];
                    var grid = ent as MyCubeGrid;
                    var character = ent as IMyCharacter;
                    if (grid == null && character == null || ent.MarkedForClose || !ent.InScene) continue;
                    MyDetectedEntityInfo entInfo;

                    if (!Info.Ai.CreateEntInfo(ent, Info.Ai.AiOwner, out entInfo)) continue;
                    switch (entInfo.Relationship)
                    {
                        case MyRelationsBetweenPlayerAndBlock.Owner:
                            continue;
                        case MyRelationsBetweenPlayerAndBlock.FactionShare:
                            continue;
                    }
                    var entSphere = ent.PositionComp.WorldVolume;
                    entSphere.Radius += Info.AmmoDef.Const.CollisionSize;
                    var dist = MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref Position, ref entSphere);
                    if (dist >= minDist) continue;
                    minDist = dist;
                    closestEnt = ent;
                }
                MyEntityList.Clear();

                if (closestEnt != null)
                {
                    ForceNewTarget();
                    Info.Target.TargetEntity = closestEnt;
                }
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsEntity && !Info.Target.TargetEntity.MarkedForClose)
            {
                var entSphere = Info.Target.TargetEntity.PositionComp.WorldVolume;
                entSphere.Radius += Info.AmmoDef.Const.CollisionSize;
                minDist = MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref Position, ref entSphere);
            }
            else
                TriggerMine(true);

            if (EnableAv)
            {
                if (Info.AvShot.Cloaked && minDist <= deCloakRadius) Info.AvShot.Cloaked = false;
                else if (Info.AvShot.AmmoDef.Trajectory.Mines.Cloak && !Info.AvShot.Cloaked && minDist > deCloakRadius) Info.AvShot.Cloaked = true;
            }

            if (minDist <= Info.AmmoDef.Const.CollisionSize) activate = true;
            if (minDist <= detectRadius) inRange = true;
            if (Info.Storage.MineActivated)
            {
                if (!inRange)
                    TriggerMine(true);
            }
            else if (inRange) ActivateMine();

            if (activate)
            {
                TriggerMine(false);
                MyEntityList.Add(Info.Target.TargetEntity);
            }
        }
        internal void TriggerMine(bool startTimer)
        {
            DistanceToTravelSqr = double.MinValue;
            if (Info.AmmoDef.Const.Ewar)
            {
                Info.AvShot.Triggered = true;
            }

            if (startTimer) DeaccelRate = Info.AmmoDef.Trajectory.Mines.FieldTime;
            Info.Storage.MineTriggered = true;
        }

        internal void ResetMine()
        {
            if (Info.Storage.MineTriggered)
            {
                Info.Storage.IsSmart = false;
                Info.DistanceTraveled = double.MaxValue;
                DeaccelRate = 0;
                return;
            }

            DeaccelRate = Info.AmmoDef.Const.Ewar || Info.AmmoDef.Const.IsMine ? Info.AmmoDef.Trajectory.DeaccelTime : 0;
            DistanceToTravelSqr = MaxTrajectorySqr;

            Info.AvShot.Triggered = false;
            Info.Storage.MineTriggered = false;
            Info.Storage.MineActivated = false;
            LockedTarget = false;
            Info.Storage.MineSeeking = true;

            if (Info.AmmoDef.Trajectory.Guidance == TrajectoryDef.GuidanceType.DetectSmart)
            {
                Info.Storage.IsSmart = false;
                Info.Storage.SmartSlot = 0;
                OffsetTarget = Vector3D.Zero;
            }

            Info.Direction = Vector3D.Zero;
            AccelDir = Vector3D.Zero;

            Velocity = Vector3D.Zero;
            TravelMagnitude = Vector3D.Zero;
            VelocityLengthSqr = 0;
        }

        #endregion

        #region Ewar
        internal void RunEwar()
        {
            if (Info.AmmoDef.Const.Pulse && !Info.EwarAreaPulse && (VelocityLengthSqr <= 0 || AtMaxRange) && !Info.AmmoDef.Const.IsMine)
            {
                Info.EwarAreaPulse = true;
                PrevVelocity = Velocity;
                Velocity = Vector3D.Zero;
                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
            }

            if (Info.EwarAreaPulse)
            {
                var maxSteps = Info.AmmoDef.Const.PulseGrowTime;
                if (Info.TriggerGrowthSteps++ < maxSteps)
                {
                    var areaSize = Info.AmmoDef.Const.EwarRadius;
                    var expansionPerTick = areaSize / maxSteps;
                    var nextSize = Info.TriggerGrowthSteps * expansionPerTick;
                    if (nextSize <= areaSize)
                    {
                        var nextRound = nextSize + 1;
                        if (nextRound > areaSize)
                        {
                            if (nextSize < areaSize)
                            {
                                nextSize = areaSize;
                                ++Info.TriggerGrowthSteps;
                            }
                        }
                        Info.TriggerMatrix = MatrixD.Identity;
                        Info.TriggerMatrix.Translation = Position;
                        MatrixD.Rescale(ref Info.TriggerMatrix, nextSize);
                        if (EnableAv)
                        {
                            Info.AvShot.Triggered = true;
                            Info.AvShot.TriggerMatrix = Info.TriggerMatrix;
                        }
                    }
                }
            }

            if (!Info.AmmoDef.Const.Pulse || Info.AmmoDef.Const.Pulse && Info.Age % Info.AmmoDef.Const.PulseInterval == 0)
                EwarEffects();
            else Info.EwarActive = false;
        }

        internal void EwarEffects()
        {
            switch (Info.AmmoDef.Const.EwarType)
            {
                case AntiSmart:
                    var eWarSphere = new BoundingSphereD(Position, Info.AmmoDef.Const.EwarRadius);

                    DynTrees.GetAllProjectilesInSphere(Info.Ai.Session, ref eWarSphere, EwaredProjectiles, false);
                    for (int j = 0; j < EwaredProjectiles.Count; j++)
                    {
                        var netted = EwaredProjectiles[j];

                        if (eWarSphere.Intersects(new BoundingSphereD(netted.Position, netted.Info.AmmoDef.Const.CollisionSize)))
                        {
                            if (netted.Info.Ai.AiType == Ai.AiTypes.Grid && Info.Target.CoreCube != null && netted.Info.Target.CoreCube.CubeGrid.IsSameConstructAs(Info.Target.CoreCube.CubeGrid) || netted.Info.Target.TargetState == Target.TargetStates.IsProjectile) continue;
                            if (Info.Random.NextDouble() * 100f < Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                            {
                                Info.BaseEwarPool -= (float)netted.Info.AmmoDef.Const.HealthHitModifier;
                                if (Info.BaseEwarPool <= 0 && Info.BaseHealthPool-- > 0)
                                {
                                    Info.EwarActive = true;
                                    netted.Info.Target.Projectile = this;
                                    netted.Info.Target.TargetState = Target.TargetStates.IsProjectile;
                                    Seekers.Add(netted);
                                }
                            }
                        }
                    }
                    EwaredProjectiles.Clear();
                    return;
                case Push:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Pull:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Tractor:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case JumpNull:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Anchor:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case EnergySink:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Emp:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Offense:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Nav:
                    if (!Info.AmmoDef.Const.Pulse || Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance)
                        Info.EwarActive = true;
                    break;
                case Dot:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                    {
                        Info.EwarActive = true;
                    }
                    break;
            }
        }
        #endregion

        #region Misc
        internal void SpawnShrapnel(bool timedSpawn = true) // inception begins
        {
            var ammoDef = Info.AmmoDef;
            var aConst = ammoDef.Const;
            var patternIndex = aConst.FragPatternCount;
            var pattern = ammoDef.Pattern;

            if (aConst.FragmentPattern)
            {
                if (pattern.Random)
                {
                    if (pattern.TriggerChance >= 1 || pattern.TriggerChance >= Info.Random.NextDouble())
                        patternIndex = Info.Random.Range(pattern.RandomMin, pattern.RandomMax);

                    for (int w = 0; w < aConst.FragPatternCount; w++)
                    {

                        var y = Info.Random.Range(0, w + 1);
                        Info.PatternShuffle[w] = Info.PatternShuffle[y];
                        Info.PatternShuffle[y] = w;
                    }
                }
                else if (pattern.PatternSteps > 0 && pattern.PatternSteps <= aConst.FragPatternCount)
                {
                    patternIndex = pattern.PatternSteps;
                    for (int p = 0; p < aConst.FragPatternCount; ++p)
                    {   
                        Info.PatternShuffle[p] = (Info.PatternShuffle[p] + patternIndex) % aConst.FragPatternCount;
                    }
                }
            }

            var fireOnTarget = timedSpawn && aConst.HasFragProximity && aConst.FragPointAtTarget;

            Vector3D newOrigin;
            if (!aConst.HasFragmentOffset)
                newOrigin = !Vector3D.IsZero(Info.Hit.LastHit) ? Info.Hit.LastHit : Position;
            else
            {
                var pos = !Vector3D.IsZero(Info.Hit.LastHit) ? Info.Hit.LastHit : Position;
                var offSet = (Info.Direction * aConst.FragmentOffset);
                newOrigin = aConst.HasNegFragmentOffset ? pos - offSet : pos + offSet;
            }

            var spawn = false;
            for (int i = 0; i < patternIndex; i++)
            {
                var fragAmmoDef = aConst.FragmentPattern ? aConst.AmmoPattern[Info.PatternShuffle[i] > 0 ? Info.PatternShuffle[i] - 1 : aConst.FragPatternCount - 1] : Info.Weapon.System.AmmoTypes[aConst.FragmentId].AmmoDef;
                Vector3D pointDir;
                if (!fireOnTarget)
                {
                    pointDir = Info.Direction;
                    if (aConst.IsDrone)
                    {
                        MathFuncs.Cone aimCone;
                        var targetSphere = new BoundingSphereD(PrevTargetPos, Info.Target.TargetEntity.PositionComp.LocalVolume.Radius);  
                        aimCone.ConeDir = Info.Direction;
                        aimCone.ConeTip = Position;
                        aimCone.ConeAngle = aConst.DirectAimCone;
                        if (!MathFuncs.TargetSphereInCone(ref targetSphere, ref aimCone)) break;
                    }
                }
                else if (!TrajectoryEstimation(fragAmmoDef, ref newOrigin, out pointDir))
                    continue;

                spawn = true;

                if (fragAmmoDef.Const.HasAdvFragOffset)
                {
                    MatrixD matrix;
                    MatrixD.CreateWorld(ref Position, ref Info.Direction, ref Info.OriginUp, out matrix);

                    Vector3D advOffSet;
                    var offSet = fragAmmoDef.Const.FragOffset;
                    Vector3D.Rotate(ref offSet, ref matrix, out advOffSet);
                    newOrigin += advOffSet;
                }

                var projectiles = Info.Ai.Session.Projectiles;
                var shrapnel = projectiles.ShrapnelPool.Get();
                shrapnel.Init(this, projectiles.FragmentPool, fragAmmoDef, timedSpawn, ref newOrigin, ref pointDir);
                projectiles.ShrapnelToSpawn.Add(shrapnel);
            }

            if (!spawn)
                return;

            ++Info.SpawnDepth;

            if (timedSpawn && ++Info.Frags == aConst.MaxFrags && aConst.FragParentDies)
                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
            Info.LastFragTime = Info.Age;
        }


        internal void CheckForNearVoxel(uint steps)
        {
            var possiblePos = BoundingBoxD.CreateFromSphere(new BoundingSphereD(Position, ((MaxSpeed) * (steps + 1) * StepConst) + Info.AmmoDef.Const.CollisionSize));
            if (MyGamePruningStructure.AnyVoxelMapInBox(ref possiblePos))
            {
                PruneQuery = MyEntityQueryType.Both;
            }
        }

        internal void SyncPosServerProjectile()
        {
            var session = Info.Ai.Session;
            var proSync = session.ProtoWeaponProSyncPosPool.Count > 0 ? session.ProtoWeaponProSyncPosPool.Pop() : new ProtoProPositionSync();
            proSync.PartId = (ushort) Info.Weapon.PartId;
            proSync.Position = Position;
            proSync.Velocity = Velocity;
            proSync.ProId = Info.Storage.SyncId;
            proSync.CoreEntityId = Info.Weapon.Comp.CoreEntity.EntityId;
            session.GlobalProPosSyncs[Info.Weapon.Comp.CoreEntity] = proSync;
        }

        internal void SyncStateServerProjectile(ProtoProStateSync.ProSyncState state)
        {
            Info.Storage.LastProSyncStateAge = int.MinValue;
            var target = Info.Target;
            var session = Info.Ai.Session;
            var seed = Info.Random.GetSeedVaues();

            var proSync = session.ProtoWeaponProSyncStatePool.Count > 0 ? session.ProtoWeaponProSyncStatePool.Pop() : new ProtoProStateSync();
            proSync.PartId = (ushort) Info.Weapon.PartId;
            proSync.State = state;
            proSync.RandomX = seed.Item1;
            proSync.RandomY = seed.Item2;
            proSync.OffsetDir = OffsetDir;
            proSync.OffsetTarget = OffsetTarget;
            proSync.ProId = Info.Storage.SyncId;
            proSync.TargetId = target.TargetId;
            proSync.CoreEntityId = Info.Weapon.Comp.CoreEntity.EntityId;
            session.GlobalProStateSyncs[Info.Weapon.Comp.CoreEntity] = proSync;
        }

        internal void SyncClientProjectile(int posSlot)
        {
            var target = Info.Target;
            var s = Info.Ai.Session;
            var w = Info.Weapon;

            ClientProSync sync;
            if (w.WeaponProSyncs.TryGetValue(Info.Storage.SyncId, out sync))
            {
                if (s.Tick - sync.UpdateTick > 30)
                {
                    w.WeaponProSyncs.Remove(Info.Storage.SyncId);
                    return;
                }

                if (sync.ProStateSync != null && sync.ProStateSync.State == ProtoProStateSync.ProSyncState.Dead)
                {
                    State = ProjectileState.Destroy;
                    w.WeaponProSyncs.Remove(Info.Storage.SyncId);
                    return;
                }

                if (sync.ProPositionSync != null && s.Tick - sync.UpdateTick <= 1 && sync.CurrentOwl < 30)
                {
                    var proPosSync = sync.ProPositionSync;

                    var oldPos = Position;
                    var oldVels = Velocity;

                    var checkSlot = posSlot - sync.CurrentOwl >= 0 ? posSlot - (int)sync.CurrentOwl : (posSlot - (int)sync.CurrentOwl) + 30;

                    var estimatedStepSize = sync.CurrentOwl * StepConst;

                    var estimatedDistTraveledToPresent = proPosSync.Velocity * estimatedStepSize;
                    var clampedEstimatedDistTraveledSqr = Math.Max(estimatedDistTraveledToPresent.LengthSquared(), 25);
                    var pastServerProPos = proPosSync.Position;
                    var futurePosition = pastServerProPos + estimatedDistTraveledToPresent;

                    var pastClientProPos = Info.Storage.PastProInfos[checkSlot];

                    if (Vector3D.DistanceSquared(pastClientProPos, pastServerProPos) > clampedEstimatedDistTraveledSqr)
                    {
                        if (++Info.Storage.ProSyncPosMissCount > 1)
                        {
                            Info.Storage.ProSyncPosMissCount = 0;
                            Position = futurePosition;
                            Velocity = proPosSync.Velocity;
                            Vector3D.Normalize(ref Velocity, out Info.Direction);
                        }
                    }
                    else
                        Info.Storage.ProSyncPosMissCount = 0;

                    if (w.System.WConst.DebugMode)
                    {
                        List<ClientProSyncDebugLine> lines;
                        if (!w.System.Session.ProSyncLineDebug.TryGetValue(Info.Storage.SyncId, out lines))
                        {
                            lines = new List<ClientProSyncDebugLine>();
                            w.System.Session.ProSyncLineDebug[Info.Storage.SyncId] = lines;
                        }

                        var pastServerLine = lines.Count == 0 ? new LineD(pastServerProPos - (proPosSync.Velocity * StepConst), pastServerProPos) : new LineD(lines[lines.Count - 1].Line.To, pastServerProPos);

                        lines.Add(new ClientProSyncDebugLine { CreateTick = s.Tick, Line = pastServerLine, Color = Color.Red});

                        //Log.Line($"ProSyn: Id:{Info.Id} - age:{Info.Age} - owl:{sync.CurrentOwl} - jumpDist:{Vector3D.Distance(oldPos, Position)}[{Vector3D.Distance(oldVels, Velocity)}] - posDiff:{Vector3D.Distance(Info.PastProInfos[checkSlot], proPosSync.Position)} - nVel:{oldVels.Length()} - oVel:{proPosSync.Velocity.Length()})");
                    }
                }

                if (sync.ProStateSync != null)
                {
                    MyEntity targetEnt;
                    if (sync.ProStateSync.TargetId > 0 && (target.TargetId != sync.ProStateSync.TargetId) && MyEntities.TryGetEntityById(sync.ProStateSync.TargetId, out targetEnt))
                    {
                        var topEntId = targetEnt.GetTopMostParent()?.EntityId ?? 0;
                        target.Set(targetEnt, targetEnt.PositionComp.WorldAABB.Center, 0, 0, topEntId);
                       
                        if (w.System.WConst.DebugMode)
                            Log.Line($"ProSyn: Id:{Info.Id} - age:{Info.Age} - targetSetTo:{targetEnt.DebugName}");
                    }
                    else if (target.TargetId > 0 && sync.ProStateSync.TargetId <= 0)
                    {
                        target.Reset(s.Tick, Target.States.NoTargetsSeen);

                        if (w.System.WConst.DebugMode)
                            Log.Line($"ProSyn: Id:{Info.Id} - age:{Info.Age} - targetCleared");
                    }

                    var seed = Info.Random.GetSeedVaues();

                    if (seed.Item1 != sync.ProStateSync.RandomX || seed.Item2 != sync.ProStateSync.RandomY)
                    {
                        var oldX = seed.Item1;
                        var oldY = seed.Item2;

                        Info.Random.SyncSeed(sync.ProStateSync.RandomX, sync.ProStateSync.RandomY);

                        var oldDir = OffsetDir;
                        var oldTarget = OffsetTarget;
                        OffsetDir = sync.ProStateSync.OffsetDir;
                        OffsetTarget = sync.ProStateSync.OffsetTarget;

                        if (w.System.WConst.DebugMode)
                            Log.Line($"seedReset: Id:{Info.Id} - age:{Info.Age} - owl:{sync.CurrentOwl} - stateAge:{Info.Age - Info.Storage.LastProSyncStateAge} - tId:{sync.ProStateSync.TargetId} - oDirDiff:{Vector3D.IsZero(oldDir - OffsetDir, 1E-02d)} - targetDiff:{Vector3D.Distance(oldTarget, OffsetTarget)} - x:{oldX}[{sync.ProStateSync.RandomX}] - y{oldY}[{sync.ProStateSync.RandomY}]");
                    }

                }

                w.WeaponProSyncs.Remove(Info.Storage.SyncId);
            }
        }
        #endregion
    }
}