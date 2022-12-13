using System;
using System.Collections.Generic;
using CoreSystems.Support;
using Jakaria.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.ProtoWeaponCompTasks;
using static CoreSystems.Session;
using static CoreSystems.Support.DroneStatus;
using static CoreSystems.Support.WeaponDefinition.AmmoDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.EwarDef.EwarType;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.FragmentDef.TimedSpawnDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.TrajectoryDef.ApproachDef;

namespace CoreSystems.Projectiles
{
    internal class Projectile
    {
        internal readonly ProInfo Info = new ProInfo();
        internal readonly List<MyLineSegmentOverlapResult<MyEntity>> MySegmentList = new List<MyLineSegmentOverlapResult<MyEntity>>();
        internal readonly List<MyEntity> MyEntityList = new List<MyEntity>();
        internal readonly List<ProInfo> VrPros = new List<ProInfo>();
        internal readonly List<Ai> Watchers = new List<Ai>();
        internal readonly HashSet<Projectile> Seekers = new HashSet<Projectile>();
        internal ProjectileState State;
        internal MyEntityQueryType PruneQuery;
        internal HadTargetState HadTarget;
        internal EndStates EndState;
        internal Vector3D Position;
        internal Vector3D LastPosition;
        internal Vector3D Velocity;
        internal Vector3D PrevVelocity;
        internal Vector3D TravelMagnitude;
        internal Vector3D TargetPosition;
        internal Vector3D OffsetTarget;
        internal Vector3 PrevTargetVel;
        internal Vector3 Gravity;
        internal LineD Beam;
        internal BoundingSphereD PruneSphere;
        internal double DistanceToTravelSqr;
        internal double VelocityLengthSqr;
        internal double DistanceToSurfaceSqr;
        internal double DesiredSpeed;
        internal double MaxSpeed;
        internal bool EnableAv;
        internal bool Intersecting;
        internal bool FinalizeIntersection;
        internal int DeaccelRate;
        internal int TargetsSeen;
        internal int PruningProxyId = -1;

        internal enum EndStates
        {
            None,
            AtMaxRange,
            EarlyEnd,
            AtMaxEarly,
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
            var ai = Info.Ai;
            var session = ai.Session;
            var ammoDef = Info.AmmoDef;
            var aConst = ammoDef.Const;
            var w = Info.Weapon;
            var comp = w.Comp;

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


            EndState = EndStates.None;
            Position = Info.Origin;
            var cameraStart = session.CameraPos;
            double distanceFromCameraSqr;
            Vector3D.DistanceSquared(ref cameraStart, ref Info.Origin, out distanceFromCameraSqr);
            var probability = ammoDef.AmmoGraphics.VisualProbability;
            EnableAv = !aConst.VirtualBeams && !session.DedicatedServer && (distanceFromCameraSqr <= session.SyncDistSqr || ai.AiType == Ai.AiTypes.Phantom) && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));
            Info.AvShot = null;
            Info.Age = -1;

            TargetsSeen = 0;
            PruningProxyId = -1;

            Intersecting = false;
            Info.PrevDistanceTraveled = 0;
            Info.DistanceTraveled = 0;
            DistanceToSurfaceSqr = double.MaxValue;
            var trajectory = ammoDef.Trajectory;
            var guidance = trajectory.Guidance;

            if (aConst.DynamicGuidance && session.AntiSmartActive) DynTrees.RegisterProjectile(this);

            Info.MyPlanet = ai.MyPlanet;
            
            if (!session.VoxelCaches.TryGetValue(Info.UniqueMuzzleId, out Info.VoxelCache))
                Info.VoxelCache = session.VoxelCaches[ulong.MaxValue];

            if (Info.MyPlanet != null)
                Info.VoxelCache.PlanetSphere.Center = ai.ClosestPlanetCenter;

            ai.ProjectileTicker = ai.Session.Tick;
            Info.ObjectsHit = 0;
            Info.BaseHealthPool = aConst.Health;
            Info.BaseEwarPool = aConst.Health;

            if (aConst.IsSmart || aConst.IsDrone)
            {
                Info.Storage.SmartSlot = Info.Random.Range(0, 10);
            }

            switch (Info.Target.TargetState)
            {
                case Target.TargetStates.WasProjectile:
                    HadTarget = HadTargetState.Projectile;
                    break;
                case Target.TargetStates.IsProjectile:
                    var tProjectile = Info.Target.TargetObject as Projectile;
                    if (tProjectile == null)
                    {
                        HadTarget = HadTargetState.None;
                        Info.Target.TargetState = Target.TargetStates.None;
                        TargetPosition = Vector3D.Zero;
                        Log.Line($"ProjectileStart had invalid Projectile target state");
                        break;
                    }
                    HadTarget = HadTargetState.Projectile;
                    TargetPosition = tProjectile.Position;
                    tProjectile.Seekers.Add(this);
                    break;
                case Target.TargetStates.IsFake:
                    TargetPosition = Info.IsFragment ? TargetPosition : Vector3D.Zero;
                    HadTarget = HadTargetState.Fake;
                    break;
                case Target.TargetStates.IsEntity:
                    var eTarget = Info.Target.TargetObject as MyEntity;
                    if (eTarget == null)
                    {
                        HadTarget = HadTargetState.None;
                        Info.Target.TargetState = Target.TargetStates.None;
                        TargetPosition = Vector3D.Zero;
                        Log.Line($"ProjectileStart had invalid entity target state, isFragment: {Info.IsFragment} - ammo:{ammoDef.AmmoRound} - weapon:{Info.Weapon.System.ShortName}");
                        break;
                    }

                    if (aConst.IsDrone)
                    {
                        Info.Storage.DroneMsn = DroneMission.Attack;//TODO handle initial defensive assignment?
                        Info.Storage.DroneStat = Launch;
                        Info.Storage.NavTargetEnt = eTarget.GetTopMostParent();
                        Info.Storage.NavTargetBound = eTarget.PositionComp.WorldVolume;
                    }

                    TargetPosition = eTarget.PositionComp.WorldAABB.Center;
                    HadTarget = HadTargetState.Entity;
                    break;
                default:
                    TargetPosition = Info.IsFragment ? TargetPosition : Vector3D.Zero;
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

            var lockedTarget = !Vector3D.IsZero(TargetPosition);
            if (!lockedTarget)
                TargetPosition = Position + (Info.Direction * Info.MaxTrajectory);

            if (lockedTarget && !aConst.IsBeamWeapon && guidance == TrajectoryDef.GuidanceType.TravelTo)
            {
                Info.Storage.RequestedStage = -2;
                if (!MyUtils.IsZero(TargetPosition))
                {
                    TargetPosition -= (Info.Direction * variance);
                }
                Vector3D.DistanceSquared(ref Info.Origin, ref TargetPosition, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = Info.MaxTrajectory * Info.MaxTrajectory;

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

            if (aConst.IsSmart && aConst.TargetOffSet && (lockedTarget || Info.Target.TargetState == Target.TargetStates.IsFake))
            {
                OffSetTarget();
            }
            else
            {
                OffsetTarget = Vector3D.Zero;
            }

            Info.Storage.PickTarget = (aConst.OverrideTarget || comp.ModOverride && !lockedTarget) && Info.Target.TargetState != Target.TargetStates.IsFake;
            if (Info.Storage.PickTarget || lockedTarget && !Info.IsFragment) TargetsSeen++;
            Info.TracerLength = aConst.TracerLength <= Info.MaxTrajectory ? aConst.TracerLength : Info.MaxTrajectory;

            var staticIsInRange = ai.ClosestStaticSqr * 0.5 < Info.MaxTrajectory * Info.MaxTrajectory;
            var pruneStaticCheck = ai.ClosestPlanetSqr * 0.5 < Info.MaxTrajectory * Info.MaxTrajectory || ai.StaticGridInRange;
            PruneQuery = (aConst.DynamicGuidance && pruneStaticCheck) || aConst.FeelsGravity && staticIsInRange || !aConst.DynamicGuidance && !aConst.FeelsGravity && staticIsInRange ? MyEntityQueryType.Both : MyEntityQueryType.Dynamic;

            if (ai.PlanetSurfaceInRange && ai.ClosestPlanetSqr <= Info.MaxTrajectory * Info.MaxTrajectory)
            {
                PruneQuery = MyEntityQueryType.Both;
            }

            if (aConst.DynamicGuidance && PruneQuery == MyEntityQueryType.Dynamic && staticIsInRange) 
                CheckForNearVoxel(60);

            var desiredSpeed = (Info.Direction * DesiredSpeed);
            var relativeSpeedCap = Info.ShooterVel + desiredSpeed;
            MaxSpeed = relativeSpeedCap.Length();
            if (aConst.AmmoSkipAccel)
            {
                Velocity = relativeSpeedCap;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = Info.ShooterVel + (Info.Direction * (aConst.DeltaVelocityPerTick * session.DeltaTimeRatio));

            if (Info.IsFragment)
                Vector3D.Normalize(ref Velocity, out Info.Direction);


            TravelMagnitude = !Info.IsFragment && aConst.AmmoSkipAccel ? desiredSpeed * DeltaStepConst : Velocity * DeltaStepConst;
            DeaccelRate = aConst.Ewar || aConst.IsMine ? trajectory.DeaccelTime : aConst.IsDrone ? 100: 0;
            State = !aConst.IsBeamWeapon ? ProjectileState.Alive : ProjectileState.OneAndDone;

            if (EnableAv)
            {
                Info.AvShot = session.Av.AvShotPool.Count > 0 ? session.Av.AvShotPool.Pop() : new AvShot(session);
                Info.AvShot.Init(Info, (aConst.DeltaVelocityPerTick * session.DeltaTimeRatio), MaxSpeed, ref Info.Direction);
                Info.AvShot.SetupSounds(distanceFromCameraSqr); //Pool initted sounds per Projectile type... this is expensive
                if (aConst.HitParticle && !aConst.IsBeamWeapon || aConst.EndOfLifeAoe && !ammoDef.AreaOfDamage.EndOfLife.NoVisuals)
                {
                    var hitPlayChance = Info.AmmoDef.AmmoGraphics.Particles.Hit.Extras.HitPlayChance;
                    Info.AvShot.HitParticleActive = hitPlayChance >= 1 || hitPlayChance >= MyUtils.GetRandomDouble(0.0f, 1f);
                }

                if (aConst.PrimeModel || aConst.TriggerModel)
                {
                    Info.AvShot.HasModel = true;
                    Info.AvShot.ModelOnly = !aConst.DrawLine;
                }
            }

            var monitor = comp.ProjectileMonitors[w.PartId];
            if (monitor.Count > 0)
            {
                comp.Session.MonitoredProjectiles[Info.Id] = this;
                for (int j = 0; j < monitor.Count; j++)
                    monitor[j].Invoke(comp.CoreEntity.EntityId, w.PartId, Info.Id, Info.Target.TargetId, Position, true);
            }
        }

        #endregion

        #region End

        internal void DestroyProjectile()
        {
            Info.Hit = new Hit { Entity = null, SurfaceHit = Position, LastHit = Position, HitVelocity = !Vector3D.IsZero(Gravity) ? Velocity * 0.33f : Velocity, HitTick = Info.Ai.Session.Tick };
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
                if (seeker.Info.Target.TargetObject == this)
                    seeker.Info.Target.Reset(session.Tick, Target.States.ProjectileClose);
            }
            Seekers.Clear();

            if (EnableAv && Info.AvShot.ForceHitParticle)
                Info.AvShot.HitEffects(true);

            State = ProjectileState.Dead;

            var detExp = aConst.EndOfLifeAv && (!aConst.ArmOnlyOnHit || Info.ObjectsHit > 0);

            if (EnableAv)
            {
                Info.AvShot.HasModel = false;

                if (!Info.AvShot.Active)
                    Info.AvShot.Close();
                else Info.AvShot.EndState = new AvClose { EndPos = Position, Dirty = true, DetonateEffect = detExp };
            }
            else if (Info.AmmoDef.Const.VirtualBeams)
            {
                for (int i = 0; i < VrPros.Count; i++)
                {
                    var vp = VrPros[i];
                    if (!vp.AvShot.Active)
                        vp.AvShot.Close();
                    else vp.AvShot.EndState = new AvClose { EndPos = Position, Dirty = true, DetonateEffect = detExp };

                    session.Projectiles.VirtInfoPool.Return(vp);
                }
                VrPros.Clear();
            }

            if (aConst.DynamicGuidance && session.AntiSmartActive)
                DynTrees.UnregisterProjectile(this);

            var dmgTotal = Info.DamageDoneAoe + Info.DamageDonePri + Info.DamageDoneShld + Info.DamageDoneProj;

            if (dmgTotal > 0 && Info.Ai?.Construct.RootAi != null && !Info.Ai.MarkedForClose && !Info.Weapon.Comp.CoreEntity.MarkedForClose)
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
                SyncPosServerProjectile(ProtoProStateSync.ProSyncState.Dead);

            PruningProxyId = -1;
            HadTarget = HadTargetState.None;
            
            Info.Clean(aConst.IsSmart || aConst.IsDrone);

        }
        #endregion


        #region Smart
        internal void RunSmart() // this is grossly inlined thanks to mod profiler... thanks keen.
        {
            Vector3D proposedVel = Velocity;

            var ammo = Info.AmmoDef;
            var aConst = ammo.Const;
            var s = Info.Storage;
            var smarts = ammo.Trajectory.Smarts;
            var coreParent = Info.Weapon.Comp.TopEntity;
            var startTrack = s.SmartReady || coreParent.MarkedForClose;
            var session = Info.Ai.Session;
            var speedCapMulti = 1d;

            var targetLock = false;
            var speedLimitPerTick = aConst.AmmoSkipAccel ? DesiredSpeed : aConst.AccelInMetersPerSec;
            if (!startTrack && Info.DistanceTraveled * Info.DistanceTraveled >= aConst.SmartsDelayDistSqr)
            {
                var lineCheck = new LineD(Position, TargetPosition);
                startTrack = !new MyOrientedBoundingBoxD(coreParent.PositionComp.LocalAABB, coreParent.PositionComp.WorldMatrixRef).Intersects(ref lineCheck).HasValue;
            }

            if (startTrack)
            {
                s.SmartReady = true;
                var fake = Info.Target.TargetState == Target.TargetStates.IsFake;
                var hadTarget = HadTarget != HadTargetState.None;

                var gaveUpChase = !fake && Info.Age - s.ChaseAge > aConst.MaxChaseTime && hadTarget;
                var overMaxTargets = hadTarget && TargetsSeen > aConst.MaxTargets && aConst.MaxTargets != 0;
                var validEntity = Info.Target.TargetState == Target.TargetStates.IsEntity && !((MyEntity)Info.Target.TargetObject).MarkedForClose;
                var validTarget = fake || Info.Target.TargetState == Target.TargetStates.IsProjectile || validEntity && !overMaxTargets;
                var checkTime = HadTarget != HadTargetState.Projectile ? 30 : 10;
                var isZombie = aConst.CanZombie && hadTarget && !fake && !validTarget && s.ZombieLifeTime > 0 && (s.ZombieLifeTime + s.SmartSlot) % checkTime == 0;
                var timeSlot = (Info.Age + s.SmartSlot) % checkTime == 0;
                var seekNewTarget = timeSlot && hadTarget && !validTarget && !overMaxTargets;
                var seekFirstTarget = !hadTarget && !validTarget && s.PickTarget && (Info.Age > 120 && timeSlot || Info.Age % checkTime == 0 && Info.IsFragment);

                #region TargetTracking
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
                        fakeTargetInfo = fakeTarget.LastInfoTick != session.Tick ? fakeTarget.GetFakeTargetInfo(Info.Ai) : fakeTarget.FakeInfo;
                        targetPos = fakeTargetInfo.WorldPosition;
                        HadTarget = HadTargetState.Fake;
                    }
                    else if (Info.Target.TargetState == Target.TargetStates.IsProjectile)
                    {
                        targetPos = ((Projectile)Info.Target.TargetObject).Position;
                        HadTarget = HadTargetState.Projectile;
                    }
                    else if (Info.Target.TargetState == Target.TargetStates.IsEntity)
                    {
                        targetPos = ((MyEntity)Info.Target.TargetObject).PositionComp.WorldAABB.Center;
                        HadTarget = HadTargetState.Entity;
                    }
                    else
                        HadTarget = HadTargetState.Other;

                    if (aConst.TargetOffSet)
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

                    TargetPosition = targetPos;
                    targetLock = true;
                    var eTarget = Info.Target.TargetObject as MyEntity;
                    var physics = eTarget != null ? eTarget?.Physics ?? eTarget?.Parent?.Physics : null;

                    var tVel = Vector3.Zero;
                    if (fake && fakeTargetInfo != null) tVel = fakeTargetInfo.LinearVelocity;
                    else if (Info.Target.TargetState == Target.TargetStates.IsProjectile) tVel = ((Projectile)Info.Target.TargetObject).Velocity;
                    else if (physics != null) tVel = physics.LinearVelocity;

                    if (aConst.TargetLossDegree > 0 && Vector3D.DistanceSquared(Info.Origin, Position) >= aConst.SmartsDelayDistSqr)
                    {
                        if (s.WasTracking && (session.Tick20 || Vector3.Dot(Info.Direction, Position - targetPos) > 0) || !s.WasTracking)
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
                    var straightAhead = roam || TargetPosition == Vector3D.Zero;

                    TargetPosition = straightAhead ? TargetPosition : Position + (Info.Direction * Info.MaxTrajectory);

                    if (s.ZombieLifeTime++ > aConst.TargetLossTime && !smarts.KeepAliveAfterTargetLoss && (smarts.NoTargetExpire || hadTarget))
                    {
                        DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                        EndState = EndStates.EarlyEnd;
                    }

                    if (roam && Info.Age - s.LastOffsetTime > 300 && hadTarget)
                    {

                        double dist;
                        Vector3D.DistanceSquared(ref Position, ref TargetPosition, out dist);
                        if (dist < aConst.SmartOffsetSqr + VelocityLengthSqr && Vector3.Dot(Info.Direction, Position - TargetPosition) > 0)
                        {

                            OffSetTarget(true);
                            TargetPosition += OffsetTarget;
                        }
                    }
                    else if (aConst.IsMine && s.LastActivatedStage >= 0)
                    {
                        ResetMine();
                        return;
                    }
                }
                #endregion

                var accelMpsMulti = speedLimitPerTick;
                if (aConst.ApproachesCount > 0 && s.RequestedStage < aConst.ApproachesCount && s.RequestedStage >= -1)
                {
                    ProcessStage(ref accelMpsMulti, ref speedCapMulti, TargetPosition, s.RequestedStage, targetLock);
                }

                #region Navigation
                Vector3D targetAcceleration = Vector3D.Zero;
                if (s.Navigation.LastVelocity.HasValue)
                    targetAcceleration = (PrevTargetVel - s.Navigation.LastVelocity.Value) * 60;

                s.Navigation.LastVelocity = PrevTargetVel;

                Vector3D missileToTarget = TargetPosition - Position;
                Vector3D missileToTargetNorm = Vector3D.Normalize(missileToTarget);
                Vector3D relativeVelocity = PrevTargetVel - Velocity;
                Vector3D lateralTargetAcceleration = (targetAcceleration - Vector3D.Dot(targetAcceleration, missileToTargetNorm) * missileToTargetNorm);

                Vector3D omega = Vector3D.Cross(missileToTarget, relativeVelocity) / Math.Max(missileToTarget.LengthSquared(), 1); //to combat instability at close range
                var lateralAcceleration = smarts.Aggressiveness * relativeVelocity.Length() * Vector3D.Cross(omega, missileToTargetNorm) + smarts.NavAcceleration * lateralTargetAcceleration;

                Vector3D commandedAccel;
                if (Vector3D.IsZero(lateralAcceleration))
                {
                    commandedAccel = missileToTargetNorm * accelMpsMulti;
                }
                else
                {
                    var diff = accelMpsMulti * accelMpsMulti - lateralAcceleration.LengthSquared();
                    commandedAccel = diff < 0 ? Vector3D.Normalize(lateralAcceleration) * accelMpsMulti : lateralAcceleration + Math.Sqrt(diff) * missileToTargetNorm;
                }

                if (Gravity.LengthSquared() > 1e-3)
                {
                    if (!Vector3D.IsZero(commandedAccel))
                    {
                        var directionNorm = Vector3D.IsUnit(ref commandedAccel) ? commandedAccel : Vector3D.Normalize(commandedAccel);
                        Vector3D gravityCompensationVec;

                        if (Vector3D.IsZero(Gravity) || Vector3D.IsZero(commandedAccel))
                            gravityCompensationVec = Vector3D.Zero;
                        else
                            gravityCompensationVec = (Gravity - Gravity.Dot(commandedAccel) / commandedAccel.LengthSquared() * commandedAccel);

                        var diffSq = accelMpsMulti * accelMpsMulti - gravityCompensationVec.LengthSquared();
                        commandedAccel = diffSq < 0 ? commandedAccel - Gravity : directionNorm * Math.Sqrt(diffSq) + gravityCompensationVec;
                    }
                }

                var offset = false;
                if (smarts.OffsetTime > 0)
                {
                    if (Info.Age % smarts.OffsetTime == 0 && !Vector3D.IsZero(Info.Direction) && MyUtils.IsValid(Info.Direction))
                    {
                        var up = Vector3D.CalculatePerpendicularVector(Info.Direction);
                        var right = Vector3D.Cross(Info.Direction, up);
                        var angle = Info.Random.NextDouble() * MathHelper.TwoPi;
                        s.RandOffsetDir = Math.Sin(angle) * up + Math.Cos(angle) * right;
                        s.RandOffsetDir *= smarts.OffsetRatio;
                    }

                    double distSqr;
                    Vector3D.DistanceSquared(ref TargetPosition, ref Position, out distSqr);
                    if (distSqr > VelocityLengthSqr)
                    {
                        commandedAccel += accelMpsMulti * s.RandOffsetDir;
                        offset = true;
                    }
                }

                if (accelMpsMulti > 0)
                {
                    var maxRotationsPerTickInRads = aConst.MaxLateralThrust;

                    if (aConst.AdvancedSmartSteering)
                    {
                        bool isNormalized;
                        var newHeading = ProNavControl(Info.Direction, Velocity, commandedAccel, aConst.PreComputedMath, out isNormalized);
                        proposedVel = Velocity + (isNormalized ? newHeading * speedLimitPerTick * DeltaStepConst : commandedAccel * DeltaStepConst);
                    }
                    else
                    {
                        if (maxRotationsPerTickInRads < 1)
                        {
                            var commandNorm = Vector3D.Normalize(commandedAccel);

                            var dot = Vector3D.Dot(Info.Direction, commandNorm);
                            if (offset || dot < 0.98)
                            {
                                var radPerTickDelta = Math.Acos(dot);
                                if (radPerTickDelta == 0)
                                    radPerTickDelta = double.Epsilon;

                                if (radPerTickDelta > maxRotationsPerTickInRads && dot > 0)
                                    commandedAccel = commandNorm * (accelMpsMulti * Math.Abs(radPerTickDelta / MathHelperD.Pi - 1));
                            }
                        }
                        proposedVel = Velocity + (commandedAccel * DeltaStepConst);
                    }

                    if (!MyUtils.IsValid(proposedVel) || Vector3D.IsZero(proposedVel)) {
                        Log.Line($"Info.Direction is NaN - proposedVel:{proposedVel} - {commandedAccel} - Position:{Position} - Direction:{Info.Direction} - rndDir:{s.RandOffsetDir} - lateralAcceleration:{lateralAcceleration} - missileToTargetNorm:{missileToTargetNorm} - missileToTargetNorm:{relativeVelocity}");
                        proposedVel = Velocity + (Info.Direction * (aConst.DeltaVelocityPerTick * Info.Ai.Session.DeltaTimeRatio));
                    }

                    Vector3D.Normalize(ref proposedVel, out Info.Direction);
                }
                #endregion
            }
            else if (!smarts.AccelClearance || s.SmartReady)
            {
                proposedVel = Velocity + (Info.Direction * (aConst.DeltaVelocityPerTick * Info.Ai.Session.DeltaTimeRatio));
            }
            VelocityLengthSqr = proposedVel.LengthSquared();
            if (VelocityLengthSqr <= DesiredSpeed * DesiredSpeed)
                MaxSpeed = DesiredSpeed;

            var speedCap = speedCapMulti * MaxSpeed;
            if (VelocityLengthSqr > speedCap * speedCap) {
                VelocityLengthSqr = proposedVel.LengthSquared();
                proposedVel = Info.Direction * speedCap;
            }

            PrevVelocity = Velocity;
            Info.TotalAcceleration += (proposedVel - PrevVelocity);
            if (Info.TotalAcceleration.LengthSquared() > aConst.MaxAccelerationSqr)
                proposedVel = Velocity;

            Velocity = proposedVel;
        }

        private void ProcessStage(ref double accelMpsMulti, ref double speedCapMulti, Vector3D targetPos, int lastActiveStage, bool targetLock)
        {
            var s = Info.Storage;

            if (targetLock)
                s.SetTargetPos = targetPos;

            if (!Vector3D.IsZero(s.SetTargetPos))
            {
                if (s.RequestedStage == -1)
                {
                    if (Info.Ai.Session.DebugMod)
                        Log.Line($"StageStart: {Info.AmmoDef.AmmoRound} - last: {s.LastActivatedStage} - age:{Info.Age}");
                    s.LastActivatedStage = -1;
                    s.RequestedStage = 0;

                }

                var stageChange = s.RequestedStage != lastActiveStage;

                if (stageChange)
                {
                    if (Info.Ai.Session.DebugMod)
                        Log.Line($"state change: {s.RequestedStage} - age:{Info.Age}");
                    s.StartDistanceTraveled = Info.DistanceTraveled;
                }

                var aConst = Info.AmmoDef.Const;
                if (s.RequestedStage >= aConst.Approaches.Length)
                {
                    Log.Line($"ProcessStage outside of bounds: {s.RequestedStage > aConst.Approaches.Length - 1} - lastStage:{lastActiveStage} - {Info.Weapon.System.ShortName} - {Info.Weapon.ActiveAmmoDef.AmmoDef.AmmoRound}");
                    return;
                }
                var approach = aConst.Approaches[s.RequestedStage];
                var def = approach.Definition;

                if (def.StartCondition1 == def.StartCondition2 || def.EndCondition1 == def.EndCondition2)
                    return; // bad modder, failed to read coreparts comment, fail silently so they drive themselves nuts

                var planetExists = Info.MyPlanet != null;

                if (def.AdjustUpDir || stageChange)
                {
                    switch (def.UpDirection)
                    {
                        case UpRelativeTo.RelativeToBlock:
                            s.OffsetDir = Info.OriginUp;
                            break;
                        case UpRelativeTo.RelativeToGravity:
                            s.OffsetDir = !planetExists ? Info.OriginUp : Vector3D.Normalize(Position - Info.MyPlanet.PositionComp.WorldAABB.Center);
                            break;
                        case UpRelativeTo.TargetDirection:
                            s.OffsetDir = Vector3D.Normalize(s.SetTargetPos - Position);
                            break;
                        case UpRelativeTo.TargetVelocity:
                            s.OffsetDir = !Vector3D.IsZero(PrevTargetVel) ? Vector3D.Normalize(PrevTargetVel) : Info.OriginUp;
                            break;
                        default:
                            s.OffsetDir = Info.OriginUp;
                            break;
                    }
                }

                if (!MyUtils.IsZero(def.AngleOffset))
                {
                    var angle = def.AngleOffset * MathHelper.Pi;
                    var forward = Vector3D.CalculatePerpendicularVector(s.OffsetDir);
                    var right = Vector3D.Cross(s.OffsetDir, forward);
                    s.OffsetDir = Math.Sin(angle) * forward + Math.Cos(angle) * right;
                }

                Vector3D surfacePos = Vector3D.Zero;
                if (stageChange || def.AdjustVantagePoint)
                {
                    switch (def.VantagePoint)
                    {
                        case VantagePointRelativeTo.Origin:
                            s.LookAtPos = Info.Origin;
                            break;
                        case VantagePointRelativeTo.Shooter:
                            s.LookAtPos = Info.Weapon.MyPivotPos;
                           break;
                        case VantagePointRelativeTo.Target:
                            s.LookAtPos = s.SetTargetPos;
                            break;
                        case VantagePointRelativeTo.Surface:
                            if (planetExists)
                            {
                                PlanetSurfaceHeightAdjustment(Position, Info.Direction, approach, out surfacePos);
                                s.LookAtPos = surfacePos;
                            }
                            else
                                s.LookAtPos = Info.Origin;
                            break;
                        case VantagePointRelativeTo.MidPoint:
                            s.LookAtPos = Vector3D.Lerp(s.SetTargetPos, Position, 0.5);
                            break;
                    }
                }

                var heightOffset = s.OffsetDir * def.DesiredElevation;

                var source = s.LookAtPos;
                var destination = def.VantagePoint != VantagePointRelativeTo.Target ? (def.AdjustDestinationPosition ? s.SetTargetPos : Info.Target.TargetPos) : (def.AdjustDestinationPosition ? Position : Info.Origin);

                var heightStart = source + heightOffset;
                var heightend = destination + heightOffset;
                var heightDir = heightend - heightStart;
                var startToEndDist = heightDir.Normalize();

                bool start1;
                switch (def.StartCondition1)
                {
                    case Conditions.DesiredElevation:
                        var plane = new PlaneD(s.LookAtPos, s.OffsetDir);
                        var distToPlane = plane.DistanceToPoint(Position);
                        var tolernace = def.ElevationTolerance + aConst.CollisionSize;
                        var distFromSurfaceSqr = !Vector3D.IsZero(surfacePos) ? Vector3D.DistanceSquared(Position, surfacePos) : distToPlane * distToPlane;
                        var lessThanTolerance = (def.Start1Value + tolernace) * (def.Start1Value + tolernace);
                        var greaterThanTolerance = (def.Start1Value - tolernace) * (def.Start1Value - tolernace);
                        start1 = distFromSurfaceSqr >= greaterThanTolerance && distFromSurfaceSqr <= lessThanTolerance;
                        break;
                    case Conditions.DistanceFromTarget: // could save a sqrt by inlining and using heightDir
                        if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Green, 10);
                        start1 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize <= def.Start1Value;
                        break;
                    case Conditions.DistanceToTarget: // could save a sqrt by inlining and using heightDir
                        if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Green, 10);
                        start1 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize >= def.Start1Value;
                        break;
                    case Conditions.Lifetime:
                        start1 = Info.Age >= def.Start1Value;
                        break;
                    case Conditions.Deadtime:
                        start1 = Info.Age <= def.Start1Value;
                        break;
                    case Conditions.MinTravelRequired:
                        start1 = Info.DistanceTraveled - s.StartDistanceTraveled >= def.Start1Value;
                        break;
                    case Conditions.MaxTravelRequired:
                        start1 = Info.DistanceTraveled - s.StartDistanceTraveled <= def.Start1Value;
                        break;
                    case Conditions.Spawn:
                    case Conditions.Ignore:
                        start1 = true;
                        break;
                    default:
                        start1 = false;
                        break;
                }

                bool start2;
                switch (def.StartCondition2)
                {
                    case Conditions.DesiredElevation:
                        var plane = new PlaneD(s.LookAtPos, s.OffsetDir);
                        var distToPlane = plane.DistanceToPoint(Position);
                        var tolernace = def.ElevationTolerance + aConst.CollisionSize;
                        var distFromSurfaceSqr = !Vector3D.IsZero(surfacePos) ? Vector3D.DistanceSquared(Position, surfacePos) : distToPlane * distToPlane;
                        var lessThanTolerance = (def.Start2Value + tolernace) * (def.Start2Value + tolernace);
                        var greaterThanTolerance = (def.Start2Value - tolernace) * (def.Start2Value - tolernace);
                        start2 = distFromSurfaceSqr >= greaterThanTolerance && distFromSurfaceSqr <= lessThanTolerance;
                        break;
                    case Conditions.DistanceFromTarget: // could save a sqrt by inlining and using heightDir
                        if (Info.Ai.Session.DebugMod)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Blue, 10);
                        start2 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize <= def.Start2Value;
                        break;
                    case Conditions.DistanceToTarget: 
                        if (Info.Ai.Session.DebugMod)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Green, 10);
                        start2 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize >= def.Start2Value;
                        break;
                    case Conditions.Lifetime:
                        start2 = Info.Age >= def.Start2Value;
                        break;
                    case Conditions.Deadtime:
                        start2 = Info.Age <= def.Start2Value;
                        break;
                    case Conditions.MinTravelRequired:
                        start2 = Info.DistanceTraveled - s.StartDistanceTraveled >= def.Start2Value;
                        break;
                    case Conditions.MaxTravelRequired:
                        start2 = Info.DistanceTraveled - s.StartDistanceTraveled <= def.Start2Value;
                        break;
                    case Conditions.Spawn:
                    case Conditions.Ignore:
                        start2 = true;
                        break;
                    default:
                        start2 = false;
                        break;
                }

                if (approach.StartAnd && start1 && start2 || !approach.StartAnd && (start1 || start2) || s.LastActivatedStage >= 0 && !def.CanExpireOnceStarted)
                {
                    if (s.LastActivatedStage != s.RequestedStage)
                    {
                        if (Info.Ai.Session.DebugMod)
                            Log.Line($"stage: age:{Info.Age} - {s.RequestedStage} - CanExpireOnceStarted:{def.CanExpireOnceStarted}");
                        s.LastActivatedStage = s.RequestedStage;

                        switch (def.StartEvent)
                        {
                            case StageEvents.EndProjectile:
                                EndState = EndStates.EarlyEnd;
                                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                                break;
                            case StageEvents.NoNothing:
                                break;
                        }
                    }

                    accelMpsMulti = aConst.AccelInMetersPerSec * def.AccelMulti;
                    speedCapMulti = def.SpeedCapMulti;

                    var travelLead = Info.DistanceTraveled - s.StartDistanceTraveled >= def.TrackingDistance ? Info.DistanceTraveled : 0;
                    var desiredLead = (def.PushLeadByTravelDistance ? travelLead : 0) + def.LeadDistance;
                    var clampedLead = MathHelperD.Clamp(desiredLead, approach.ModFutureStep, double.MaxValue);
                    var leadPosition = heightStart + heightDir * clampedLead;

                    Vector3D heightAdjLeadPos;
                    switch (def.AdjustElevation)
                    {
                        case VantagePointRelativeTo.Surface:
                        {
                            if (Info.MyPlanet != null && planetExists && def.AdjustElevation == VantagePointRelativeTo.Surface)
                            {
                                Vector3D followSurfacePos;
                                heightAdjLeadPos = PlanetSurfaceHeightAdjustment(leadPosition, s.OffsetDir, approach, out followSurfacePos);
                            }
                            else
                                heightAdjLeadPos = leadPosition;
                            break;
                        }
                        case VantagePointRelativeTo.Origin:
                        {
                            var plane = new PlaneD(Info.Origin, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        case VantagePointRelativeTo.MidPoint:
                        {
                            var projetedPos = Vector3D.Lerp(destination, leadPosition, 0.5);
                            var plane = new PlaneD(projetedPos, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        case VantagePointRelativeTo.Shooter:
                        {
                            var plane = new PlaneD(Info.Weapon.MyPivotPos, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        case VantagePointRelativeTo.Target:
                        {
                            var plane = new PlaneD(destination, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        default:
                            heightAdjLeadPos = leadPosition;
                            break;
                    }

                    var destPerspectiveDir = Vector3D.Normalize(heightAdjLeadPos - destination);

                    TargetPosition = MyUtils.LinePlaneIntersection(heightAdjLeadPos, heightDir, destination, destPerspectiveDir);
                    if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                    {
                        DsDebugDraw.DrawLine(heightAdjLeadPos, destination, Color.White, 3);
                        DsDebugDraw.DrawSingleVec(destination, 10, Color.LightSkyBlue);
                    }
                }

                if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                {
                    DsDebugDraw.DrawSingleVec(heightStart, 10, Color.GreenYellow);
                    DsDebugDraw.DrawSingleVec(heightend, 10, Color.LightSkyBlue);
                    DsDebugDraw.DrawSingleVec(TargetPosition, 10, Color.Red);
                }

                bool end1;
                switch (def.EndCondition1)
                {
                    case Conditions.DesiredElevation:
                        var plane = new PlaneD(s.LookAtPos, s.OffsetDir);
                        var distToPlane = plane.DistanceToPoint(Position);
                        var tolernace = def.ElevationTolerance + aConst.CollisionSize;
                        var distFromSurfaceSqr = !Vector3D.IsZero(surfacePos) ? Vector3D.DistanceSquared(Position, surfacePos) : distToPlane * distToPlane;
                        var lessThanTolerance = (def.End1Value + tolernace) * (def.End1Value + tolernace);
                        var greaterThanTolerance = (def.End1Value - tolernace) * (def.End1Value - tolernace);
                        end1 = distFromSurfaceSqr >= greaterThanTolerance && distFromSurfaceSqr <= lessThanTolerance;
                        break;
                    case Conditions.DistanceFromTarget:
                        if (def.EndCondition1 == def.StartCondition1)
                            end1 = start1;
                        else if (def.EndCondition1 == def.StartCondition2)
                            end1 = start2;
                        else
                        {
                            if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                                DsDebugDraw.DrawLine(heightend, destination, Color.Red, 10);
                            end1 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize <= def.End1Value;
                        }
                        break;
                    case Conditions.DistanceToTarget: 
                        if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Green, 10);
                        end1 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize >= def.End1Value;
                        break;
                    case Conditions.Lifetime:
                        end1 = Info.Age >= def.End1Value;
                        break;
                    case Conditions.Deadtime:
                        end1 = Info.Age <= def.End1Value;
                        break;
                    case Conditions.MinTravelRequired:
                        end1 = Info.DistanceTraveled - s.StartDistanceTraveled >= def.End1Value;
                        break;
                    case Conditions.MaxTravelRequired:
                        end1 = Info.DistanceTraveled - s.StartDistanceTraveled <= def.End1Value;
                        break;
                    case Conditions.Ignore:
                        end1 = true;
                        break;
                    default:
                        end1 = false;
                        break;
                }

                bool end2;
                switch (def.EndCondition2)
                {
                    case Conditions.DesiredElevation:
                        var plane = new PlaneD(s.LookAtPos, s.OffsetDir);
                        var distToPlane = plane.DistanceToPoint(Position);
                        var tolernace = def.ElevationTolerance + aConst.CollisionSize;
                        var distFromSurfaceSqr = !Vector3D.IsZero(surfacePos) ? Vector3D.DistanceSquared(Position, surfacePos) : distToPlane * distToPlane;
                        var lessThanTolerance = (def.End2Value + tolernace) * (def.End2Value + tolernace);
                        var greaterThanTolerance = (def.End2Value - tolernace) * (def.End2Value - tolernace);
                        end2 = distFromSurfaceSqr >= greaterThanTolerance && distFromSurfaceSqr <= lessThanTolerance;
                        break;
                    case Conditions.DistanceFromTarget:
                        if (def.EndCondition2 == def.StartCondition1)
                            end2 = start1;
                        else if (def.EndCondition2 == def.StartCondition2)
                            end2 = start2;
                        else
                        {
                            if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                                DsDebugDraw.DrawLine(heightend, destination, Color.Yellow, 10);
                            end2 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize <= def.End2Value;
                        }
                        break;
                    case Conditions.DistanceToTarget: 
                        if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Green, 10);
                        end2 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize >= def.End2Value;
                        break;
                    case Conditions.Lifetime:
                        end2 = Info.Age >= def.End2Value;
                        break;
                    case Conditions.Deadtime:
                        end2 = Info.Age <= def.End2Value;
                        break;
                    case Conditions.MinTravelRequired:
                        end2 = Info.DistanceTraveled - s.StartDistanceTraveled >= def.End2Value;
                        break;
                    case Conditions.MaxTravelRequired:
                        end2 = Info.DistanceTraveled - s.StartDistanceTraveled <= def.End2Value;
                        break;
                    case Conditions.Ignore:
                        end2 = true;
                        break;
                    default:
                        end2 = false;
                        break;
                }

                if (Info.Ai.Session.DebugMod)
                {
                    var session = Info.Ai.Session;
                    if (session.ApproachDebug.ProId == Info.Id || session.Tick != session.ApproachDebug.LastTick)
                    {
                        session.ApproachDebug = new ApproachDebug { 
                            LastTick = session.Tick, Approach = approach, 
                            Start1 = start1, Start2 = start2, End1 = end1, End2 = end2, 
                            ProId = Info.Id, Stage = s.LastActivatedStage
                        };
                    }
                }

                if (approach.EndAnd && end1 && end2 || !approach.EndAnd && (end1 || end2))
                {
                    var hasNextStep = s.RequestedStage + 1 < aConst.ApproachesCount;
                    var isActive = s.LastActivatedStage >= 0;
                    var activeNext = isActive && (def.Failure == StartFailure.Wait || def.Failure == StartFailure.MoveToPrevious || def.Failure == StartFailure.MoveToNext);
                    var inActiveNext = !isActive && def.Failure == StartFailure.MoveToNext;
                    var moveForward = hasNextStep && (activeNext || inActiveNext);
                    var failBackwards = def.Failure == StartFailure.MoveToPrevious && !isActive || def.Failure == StartFailure.ForceReset;

                    if (def.EndEvent == StageEvents.EndProjectile || def.EndEvent == StageEvents.EndProjectileOnFailure && (failBackwards || !moveForward && hasNextStep)) {
                        EndState = EndStates.EarlyEnd;
                        DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                    }

                    if (moveForward)
                    {
                        var oldLast = s.LastActivatedStage;
                        s.LastActivatedStage = s.RequestedStage;
                        ++s.RequestedStage;
                        if (Info.Ai.Session.DebugMod)
                            Log.Line($"stageEnd: age:{Info.Age} - next: {s.RequestedStage} - last:{oldLast} - eCon1:{def.EndCondition1} - eCon2:{def.EndCondition2}");
                        ProcessStage(ref accelMpsMulti, ref speedCapMulti, targetPos, s.LastActivatedStage, targetLock);
                    }
                    else if (failBackwards)
                    {
                        s.LastActivatedStage = s.RequestedStage;
                        var prev = s.RequestedStage;
                        s.RequestedStage = def.OnFailureRevertTo;
                        if (Info.Ai.Session.DebugMod)
                            Log.Line($"stageEnd:age:{Info.Age} - previous:{prev} to {s.RequestedStage} - eCon1:{def.EndCondition1} - eCon2:{def.EndCondition2}");
                    }
                    else if (!hasNextStep)
                    {
                        if (Info.Ai.Session.DebugMod)
                            Log.Line($"Approach ended, no more steps - age:{Info.Age} - strages:[r:{s.RequestedStage} l:{s.LastActivatedStage}] - ec1:{def.EndCondition1} - ec1:{def.End1Value} - ec1:{def.EndCondition2} - ec1:{def.End2Value} - failure:{def.Failure}");
                        s.LastActivatedStage = aConst.Approaches.Length;
                        s.RequestedStage = aConst.Approaches.Length;
                    }
                    else
                    {
                        if (Info.Ai.Session.DebugMod)
                            Log.Line($"end met no valid condition");
                    }
                }
            }
        }

        private Vector3D PlanetSurfaceHeightAdjustment(Vector3D checkPosition, Vector3D upDir, ApproachConstants approach, out Vector3D surfacePos)
        {
            var planetCenter = Info.MyPlanet.PositionComp.WorldAABB.Center;

            Vector3D waterSurfacePos = checkPosition;
            double waterSurface = 0;

            WaterData water = null;
            if (Info.Weapon.Comp.Session.WaterApiLoaded && Info.Weapon.Comp.Session.WaterMap.TryGetValue(Info.MyPlanet.EntityId, out water))
            {
                waterSurfacePos = WaterModAPI.GetClosestSurfacePoint(checkPosition, water.Planet);
                Vector3D.DistanceSquared(ref waterSurfacePos, ref planetCenter, out waterSurface);
            }

            var voxelSurfacePos = Info.MyPlanet.GetClosestSurfacePointGlobal(ref checkPosition);

            double surfaceToCenterSqr;
            Vector3D.DistanceSquared(ref voxelSurfacePos, ref planetCenter, out surfaceToCenterSqr);

            surfacePos = surfaceToCenterSqr > waterSurface ? voxelSurfacePos : waterSurfacePos;

            return surfacePos + (upDir * approach.Definition.DesiredElevation);
        }

        private static Vector3D ProNavControl(Vector3D currentDir, Vector3D velocity, Vector3D commandAccel, PreComputedMath preComp, out bool isNormalized)
        {
            Vector3D actualHeading;
            isNormalized = false;
            if (velocity.LengthSquared() < MathHelper.EPSILON10 || commandAccel.LengthSquared() < MathHelper.EPSILON10)
                actualHeading = commandAccel;
            else if (Vector3D.Dot(currentDir, Vector3D.Normalize(commandAccel)) < preComp.SteeringCos)
            {
                isNormalized = true;
                var normalVec = Vector3D.Normalize(Vector3D.Cross(Vector3D.Cross(currentDir, commandAccel), currentDir));

                if (normalVec.LengthSquared() < MathHelper.EPSILON10)
                    normalVec = Vector3D.CalculatePerpendicularVector(currentDir);

                actualHeading = preComp.SteeringSign * currentDir * preComp.SteeringParallelLen + normalVec * preComp.SteeringNormLen;
            }
            else
                actualHeading = commandAccel;

            return actualHeading;
        }

        private void ApproachOrbits(Vector3D targetHeightOffset, double orbitHeight)
        {
            //Crap in cfg
            var orbitRadius = 100; //Desired orbit radius, from point above target
            var orbitTolerance = 5; //Tolerance of orbit

            //Aconst?
            var fixedMagicNumber = Math.Sqrt(orbitHeight * orbitHeight + orbitRadius + orbitRadius); // calc this once per approach and cache?

            //Runtime
            var orbitCenter = TargetPosition + targetHeightOffset; 

            var targDist = Vector3D.Distance(Position, TargetPosition);
            var orbDist = Vector3D.Distance(Position, orbitCenter);

            var tooFar = orbDist > orbitRadius + orbitTolerance;
            var tooClose = orbDist < orbDist - orbitTolerance;

            if (!tooFar && !tooClose) //Distance to orbit ctr is good if true
            {
                if (targDist - orbitTolerance > fixedMagicNumber) ; //Too high, head lower
                if (targDist + orbitTolerance < fixedMagicNumber) ; //Too low, climb
            }

            if (tooFar) ;//Head toward orbitCenter
            if (tooClose) ;//Head away from orbitCenter

            //but that means "fixedmagicnumber" is no longer static for that particular approach
            //maybe we let them specify a min value, but otherwise it uses the sphere circumfuse at that sphere position/angle
            //so orbitheight = desired height above target + targetVolume * 0.5
            //orbitradius = desired radius + targetVolume * 0.5
        }

        #endregion

        #region Drones
        internal void RunDrone()
        {
            var ammo = Info.AmmoDef;
            var aConst = ammo.Const;
            var s = Info.Storage;
            var newVel = new Vector3D();
            var w = Info.Weapon;
            var comp = w.Comp;
            var parentEnt = comp.TopEntity;

            if (s.DroneStat == Launch)
                DroneLaunch(parentEnt, aConst, s);

            if (s.DroneStat != Launch)//Start of main nav after clear of launcher
                DroneNav(parentEnt, ref newVel);
            else
            {
                newVel = Velocity + (Info.Direction * (aConst.DeltaVelocityPerTick * comp.Session.DeltaTimeRatio));
                VelocityLengthSqr = newVel.LengthSquared();

                if (VelocityLengthSqr > MaxSpeed * MaxSpeed)
                    newVel = (Info.Direction * 0.95 + Vector3D.CalculatePerpendicularVector(Info.Direction) * 0.05) * MaxSpeed;

                Velocity = newVel;
            }
        }

        private void DroneLaunch(MyEntity parentEnt, AmmoConstants aConst, SmartStorage s)
        {
            if (s.DroneStat == Launch && Info.DistanceTraveled * Info.DistanceTraveled >= aConst.SmartsDelayDistSqr && Info.Ai.AiType == Ai.AiTypes.Grid)//Check for LOS & delaytrack after launch
            {
                var lineCheck = new LineD(Position, TargetPosition);
                var startTrack = !new MyOrientedBoundingBoxD(parentEnt.PositionComp.LocalAABB, parentEnt.PositionComp.WorldMatrixRef).Intersects(ref lineCheck).HasValue;

                if (startTrack)
                    s.DroneStat = Transit;
            }
            else if (Info.Ai.AiType != Ai.AiTypes.Grid)
                s.DroneStat = Transit;
        }

        private void DroneNav(MyEntity parentEnt, ref Vector3D newVel)
        {
            var ammo = Info.AmmoDef;
            var aConst = ammo.Const;
            var s = Info.Storage;
            var w = Info.Weapon;
            var comp = w.Comp;
            var target = Info.Target;

            var tasks = comp.Data.Repo.Values.State.Tasks;
            var updateTask = tasks.UpdatedTick == Info.Ai.Session.Tick - 1;
            var tracking = aConst.DeltaVelocityPerTick <= 0 || (s.DroneStat == Dock || Vector3D.DistanceSquared(Info.Origin, Position) >= aConst.SmartsDelayDistSqr);
            var parentPos = Vector3D.Zero;

            if (!updateTask)//Top level check for a current target or update to tasks
                UpdateExistingTargetState(parentEnt, target, aConst, s);
            else
                UpdateTask(parentEnt, target, aConst, s);

            //Hard break, everything below sorts out the best navigation action to conduct based on the drone position and target/status/mission info from above

            //General use vars
            var targetSphere = s.NavTargetBound;
            var orbitSphere = targetSphere; //desired orbit dist
            var orbitSphereClose = targetSphere; //"Too close" or collision imminent

            DroneMissions(parentEnt, ref orbitSphere, ref orbitSphereClose, ref targetSphere, aConst, s, ref parentPos);

            if (w.System.WConst.DebugMode && !w.System.Session.DedicatedServer)
                DroneDebug(ref orbitSphere);

            if (tracking && s.DroneMsn != DroneMission.Rtb && !DroneTracking(target, s, aConst))
                return;

            if (s.DroneMsn == DroneMission.Rtb || tracking)
                ComputeSmartVelocity(ref orbitSphere, ref orbitSphereClose, ref targetSphere, ref parentPos, out newVel);

            UpdateSmartVelocity(newVel, tracking);
        }

        private void DroneMissions(MyEntity parentEnt, ref BoundingSphereD orbitSphere, ref BoundingSphereD orbitSphereClose, ref BoundingSphereD targetSphere, AmmoConstants aConst, SmartStorage s, ref Vector3D parentPos)
        {
            var comp = Info.Weapon.Comp;
            var ammo = Info.AmmoDef;
            var speedLimitPerTick = aConst.AmmoSkipAccel ? DesiredSpeed : aConst.AccelInMetersPerSec;
            var fragProx = aConst.FragProximity;
            var hasObstacle = s.Obstacle.Entity != parentEnt && comp.Session.Tick - 1 == s.Obstacle.LastSeenTick;
            var hasStrafe = ammo.Fragment.TimedSpawns.PointType == PointTypes.Direct && ammo.Fragment.TimedSpawns.PointAtTarget == false;
            var hasKamikaze = ammo.AreaOfDamage.ByBlockHit.Enable || (ammo.AreaOfDamage.EndOfLife.Enable && Info.Age >= ammo.AreaOfDamage.EndOfLife.MinArmingTime); //check for explosive payload on drone
            var maxLife = aConst.MaxLifeTime;
            var orbitSphereFar = orbitSphere; //Indicates start of approach

            switch (s.DroneMsn)
            {
                case DroneMission.Attack:

                    orbitSphere.Radius += fragProx;
                    orbitSphereFar.Radius += fragProx + speedLimitPerTick + MaxSpeed; //first whack at dynamic setting   
                    orbitSphereClose.Radius += MaxSpeed * 0.3f + ammo.Shape.Diameter; //Magic number, needs logical work?
                    if (hasObstacle && orbitSphereClose.Contains(s.Obstacle.Entity.PositionComp.GetPosition()) != ContainmentType.Contains && s.DroneStat != Kamikaze)
                    {
                        orbitSphereClose = s.Obstacle.Entity.PositionComp.WorldVolume;
                        orbitSphereClose.Radius = s.Obstacle.Entity.PositionComp.WorldVolume.Radius + MaxSpeed * 0.3f;
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
                            else if (s.DroneStat != Escape)
                            {
                                switch (hasStrafe)
                                {
                                    case false:
                                        s.DroneStat = Orbit;
                                        break;
                                    case true:
                                        {
                                            var fragInterval = aConst.FragInterval;
                                            var fragGroupDelay = aConst.FragGroupDelay;
                                            var timeSinceLastFrag = Info.Age - Info.LastFragTime;

                                            if (fragGroupDelay == 0 && timeSinceLastFrag >= fragInterval)
                                                s.DroneStat = Strafe;//TODO incorporate group delays
                                            else if (fragGroupDelay > 0 && (timeSinceLastFrag >= fragGroupDelay || timeSinceLastFrag <= fragInterval))
                                                s.DroneStat = Strafe;
                                            else s.DroneStat = Orbit;
                                            break;
                                        }
                                }
                            }
                        }
                        else if (s.DroneStat == Transit && orbitSphereFar.Contains(Position) != ContainmentType.Disjoint)
                        {
                            s.DroneStat = Approach;
                        }
                    }
                    else if (s.DroneStat == Escape)
                    {
                        if (orbitSphere.Contains(Position) == ContainmentType.Disjoint)
                            s.DroneStat = Orbit;
                    }

                    if ((hasKamikaze) && s.DroneStat != Kamikaze && maxLife > 0)//Parenthesis for everyone!
                    {
                        var kamiFlightTime = orbitSphere.Radius / MaxSpeed * 60 * 1.05; //time needed for final dive into target
                        if (maxLife - Info.Age <= kamiFlightTime || (Info.Frags >= aConst.MaxFrags))
                        {
                            s.DroneStat = Kamikaze;
                        }
                    }
                    else if (!hasKamikaze && s.NavTargetEnt != parentEnt)
                    {
                        parentPos = comp.CoreEntity.PositionComp.WorldAABB.Center;
                        if (parentPos != Vector3D.Zero && s.DroneStat != Return)
                        {
                            var rtbFlightTime = Vector3D.Distance(Position, parentPos) / MaxSpeed * 60 * 1.1d;//added multiplier to ensure final docking time?
                            if ((maxLife > 0 && maxLife - Info.Age <= rtbFlightTime) || (Info.Frags >= aConst.MaxFrags))
                            {
                                var rayTestPath = new RayD(Position, Vector3D.Normalize(parentPos - Position));//Check for clear LOS home
                                if (rayTestPath.Intersects(orbitSphereClose) == null)
                                {
                                    s.DroneMsn = DroneMission.Rtb;
                                    s.DroneStat = Transit;
                                }
                            }
                        }
                    }
                    break;
                case DroneMission.Defend:
                    orbitSphere.Radius += fragProx / 2;
                    orbitSphereFar.Radius += speedLimitPerTick + MaxSpeed;
                    orbitSphereClose.Radius += MaxSpeed * 0.3f + ammo.Shape.Diameter;
                    if (hasObstacle)
                    {
                        orbitSphereClose = s.Obstacle.Entity.PositionComp.WorldVolume;
                        orbitSphereClose.Radius = s.Obstacle.Entity.PositionComp.WorldVolume.Radius + MaxSpeed * 0.3f;
                        s.DroneStat = Escape;
                        break;
                    }

                    if (s.DroneStat == Escape) s.DroneStat = Transit;

                    if (s.DroneStat != Transit && orbitSphereFar.Contains(Position) == ContainmentType.Disjoint)
                    {
                        s.DroneStat = Transit;
                        break;
                    }

                    if (s.DroneStat != Transit)
                    {
                        if (orbitSphere.Contains(Position) != ContainmentType.Disjoint)
                        {
                            s.DroneStat = orbitSphereClose.Contains(Position) != ContainmentType.Disjoint ? Escape : Orbit;
                        }
                    }
                    else if (orbitSphereFar.Contains(Position) != ContainmentType.Disjoint && (s.DroneStat == Transit || s.DroneStat == Orbit))
                    {
                        s.DroneStat = Approach;
                    }

                    parentPos = comp.CoreEntity.PositionComp.WorldAABB.Center;
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

                    if (hasObstacle && s.DroneStat != Dock)
                    {
                        orbitSphereClose = s.Obstacle.Entity.PositionComp.WorldVolume;
                        orbitSphereClose.Radius = s.Obstacle.Entity.PositionComp.WorldVolume.Radius + MaxSpeed * 0.3f;
                        s.DroneStat = Escape;
                        break;
                    }

                    if (s.DroneStat == Escape) s.DroneStat = Transit;

                    if (s.DroneStat != Return && s.DroneStat != Dock)
                    {
                        if (orbitSphere.Contains(Position) != ContainmentType.Disjoint)
                        {
                            s.DroneStat = orbitSphereClose.Contains(Position) != ContainmentType.Disjoint ? Escape : Return;
                        }
                        else if (orbitSphereFar.Contains(Position) != ContainmentType.Disjoint && (s.DroneStat == Transit || s.DroneStat == Orbit))
                        {
                            s.DroneStat = Approach;
                        }
                    }
                    break;
            }

        }

        private void DroneDebug(ref BoundingSphereD orbitSphere)
        {
            var s = Info.Storage;
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


        private bool DroneTracking(Target target, SmartStorage s, AmmoConstants aConst)
        {
            var validEntity = target.TargetState == Target.TargetStates.IsEntity && !((MyEntity)target.TargetObject).MarkedForClose;
            var timeSlot = (Info.Age + s.SmartSlot) % 30 == 0;
            var hadTarget = HadTarget != HadTargetState.None;
            var overMaxTargets = hadTarget && TargetsSeen > aConst.MaxTargets && aConst.MaxTargets != 0;
            var fake = target.TargetState == Target.TargetStates.IsFake;
            var validTarget = fake || target.TargetState == Target.TargetStates.IsProjectile || validEntity && !overMaxTargets;
            var seekFirstTarget = !hadTarget && !validTarget && s.PickTarget && (Info.Age > 120 && timeSlot || Info.Age % 30 == 0 && Info.IsFragment);
            var gaveUpChase = !fake && Info.Age - s.ChaseAge > aConst.MaxChaseTime && hadTarget;
            var isZombie = aConst.CanZombie && hadTarget && !fake && !validTarget && s.ZombieLifeTime > 0 && (s.ZombieLifeTime + s.SmartSlot) % 30 == 0;
            var seekNewTarget = timeSlot && hadTarget && !validEntity && !overMaxTargets;
            var needsTarget = (s.PickTarget && timeSlot || seekNewTarget || gaveUpChase && validTarget || isZombie || seekFirstTarget);

            if (needsTarget && NewTarget() || validTarget)
                TrackSmartTarget(fake);
            else if (!SmartRoam())
                return false;

            return true;
        }

        private void UpdateExistingTargetState(MyEntity parentEnt, Target target, AmmoConstants aConst, SmartStorage s)
        {
            var comp = Info.Weapon.Comp;
            var fragProx = aConst.FragProximity;
            var tasks = Info.Weapon.Comp.Data.Repo.Values.State.Tasks;
            var hasTarget = false;

            switch (HadTarget)//Internal drone target reassignment
            {
                case HadTargetState.Entity:
                    var entity = target.TargetObject as MyEntity;
                    if (entity != null && !entity.MarkedForClose)
                    {
                        hasTarget = true;
                    }
                    else
                    {
                        NewTarget();
                        var myEntity = target.TargetObject as MyEntity;
                        if (myEntity != null)
                        {
                            s.NavTargetEnt = myEntity.GetTopMostParent();
                            s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;
                            hasTarget = true;
                        }
                    }
                    break;
                case HadTargetState.Projectile: //TODO evaluate whether TargetBound should remain unchanged (ie, keep orbiting assigned target but shoot at projectile)
                    if (target.TargetState == Target.TargetStates.IsProjectile)
                        NewTarget();

                    break;
                case HadTargetState.Fake:
                    if (s.DummyTargets != null)
                    {
                        var fakeTarget = s.DummyTargets.PaintedTarget.EntityId != 0 ? s.DummyTargets.PaintedTarget : s.DummyTargets.ManualTarget;
                        if (fakeTarget == s.DummyTargets.PaintedTarget)
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
                            s.SetTargetPos = fakeTarget.FakeInfo.WorldPosition;
                            hasTarget = true;
                        }
                    }
                    else
                        NewTarget();
                    break;
            }

            if (s.NavTargetEnt != null && hasTarget)
                s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;//Refresh position info
                                                                           //Logic to handle loss of target and reassigment to come home
            if (!hasTarget && s.DroneMsn == DroneMission.Attack)
            {
                s.DroneMsn = DroneMission.Defend;//Try to return to parent in defensive state
                s.NavTargetBound = parentEnt.PositionComp.WorldVolume;
                s.NavTargetEnt = parentEnt;
            }
            else if (s.DroneMsn == DroneMission.Rtb || s.DroneMsn == DroneMission.Defend)
            {
                if (s.DroneMsn == DroneMission.Rtb || tasks.FriendId == 0)
                {
                    s.NavTargetBound = parentEnt.PositionComp.WorldVolume;
                    s.NavTargetEnt = parentEnt;
                }
                else if (tasks.Friend != null && s.DroneMsn != DroneMission.Rtb && tasks.Friend != null)//If all else fails, try to protect a friendly
                {
                    s.NavTargetBound = tasks.Friend.PositionComp.WorldVolume;
                    s.NavTargetEnt = tasks.Friend;
                }
            }
        }

        private void UpdateTask(MyEntity parentEnt, Target target, AmmoConstants aConst, SmartStorage s)
        {
            var comp = Info.Weapon.Comp;
            var tasks = comp.Data.Repo.Values.State.Tasks;
            var fragProx = aConst.FragProximity;

            switch (tasks.Task)
            {
                case Tasks.Attack:
                    s.DroneMsn = DroneMission.Attack;
                    s.NavTargetEnt = tasks.Enemy;
                    s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;
                    var tTargetDist = Vector3D.Distance(Position, tasks.Enemy.PositionComp.WorldVolume.Center);
                    target.Set(tasks.Enemy, tasks.Enemy.PositionComp.WorldVolume.Center, tTargetDist, tTargetDist, tasks.EnemyId);
                    break;
                case Tasks.Defend:
                    s.DroneMsn = DroneMission.Defend;
                    s.NavTargetEnt = tasks.Friend;
                    s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;
                    break;
                case Tasks.Screen:
                    s.DroneMsn = DroneMission.Defend;
                    s.NavTargetEnt = parentEnt;
                    s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;
                    break;
                case Tasks.Recall:
                    s.DroneMsn = DroneMission.Rtb;
                    s.NavTargetEnt = parentEnt;
                    s.NavTargetBound = s.NavTargetEnt.PositionComp.WorldVolume;
                    break;
                case Tasks.RoamAtPoint:
                    s.DroneMsn = DroneMission.Defend;
                    s.NavTargetEnt = null;
                    s.NavTargetBound = new BoundingSphereD(tasks.Position, fragProx * 0.5f);
                    break;
                case Tasks.None:
                    break;
            }
            s.DroneStat = Transit;
        }


        private void OffsetSmartVelocity(ref Vector3D commandedAccel)
        {
            var ammo = Info.AmmoDef;
            var aConst = Info.AmmoDef.Const;

            var smarts = ammo.Trajectory.Smarts;
            var s = Info.Storage;
            var speedLimitPerTick = aConst.AmmoSkipAccel ? DesiredSpeed : aConst.AccelInMetersPerSec;

            var offsetTime = smarts.OffsetTime;
            var revCmdAccel = -commandedAccel / speedLimitPerTick;
            var revOffsetDir = MyUtils.IsZero(s.RandOffsetDir.X - revCmdAccel.X, 1E-03f) && MyUtils.IsZero(s.RandOffsetDir.Y - revCmdAccel.Y, 1E-03f) && MyUtils.IsZero(Info.Storage.RandOffsetDir.Z - revCmdAccel.Z, 1E-03f);

            if (Info.Age % offsetTime == 0 || revOffsetDir)
            {

                double angle = Info.Random.NextDouble() * MathHelper.TwoPi;
                var up = Vector3D.CalculatePerpendicularVector(Info.Direction);
                var right = Vector3D.Cross(Info.Direction, up);
                s.RandOffsetDir = Math.Sin(angle) * up + Math.Cos(angle) * right;
                s.RandOffsetDir *= smarts.OffsetRatio;
            }

            commandedAccel += speedLimitPerTick * s.RandOffsetDir;
            commandedAccel = Vector3D.Normalize(commandedAccel) * speedLimitPerTick;
        }

        private void ComputeSmartVelocity(ref BoundingSphereD orbitSphere, ref BoundingSphereD orbitSphereClose, ref BoundingSphereD targetSphere, ref Vector3D parentPos, out Vector3D newVel)
        {
            var s = Info.Storage;
            var droneNavTarget = Vector3D.Zero;
            var ammo = Info.AmmoDef;
            var smarts = ammo.Trajectory.Smarts;

            var aConst = Info.AmmoDef.Const;
            var parentCubePos = Info.Weapon.Comp.CoreEntity.PositionComp.GetPosition();
            var parentCubeOrientation = Info.Weapon.Comp.CoreEntity.PositionComp.GetOrientation();
            var droneSize = Math.Max(ammo.Shape.Diameter, 5);//Minimum drone "size" clamped to 5m for nav purposes, prevents chasing tiny points in space
            var speedLimitPerTick = aConst.AmmoSkipAccel ? DesiredSpeed : aConst.AccelInMetersPerSec;

            switch (s.DroneStat)
            {
                case Transit:
                    droneNavTarget = Vector3D.Normalize(targetSphere.Center - Position);
                    break;
                case Approach:
                    if (s.DroneMsn == DroneMission.Rtb)//Check for LOS to docking target
                    {
                        var returnTargetTest = new Vector3D(parentCubePos + parentCubeOrientation.Forward * orbitSphere.Radius);
                        var droneNavTargetAim = returnTargetTest;
                        var testPathRayCheck = new RayD(returnTargetTest, -droneNavTargetAim);//Ray looking out from dock approach point

                        if (testPathRayCheck.Intersects(orbitSphereClose) == null)
                        {
                            s.DroneStat = Return;
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
                    if (double.IsNaN(droneNavTarget.X) || Vector3D.IsZero(droneNavTarget)) droneNavTarget = Info.Direction; //Error catch
                    break;

                case Orbit://Orbit & shoot behavior
                    var insideOrbitSphere = new BoundingSphereD(orbitSphere.Center, orbitSphere.Radius * 0.90);
                    if (insideOrbitSphere.Contains(Position) != ContainmentType.Disjoint)
                    {
                        droneNavTarget = Position + (Info.Direction + new Vector3D(0, 0.5, 0));//Strafe or too far inside sphere recovery
                    }
                    else
                    {
                        var noseOffset = new Vector3D(Position + (Info.Direction * (speedLimitPerTick)));
                        double length;
                        Vector3D.Distance(ref orbitSphere.Center, ref noseOffset, out length);
                        var dir = (noseOffset - orbitSphere.Center) / length;
                        var deltaDist = length - orbitSphere.Radius * 0.95; //0.95 modifier for hysterisis, keeps target inside dronesphere
                        var navPoint = noseOffset + (-dir * deltaDist);
                        droneNavTarget = Vector3D.Normalize(navPoint - Position);
                    }
                    break;

                case Strafe:
                    droneNavTarget = Vector3D.Normalize(TargetPosition - Position);
                    break;
                case Escape:
                    var metersInSideOrbit = MyUtils.GetSmallestDistanceToSphere(ref Position, ref orbitSphereClose);
                    if (metersInSideOrbit < 0)
                    {
                        var futurePos = (Position + (TravelMagnitude * Math.Abs(metersInSideOrbit * 0.5)));
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
                    droneNavTarget = Vector3D.Normalize(TargetPosition - Position);
                    break;
                case Return:
                    var returnTarget = new Vector3D(parentCubePos + parentCubeOrientation.Forward * orbitSphere.Radius);
                    droneNavTarget = Vector3D.Normalize(returnTarget - Position);
                    DeaccelRate = 30;
                    if (Vector3D.Distance(Position, returnTarget) <= droneSize) s.DroneStat = Dock;
                    break;
                case Dock: //This is ugly and I hate it...
                    var sphereTarget = new Vector3D(parentCubePos + parentCubeOrientation.Forward * (orbitSphereClose.Radius + MaxSpeed / 2));

                    if (Vector3D.Distance(sphereTarget, Position) >= droneSize)
                    {
                        if (DeaccelRate >= 25)//Final Approach
                        {
                            droneNavTarget = Vector3D.Normalize(sphereTarget - Position);
                            DeaccelRate = 25;
                        }

                    }
                    else if (DeaccelRate >= 25)
                    {
                        DeaccelRate = 15;
                    }

                    if (DeaccelRate <= 15)
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

            // var commandedAccel = s.Navigation.Update(Position, Velocity, speedLimitPerTick, droneNavTarget, PrevTargetVel, Gravity, smarts.Aggressiveness, Info.AmmoDef.Const.MaxLateralThrust, smarts.NavAcceleration);
            var missileToTarget = droneNavTarget;
            var relativeVelocity = PrevTargetVel - Velocity;
            var normalMissileAcceleration = (relativeVelocity - (relativeVelocity.Dot(missileToTarget) * missileToTarget)) * smarts.Aggressiveness;

            Vector3D commandedAccel;
            if (Vector3D.IsZero(normalMissileAcceleration))
            {
                commandedAccel = (missileToTarget * aConst.AccelInMetersPerSec);
            }
            else
            {
                var maxLateralThrust = aConst.AccelInMetersPerSec * Math.Min(1, Math.Max(0, Info.AmmoDef.Const.MaxLateralThrust));
                if (normalMissileAcceleration.LengthSquared() > maxLateralThrust * maxLateralThrust)
                {
                    Vector3D.Normalize(ref normalMissileAcceleration, out normalMissileAcceleration);
                    normalMissileAcceleration *= maxLateralThrust;
                }
                commandedAccel = Math.Sqrt(Math.Max(0, aConst.AccelInMetersPerSec * aConst.AccelInMetersPerSec - normalMissileAcceleration.LengthSquared())) * missileToTarget + normalMissileAcceleration;
            }
            if (smarts.OffsetTime > 0 && s.DroneStat != Strafe && s.DroneStat != Return && s.DroneStat != Dock) // suppress offsets when strafing or docking
                OffsetSmartVelocity(ref commandedAccel);

            newVel = Velocity + (commandedAccel * DeltaStepConst);

            Vector3D.Normalize(ref newVel, out Info.Direction);
        }

        private bool SmartRoam()
        {
            var smarts = Info.AmmoDef.Trajectory.Smarts;
            var hadTaret = HadTarget != HadTargetState.None;
            TargetPosition = Position + (Info.Direction * Info.MaxTrajectory);

            if (Info.Storage.ZombieLifeTime++ > Info.AmmoDef.Const.TargetLossTime && !smarts.KeepAliveAfterTargetLoss && (smarts.NoTargetExpire || hadTaret))
            {
                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                EndState = EndStates.EarlyEnd;
            }

            return true;
        }

        private void UpdateSmartVelocity(Vector3D newVel, bool tracking)
        {
            if (!tracking)
                newVel = Velocity += (Info.Direction * (Info.AmmoDef.Const.DeltaVelocityPerTick * Info.Ai.Session.DeltaTimeRatio));
            VelocityLengthSqr = newVel.LengthSquared();

            if (VelocityLengthSqr > MaxSpeed * MaxSpeed || (DeaccelRate < 100 && Info.AmmoDef.Const.IsDrone)) newVel = Info.Direction * MaxSpeed * DeaccelRate / 100;

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

            var eTarget = Info.Target.TargetObject as MyEntity;
            var pTarget = Info.Target.TargetObject as Projectile;

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
            else if (Info.Target.TargetState == Target.TargetStates.IsProjectile && pTarget != null)
            {
                targetPos = pTarget.Position;
                HadTarget = HadTargetState.Projectile;
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsEntity && eTarget != null)
            {
                targetPos = eTarget.PositionComp.WorldAABB.Center;
                HadTarget = HadTargetState.Entity;
                physics = eTarget.Physics;

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

            TargetPosition = targetPos;

            var tVel = Vector3.Zero;
            if (fake && fakeTargetInfo != null)
            {
                tVel = fakeTargetInfo.LinearVelocity;
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsProjectile && pTarget != null)
            {
                tVel = pTarget.Velocity;
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
        #endregion

        #region Targeting
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
            var aConst = Info.AmmoDef.Const;
            var s = Info.Storage;
            var giveUp = HadTarget != HadTargetState.None && ++TargetsSeen > aConst.MaxTargets && aConst.MaxTargets != 0;
            s.ChaseAge = Info.Age;
            s.PickTarget = false;
            var eTarget = Info.Target.TargetObject as MyEntity;
            var pTarget = Info.Target.TargetObject as Projectile;
            var newTarget = true;

            var oldTarget = Info.Target.TargetObject;
            if (HadTarget != HadTargetState.Projectile)
            {
                if (giveUp || !Ai.ReacquireTarget(this))
                {
                    var activeEntity = Info.Target.TargetState == Target.TargetStates.IsEntity && eTarget != null;
                    var badEntity = !Info.AcquiredEntity && activeEntity && eTarget.MarkedForClose || Info.AcquiredEntity && activeEntity && (eTarget.GetTopMostParent()?.MarkedForClose ?? true);
                    if (!giveUp && !Info.AcquiredEntity || Info.AcquiredEntity && giveUp || !Info.AmmoDef.Trajectory.Smarts.NoTargetExpire || badEntity)
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
                    pTarget?.Seekers.Remove(this);

                if (giveUp || !Ai.ReAcquireProjectile(this))
                {
                    if (Info.Target.TargetState == Target.TargetStates.IsProjectile)
                        Info.Target.Reset(Info.Ai.Session.Tick, Target.States.ProjectileNewTarget);

                    newTarget = false;
                }
            }

            if (newTarget && aConst.Health > 0 && !aConst.IsBeamWeapon && (Info.Target.TargetState == Target.TargetStates.IsFake || Info.Target.TargetObject != null && oldTarget != Info.Target.TargetObject))
                Info.Ai.Session.Projectiles.AddProjectileTargets(this);

            if (aConst.ProjectileSync && Info.Ai.Session.IsServer && (Info.Target.TargetState != Target.TargetStates.IsFake || Info.Target.TargetState != Target.TargetStates.IsProjectile))
            {
                s.LastProSyncStateAge = Info.Age;
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
            var eTarget = Info.Target.TargetObject as MyEntity;

            if (eTarget?.GetTopMostParent()?.Physics?.LinearVelocity == null)
            {
                targetDirection = Vector3D.Zero;
                return false;
            }

            var targetPos = eTarget.PositionComp.WorldAABB.Center;

            if (aConst.FragPointType == PointTypes.Direct)
            {
                targetDirection = Vector3D.Normalize(targetPos - Position);
                return true;
            }


            var targetVel = eTarget.GetTopMostParent().Physics.LinearVelocity;
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
            Info.Storage.RequestedStage = -2;
            EndState = EndStates.None;
            var ent = (MyEntity)Info.Target.TargetObject;

            var targetPos = ent.PositionComp.WorldAABB.Center;
            var deltaPos = targetPos - Position;
            var targetVel = ent.Physics?.LinearVelocity ?? Vector3.Zero;
            var deltaVel = targetVel - Vector3.Zero;
            var timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, DesiredSpeed);
            var predictedPos = targetPos + (float)timeToIntercept * deltaVel;
            var ammo = Info.AmmoDef;
            var aConst = ammo.Const;
            TargetPosition = predictedPos;

            if (ammo.Trajectory.Guidance == TrajectoryDef.GuidanceType.DetectFixed) return;
            Vector3D.DistanceSquared(ref Info.Origin, ref predictedPos, out DistanceToTravelSqr);
            Info.DistanceTraveled = 0;
            Info.PrevDistanceTraveled = 0;

            Info.Direction = Vector3D.Normalize(predictedPos - Position);
            VelocityLengthSqr = 0;

            if (aConst.AmmoSkipAccel)
            {
                Velocity = (Info.Direction * MaxSpeed);
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity += Info.Direction * (aConst.DeltaVelocityPerTick * Info.Ai.Session.DeltaTimeRatio);

            if (ammo.Trajectory.Guidance == TrajectoryDef.GuidanceType.DetectSmart)
            {
                if (aConst.TargetOffSet)
                {
                    OffSetTarget();
                }
                else
                {
                    OffsetTarget = Vector3D.Zero;
                }
            }

            TravelMagnitude = Velocity * DeltaStepConst;
        }


        internal void SeekEnemy()
        {
            var mineInfo = Info.AmmoDef.Trajectory.Mines;
            var detectRadius = mineInfo.DetectRadius;
            var deCloakRadius = mineInfo.DeCloakRadius;

            var targetEnt = Info.Target.TargetObject as MyEntity;

            var wakeRadius = detectRadius > deCloakRadius ? detectRadius : deCloakRadius;
            PruneSphere = new BoundingSphereD(Position, wakeRadius);
            var inRange = false;
            var activate = false;
            var minDist = double.MaxValue;
            if (Info.Storage.RequestedStage != -2)
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
                    Info.Target.TargetObject = closestEnt;
                }
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsEntity && targetEnt != null && !targetEnt.MarkedForClose)
            {
                var entSphere = targetEnt.PositionComp.WorldVolume;
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

            if (minDist <= Info.AmmoDef.Const.CollisionSize) 
                activate = true;
            if (minDist <= detectRadius && Info.Target.TargetObject is MyEntity) 
                inRange = true;

            if (Info.Storage.RequestedStage == -2)
            {
                if (!inRange)
                    TriggerMine(true);
            }
            else if (inRange) ActivateMine();

            if (activate)
            {
                TriggerMine(false);
                if (targetEnt != null) 
                    MyEntityList.Add(targetEnt);
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
            Info.Storage.RequestedStage = -3; // stage1, Guidance == DetectSmart and DistanceToTravelSqr != double.MaxValue means smart tracking is active.
        }

        internal void ResetMine()
        {
            if (Info.Storage.RequestedStage == -3)
            {
                Info.DistanceTraveled = double.MaxValue;
                DeaccelRate = 0;
                return;
            }

            DeaccelRate = Info.AmmoDef.Const.Ewar || Info.AmmoDef.Const.IsMine ? Info.AmmoDef.Trajectory.DeaccelTime : 0;
            DistanceToTravelSqr = Info.MaxTrajectory * Info.MaxTrajectory;

            Info.AvShot.Triggered = false;
            Info.Storage.LastActivatedStage = Info.Storage.RequestedStage;
            Info.Storage.RequestedStage = -1;
            TargetPosition = Vector3D.Zero;
            if (Info.AmmoDef.Trajectory.Guidance == TrajectoryDef.GuidanceType.DetectSmart)
            {
                OffsetTarget = Vector3D.Zero;
            }

            Info.Direction = Vector3D.Zero;

            Velocity = Vector3D.Zero;
            TravelMagnitude = Vector3D.Zero;
            VelocityLengthSqr = 0;
        }

        #endregion

        #region Ewar
        internal void RunEwar()
        {
            if (Info.AmmoDef.Const.Pulse && !Info.EwarAreaPulse && (VelocityLengthSqr <= 0 || EndState == EndStates.AtMaxRange) && !Info.AmmoDef.Const.IsMine)
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

                    var s = Info.Ai.Session;
                    DynTrees.GetAllProjectilesInSphere(Info.Ai.Session, ref eWarSphere, s.EwaredProjectiles, false);
                    for (int j = 0; j < s.EwaredProjectiles.Count; j++)
                    {
                        var netted = s.EwaredProjectiles[j];

                        if (eWarSphere.Intersects(new BoundingSphereD(netted.Position, netted.Info.AmmoDef.Const.CollisionSize)))
                        {
                            if (netted.Info.Ai.TopEntityMap.GroupMap.Construct.ContainsKey(Info.Weapon.Comp.TopEntity) || netted.Info.Target.TargetState == Target.TargetStates.IsProjectile) continue;
                            if (Info.Random.NextDouble() * 100f < Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                            {
                                Info.BaseEwarPool -= (float)netted.Info.AmmoDef.Const.HealthHitModifier;
                                if (Info.BaseEwarPool <= 0 && Info.BaseHealthPool-- > 0)
                                {
                                    Info.EwarActive = true;
                                    netted.Info.Target.TargetObject = this;
                                    netted.Info.Target.TargetState = Target.TargetStates.IsProjectile;
                                    Seekers.Add(netted);
                                }
                            }
                        }
                    }
                    s.EwaredProjectiles.Clear();
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
                        var eTarget = Info.Target.TargetObject as MyEntity;
                        var radius = eTarget != null ? eTarget.PositionComp.LocalVolume.Radius : 1;
                        var targetSphere = new BoundingSphereD(TargetPosition, radius);

                        MathFuncs.Cone aimCone;
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
                var shrapnel = projectiles.ShrapnelPool.Count > 0 ? projectiles.ShrapnelPool.Pop() : new Fragments();
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
            var possiblePos = BoundingBoxD.CreateFromSphere(new BoundingSphereD(Position, ((MaxSpeed) * (steps + 1) * DeltaStepConst) + Info.AmmoDef.Const.CollisionSize));
            if (MyGamePruningStructure.AnyVoxelMapInBox(ref possiblePos))
            {
                PruneQuery = MyEntityQueryType.Both;
            }
        }

        internal void SyncPosServerProjectile(ProtoProStateSync.ProSyncState state)
        {
            var session = Info.Ai.Session;
            var proSync = session.ProtoWeaponProSyncPosPool.Count > 0 ? session.ProtoWeaponProSyncPosPool.Pop() : new ProtoProPositionSync();
            proSync.PartId = (ushort) Info.Weapon.PartId;
            proSync.Position = Position;
            proSync.State = state;
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
            proSync.OffsetDir = Info.Storage.RandOffsetDir;
            proSync.OffsetTarget = OffsetTarget;
            proSync.ProId = Info.Storage.SyncId;
            proSync.TargetId = target.TargetId;
            proSync.CoreEntityId = Info.Weapon.Comp.CoreEntity.EntityId;
            session.GlobalProStateSyncs[Info.Weapon.Comp.CoreEntity] = proSync;
        }

        internal void SyncClientProjectile(int posSlot)
        {
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

                if (sync.ProPositionSync != null && s.Tick - sync.UpdateTick <= 1 && sync.CurrentOwl < 30)
                {
                    var proPosSync = sync.ProPositionSync;

                    if (proPosSync.State == ProtoProStateSync.ProSyncState.Dead)
                    {
                        State = ProjectileState.Destroy;
                        w.WeaponProSyncs.Remove(Info.Storage.SyncId);
                        return;
                    }

                    var oldPos = Position;
                    var oldVels = Velocity;

                    var checkSlot = posSlot - sync.CurrentOwl >= 0 ? posSlot - (int)sync.CurrentOwl : (posSlot - (int)sync.CurrentOwl) + 30;

                    var estimatedStepSize = sync.CurrentOwl * DeltaStepConst;

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

                        var pastServerLine = lines.Count == 0 ? new LineD(pastServerProPos - (proPosSync.Velocity * DeltaStepConst), pastServerProPos) : new LineD(lines[lines.Count - 1].Line.To, pastServerProPos);

                        lines.Add(new ClientProSyncDebugLine { CreateTick = s.Tick, Line = pastServerLine, Color = Color.Red});

                        //Log.Line($"ProSyn: Id:{Info.Id} - age:{Info.Age} - owl:{sync.CurrentOwl} - jumpDist:{Vector3D.Distance(oldPos, Position)}[{Vector3D.Distance(oldVels, Velocity)}] - posDiff:{Vector3D.Distance(Info.PastProInfos[checkSlot], proPosSync.Position)} - nVel:{oldVels.Length()} - oVel:{proPosSync.Velocity.Length()})");
                    }
                }

                /*
                if (sync.ProStateSync != null)
                {
                    MyEntity targetEnt;
                    if (sync.ProStateSync.TargetId > 0 && (target.TargetId != sync.ProStateSync.TargetId) && MyEntities.TryGetEntityById(sync.ProStateSync.TargetId, out targetEnt))
                    {
                        target.Set(targetEnt, targetEnt.PositionComp.WorldAABB.Center, 0, 0, targetEnt.GetTopMostParent()?.EntityId ?? 0);
                       
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

                        var oldDir = Info.Storage.RandOffsetDir;
                        var oldTarget = OffsetTarget;
                        Info.Storage.RandOffsetDir = sync.ProStateSync.OffsetDir;
                        OffsetTarget = sync.ProStateSync.OffsetTarget;

                        if (w.System.WConst.DebugMode)
                            Log.Line($"seedReset: Id:{Info.Id} - age:{Info.Age} - owl:{sync.CurrentOwl} - stateAge:{Info.Age - Info.Storage.LastProSyncStateAge} - tId:{sync.ProStateSync.TargetId} - oDirDiff:{Vector3D.IsZero(oldDir - Info.Storage.RandOffsetDir, 1E-02d)} - targetDiff:{Vector3D.Distance(oldTarget, OffsetTarget)} - x:{oldX}[{sync.ProStateSync.RandomX}] - y{oldY}[{sync.ProStateSync.RandomY}]");
                    }

                }
                */

                w.WeaponProSyncs.Remove(Info.Storage.SyncId);
            }
        }
        #endregion
    }
}