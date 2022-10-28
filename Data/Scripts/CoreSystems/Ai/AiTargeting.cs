using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Projectiles;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition;
using static CoreSystems.Support.WeaponDefinition.TargetingDef;
using static CoreSystems.Support.WeaponDefinition.TargetingDef.BlockTypes;
using static CoreSystems.Support.WeaponDefinition.AmmoDef;
using static CoreSystems.WeaponRandomGenerator.RandomType;
using static CoreSystems.WeaponRandomGenerator;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using Sandbox.Engine.Physics;

namespace CoreSystems.Support
{
    public partial class Ai
    {
        internal static void AcquireTarget(Weapon w, bool attemptReset, MyEntity targetGrid = null, ProtoWeaponOverrides overrides = null)
        {
            var targetType = TargetType.None;

            if (w.PosChangedTick != w.Comp.Session.Tick) 
                w.UpdatePivotPos();

            FakeTarget.FakeWorldTargetInfo fakeInfo = null;
            overrides = overrides ?? w.Comp.Data.Repo.Values.Set.Overrides;
            var cMode =  overrides.Control;
            if (cMode == ProtoWeaponOverrides.ControlModes.Auto || cMode == ProtoWeaponOverrides.ControlModes.Painter && !w.Comp.PainterMode)
            {
                w.AimCone.ConeDir = w.MyPivotFwd;
                w.AimCone.ConeTip = w.BarrelOrigin + (w.MyPivotFwd * w.MuzzleDistToBarrelCenter);
                var pCount = w.Comp.Ai.LiveProjectile.Count;
                var shootProjectile = pCount > 0 && (w.System.TrackProjectile || w.Comp.OnCustomTurret) && overrides.Projectiles;
                var projectilesFirst = !attemptReset && shootProjectile && w.System.Values.Targeting.Threats.Length > 0 && w.System.Values.Targeting.Threats[0] == Threat.Projectiles;
                var onlyCheckProjectile = w.ProjectilesNear && !w.Target.TargetChanged && w.Comp.Session.Count != w.Acquire.SlotId && !attemptReset;
                if (!projectilesFirst && w.System.TrackTopMostEntities && !onlyCheckProjectile)
                {
                    AcquireTopMostEntity(w, overrides, out targetType, attemptReset, targetGrid);
                }
                else if (!attemptReset && shootProjectile)
                {
                    AcquireProjectile(w, out targetType);
                }

                if (projectilesFirst && targetType == TargetType.None && !onlyCheckProjectile)
                {
                    AcquireTopMostEntity(w, overrides, out targetType, false, targetGrid);
                }
            }
            else if (w.ValidFakeTargetInfo(w.Comp.Data.Repo.Values.State.PlayerId, out fakeInfo))
            {
                Vector3D predictedPos;
                if (Weapon.CanShootTarget(w, ref fakeInfo.WorldPosition, fakeInfo.LinearVelocity, fakeInfo.Acceleration, out predictedPos))
                {
                    w.Target.SetFake(w.Comp.Session.Tick, predictedPos);
                    if (w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance != TrajectoryDef.GuidanceType.None || !w.MuzzleHitSelf())
                        targetType = TargetType.Other;
                }
            }

            if (targetType == TargetType.None)
            {
                if (w.Target.CurrentState == Target.States.Acquired && w.Acquire.IsSleeping && w.Acquire.Monitoring && w.System.Session.AcqManager.MonitorState.Remove(w.Acquire))
                    w.Acquire.Monitoring = false;

                if (w.NewTarget.CurrentState != Target.States.NoTargetsSeen) w.NewTarget.Reset(w.Comp.Session.Tick, Target.States.NoTargetsSeen);
                if (w.Target.CurrentState != Target.States.NoTargetsSeen)
                {
                    w.Target.Reset(w.Comp.Session.Tick, Target.States.NoTargetsSeen, fakeInfo == null);
                }

                w.LastBlockCount = w.Comp.Ai.BlockCount;
            }
            else w.WakeTargets();
        }

        internal static bool SwitchToDrone(Weapon w)
        {
            w.AimCone.ConeDir = w.MyPivotFwd;
            w.AimCone.ConeTip = w.BarrelOrigin + (w.MyPivotFwd * w.MuzzleDistToBarrelCenter);

            var comp = w.Comp;
            var overRides = comp.Data.Repo.Values.Set.Overrides;
            var attackNeutrals = overRides.Neutrals;
            var attackNoOwner = overRides.Unowned;
            var session = w.Comp.Session;
            var ai = comp.Ai;
            session.TargetRequests++;
            var ammoDef = w.ActiveAmmoDef.AmmoDef;
            var aConst = ammoDef.Const;
            var barrelPos = w.BarrelOrigin;
            var weaponPos = w.BarrelOrigin + (w.MyPivotFwd * w.MuzzleDistToBarrelCenter);
            var target = w.NewTarget;
            var s = w.System;
            var accelPrediction = (int)s.Values.HardPoint.AimLeadingPrediction > 1;
            var minRadius = overRides.MinSize * 0.5f;
            var maxRadius = overRides.MaxSize * 0.5f;
            var minTargetRadius = minRadius > 0 ? minRadius : s.MinTargetRadius;
            var maxTargetRadius = maxRadius < s.MaxTargetRadius ? maxRadius : s.MaxTargetRadius;

            var moveMode = overRides.MoveMode;
            var movingMode = moveMode == ProtoWeaponOverrides.MoveModes.Moving;
            var fireOnStation = moveMode == ProtoWeaponOverrides.MoveModes.Any || moveMode == ProtoWeaponOverrides.MoveModes.Moored;
            var stationOnly = moveMode == ProtoWeaponOverrides.MoveModes.Moored;
            BoundingSphereD waterSphere = new BoundingSphereD(Vector3D.Zero, 1f);
            WaterData water = null;
            if (session.WaterApiLoaded && !ammoDef.IgnoreWater && ai.InPlanetGravity && ai.MyPlanet != null && session.WaterMap.TryGetValue(ai.MyPlanet.EntityId, out water))
                waterSphere = new BoundingSphereD(ai.MyPlanet.PositionComp.WorldAABB.Center, water.MinRadius);
            var numOfTargets = ai.SortedTargets.Count;
            var deck = GetDeck(ref target.TargetDeck, ref target.TargetPrevDeckLen,  0, numOfTargets, ai.DetectionInfo.DroneCount, ref w.TargetData.WeaponRandom.AcquireRandom);

            for (int i = 0; i < numOfTargets; i++)
            {
                var info = ai.SortedTargets[deck[i]];

                if (!info.Drone)
                    break;

                if (info?.Target == null || info.Target.MarkedForClose || !attackNeutrals && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral || !attackNoOwner && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership) continue;

                if (movingMode && info.VelLenSqr < 1 || !fireOnStation && info.IsStatic || stationOnly && !info.IsStatic)
                    continue;

                var character = info.Target as IMyCharacter;
                var targetRadius = character != null ? info.TargetRadius * 5 : info.TargetRadius;
                if (targetRadius < minTargetRadius || info.TargetRadius > maxTargetRadius && maxTargetRadius < 8192) continue;

                var targetCenter = info.Target.PositionComp.WorldAABB.Center;
                var targetDistSqr = Vector3D.DistanceSquared(targetCenter, weaponPos);
                if (targetDistSqr > (w.MaxTargetDistance + info.TargetRadius) * (w.MaxTargetDistance + info.TargetRadius) || targetDistSqr < w.MinTargetDistanceSqr) continue;
                if (water != null)
                {
                    if (new BoundingSphereD(ai.MyPlanet.PositionComp.WorldAABB.Center, water.MinRadius).Contains(new BoundingSphereD(targetCenter, targetRadius)) == ContainmentType.Contains)
                        continue;
                }
                session.TargetChecks++;
                Vector3D targetLinVel = info.Target.Physics?.LinearVelocity ?? Vector3D.Zero;
                Vector3D targetAccel = accelPrediction ? info.Target.Physics?.LinearAcceleration ?? Vector3D.Zero : Vector3.Zero;

                if (info.IsGrid)
                {

                    if (!s.TrackGrids || !overRides.Grids || info.FatCount < 2 || (!overRides.LargeGrid && info.LargeGrid) || (!overRides.SmallGrid && !info.LargeGrid)) continue;
                    session.CanShoot++;
                    Vector3D newCenter;
                    if (!w.TurretController)
                    {

                        var validEstimate = true;
                        newCenter = w.System.Prediction != HardPointDef.Prediction.Off && (!aConst.IsBeamWeapon && aConst.DesiredProjectileSpeed > 0) ? Weapon.TrajectoryEstimation(w, targetCenter, targetLinVel, targetAccel,Vector3D.Zero, out validEstimate) : targetCenter;
                        var targetSphere = info.Target.PositionComp.WorldVolume;
                        targetSphere.Center = newCenter;
                        if (!validEstimate || !aConst.SkipAimChecks && !MathFuncs.TargetSphereInCone(ref targetSphere, ref w.AimCone)) continue;
                    }
                    else if (!Weapon.CanShootTargetObb(w, info.Target, targetLinVel, targetAccel, out newCenter)) continue;

                    if (w.Comp.Ai.FriendlyShieldNear)
                    {
                        var targetDir = newCenter - weaponPos;
                        if (w.HitFriendlyShield(weaponPos, newCenter, targetDir))
                            continue;
                    }

                    var targetNormDir = Vector3D.Normalize(targetCenter - barrelPos);
                    var predictedMuzzlePos = barrelPos + (targetNormDir * w.MuzzleDistToBarrelCenter);

                    if (!AcquireBlock(s, w.Comp.Ai, target, info, predictedMuzzlePos, w.TargetData.WeaponRandom, Acquire, ref waterSphere, ref w.XorRnd, w, true)) continue;
                    target.TransferTo(w.Target, w.Comp.Session.Tick, true);

                    var validTarget = w.Target.TargetState == Target.TargetStates.IsEntity;

                    if (validTarget)
                    {

                        ai.Session.NewThreat(w);

                        if (ai.Session.MpActive)
                            w.Target.PushTargetToClient(w);
                    }

                    return validTarget;
                }
            }

            return false;
        }

        internal static bool ReacquireTarget(Projectile p)
        {
            var info = p.Info;
            if (info.CompSceneVersion != info.Weapon.Comp.SceneVersion)
                return false;

            var s = info.Weapon.System;
            var target = info.Target;
            p.ChaseAge = info.Age;
            var ai = info.Ai;
            var aConst = info.AmmoDef.Const;
            var weaponPos = p.Position;
            var overRides = info.Weapon.Comp.Data.Repo.Values.Set.Overrides;
            var attackNeutrals = overRides.Neutrals;
            var attackFriends = overRides.Friendly;
            var attackNoOwner = overRides.Unowned;
            var forceFoci = overRides.FocusTargets;
            var minRadius = overRides.MinSize * 0.5f;
            var maxRadius = overRides.MaxSize * 0.5f;
            var minTargetRadius = minRadius > 0 ? minRadius : s.MinTargetRadius;
            var maxTargetRadius = maxRadius < s.MaxTargetRadius ? maxRadius : s.MaxTargetRadius;
            var moveMode = overRides.MoveMode;
            var movingMode = moveMode == ProtoWeaponOverrides.MoveModes.Moving;
            var fireOnStation = moveMode == ProtoWeaponOverrides.MoveModes.Any || moveMode == ProtoWeaponOverrides.MoveModes.Moored;
            var stationOnly = moveMode == ProtoWeaponOverrides.MoveModes.Moored;
            var acquired = false;
            var lockedToTarget = info.LockOnFireState;
            BoundingSphereD waterSphere = new BoundingSphereD(Vector3D.Zero, 1f);
            WaterData water = null;
            if (s.Session.WaterApiLoaded && !info.AmmoDef.IgnoreWater && ai.InPlanetGravity && ai.MyPlanet != null && s.Session.WaterMap.TryGetValue(ai.MyPlanet.EntityId, out water))
                waterSphere = new BoundingSphereD(ai.MyPlanet.PositionComp.WorldAABB.Center, water.MinRadius);
            TargetInfo alphaInfo = null;
            int offset = 0;
            MyEntity fTarget;
            if (!aConst.OverrideTarget && ai.Construct.Data.Repo.FocusData.Target > 0 && MyEntities.TryGetEntityById(ai.Construct.Data.Repo.FocusData.Target, out fTarget) && ai.Targets.TryGetValue(fTarget, out alphaInfo))
                offset++;


            MyEntity topTarget = null;
            if (lockedToTarget && !aConst.OverrideTarget && target.TargetState == Target.TargetStates.IsEntity) {
                topTarget = target.TargetEntity.GetTopMostParent() ?? alphaInfo?.Target;
                if (topTarget != null && topTarget.MarkedForClose)
                    topTarget = null;
            }

            var numOfTargets = ai.SortedTargets.Count;
            var hasOffset = offset > 0;
            var adjTargetCount = forceFoci && hasOffset ? offset : numOfTargets + offset;
            var deck = GetDeck(ref target.TargetDeck, ref target.TargetPrevDeckLen,  0, numOfTargets, p.Info.Weapon.System.Values.Targeting.TopTargets, ref p.Info.Random);

            for (int i = 0; i < adjTargetCount; i++)
            {
                var focusTarget = hasOffset && i < offset;
                var lastOffset = offset - 1;

                TargetInfo tInfo;
                if (i == 0 && alphaInfo != null) tInfo = alphaInfo;
                else tInfo = ai.SortedTargets[deck[i - offset]];

                if (!focusTarget && tInfo.OffenseRating <= 0 || focusTarget && !attackFriends && tInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Friends || tInfo.Target == null || tInfo.Target.MarkedForClose || hasOffset && i > lastOffset && (tInfo.Target == alphaInfo?.Target)) { continue; }

                if (!attackNeutrals && tInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral || !attackNoOwner && tInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership) continue;

                if (movingMode && tInfo.VelLenSqr < 1 || !fireOnStation && tInfo.IsStatic || stationOnly && !tInfo.IsStatic)
                    continue;

                var character = tInfo.Target as IMyCharacter;
                if (character != null && (!s.TrackCharacters || !overRides.Biologicals)) continue;

                var meteor = tInfo.Target as MyMeteor;
                if (meteor != null && (!s.TrackMeteors || !overRides.Meteors)) continue;

                var targetPos = tInfo.Target.PositionComp.WorldAABB.Center;

                double distSqr;
                Vector3D.DistanceSquared(ref targetPos, ref p.Position, out distSqr);

                if (distSqr > p.DistanceToTravelSqr)
                    continue;

                var targetRadius = tInfo.Target.PositionComp.LocalVolume.Radius;
                if (targetRadius < minTargetRadius || targetRadius > maxTargetRadius && maxTargetRadius < 8192 || topTarget != null && tInfo.Target != topTarget) continue;
                if (water != null)
                {
                    if (new BoundingSphereD(ai.MyPlanet.PositionComp.WorldAABB.Center, water.MinRadius).Contains(new BoundingSphereD(targetPos, targetRadius)) == ContainmentType.Contains)
                        continue;
                }

                if (tInfo.IsGrid)
                {

                    if (!s.TrackGrids || !overRides.Grids || !focusTarget && tInfo.FatCount < 2 || Obstruction(ref tInfo, ref targetPos, p) || (!overRides.LargeGrid && tInfo.LargeGrid) || (!overRides.SmallGrid && !tInfo.LargeGrid)) continue;

                    if (!AcquireBlock(s, ai, target, tInfo, weaponPos, null, ReAcquire, ref waterSphere, ref info.Random, null, !focusTarget, overRides)) continue;
                    acquired = true;
                    break;
                }

                if (Obstruction(ref tInfo, ref targetPos, p))
                    continue;

                var topEntId = tInfo.Target.GetTopMostParent().EntityId;
                target.Set(tInfo.Target, targetPos, 0, 0, topEntId);
                acquired = true;
                break;
            }
            if (!acquired && !lockedToTarget) target.Reset(s.Session.Tick, Target.States.NoTargetsSeen);
            return acquired;
        }

        internal static bool ReAcquireProjectile(Projectile p)
        {
            var info = p.Info;
            var s = info.Weapon.System;
            var target = info.Target;
            p.ChaseAge = info.Age;
            var ai = info.Ai;
            var physics = s.Session.Physics;
            var weaponPos = p.Position;

            var collection = ai.GetProCache();
            var numOfTargets = collection.Count;
            var lockedOnly = s.Values.Targeting.LockedSmartOnly;
            var smartOnly = s.Values.Targeting.IgnoreDumbProjectiles;
            var found = false;
            if (s.ClosestFirst)
            {
                int length = collection.Count;
                for (int h = length / 2; h > 0; h /= 2)
                {
                    for (int i = h; i < length; i += 1)
                    {
                        var tempValue = collection[i];
                        double temp;
                        Vector3D.DistanceSquared(ref collection[i].Position, ref weaponPos, out temp);

                        int j;
                        for (j = i; j >= h && Vector3D.DistanceSquared(collection[j - h].Position, weaponPos) > temp; j -= h)
                            collection[j] = collection[j - h];

                        collection[j] = tempValue;
                    }
                }
            }

            var numToRandomize = s.ClosestFirst ? s.Values.Targeting.TopTargets : numOfTargets;
            if (target.TargetPrevDeckLen < numOfTargets) {
                target.TargetDeck = new int[numOfTargets];
                target.TargetPrevDeckLen = numOfTargets;
            }

            for (int i = 0; i < numOfTargets; i++) {
                var j = i < numToRandomize ? info.Random.Range(0, i + 1) : i;
                target.TargetDeck[i] = target.TargetDeck[j];
                target.TargetDeck[j] = 0 + i;
            }
            var deck = target.TargetDeck;
            for (int x = 0; x < numOfTargets; x++)
            {
                var card = deck[x];
                var lp = collection[card];
                if (smartOnly && !lp.IsSmart || lockedOnly && !lp.IsSmart || lp.MaxSpeed > s.MaxTargetSpeed || lp.MaxSpeed <= 0 || lp.State != Projectile.ProjectileState.Alive) continue;

                var needsCast = false;
                for (int i = 0; i < ai.Obstructions.Count; i++)
                {
                    var ent = ai.Obstructions[i];
                    var obsSphere = ent.PositionComp.WorldVolume;

                    var dir = lp.Position - weaponPos;
                    var ray = new RayD(ref weaponPos, ref dir);

                    if (ray.Intersects(obsSphere) != null)
                    {
                        var transform = ent.PositionComp.WorldMatrixRef;
                        var box = ent.PositionComp.LocalAABB;
                        var obb = new MyOrientedBoundingBoxD(box, transform);
                        if (obb.Intersects(ref ray) != null)
                        {
                            needsCast = true;
                            break;
                        }
                    }
                }

                if (needsCast)
                {
                    IHitInfo hitInfo;
                    physics.CastRay(weaponPos, lp.Position, out hitInfo, 15);
                    if (hitInfo?.HitEntity == null)
                    {
                        target.Set(null, lp.Position, 0, 0, long.MaxValue, lp);
                        p.PrevTargetPos = target.Projectile.Position;
                        target.Projectile.Seekers.Add(p);
                        found = true;
                        break;
                    }
                }
                else
                {
                    Vector3D? hitInfo;
                    if (ai.AiType == AiTypes.Grid && GridIntersection.BresenhamGridIntersection(ai.GridEntity, ref weaponPos, ref lp.Position, out hitInfo, target.CoreCube, ai))
                        continue;

                    target.Set(null, lp.Position, 0, 0, long.MaxValue, lp);
                    p.PrevTargetPos = target.Projectile.Position;
                    target.Projectile.Seekers.Add(p);
                    found = true;
                    break;
                }
            }

            return found;
        }

        private static void AcquireTopMostEntity(Weapon w, ProtoWeaponOverrides overRides, out TargetType targetType, bool attemptReset = false, MyEntity targetGrid = null)
        {
            var comp = w.Comp;
            var attackNeutrals = overRides.Neutrals;
            var attackFriends = overRides.Friendly;
            var attackNoOwner = overRides.Unowned;
            var forceFoci = overRides.FocusTargets;
            var session = w.Comp.Session;
            var ai = comp.Ai;
            var aConst = w.ActiveAmmoDef.AmmoDef.Const;
            session.TargetRequests++;
            var barrelPos = w.BarrelOrigin;
            var weaponPos = w.BarrelOrigin + (w.MyPivotFwd * w.MuzzleDistToBarrelCenter);
            var target = w.NewTarget;
            var s = w.System;
            var accelPrediction = (int)s.Values.HardPoint.AimLeadingPrediction > 1;
            var minRadius = overRides.MinSize * 0.5f;
            var maxRadius = overRides.MaxSize * 0.5f;
            var minTargetRadius = minRadius > 0 ? minRadius : s.MinTargetRadius;
            var maxTargetRadius = maxRadius < s.MaxTargetRadius ? maxRadius : s.MaxTargetRadius;

            var moveMode = overRides.MoveMode;
            var movingMode = moveMode == ProtoWeaponOverrides.MoveModes.Moving;
            var fireOnStation = moveMode == ProtoWeaponOverrides.MoveModes.Any || moveMode == ProtoWeaponOverrides.MoveModes.Moored;
            var stationOnly = moveMode == ProtoWeaponOverrides.MoveModes.Moored;
            BoundingSphereD waterSphere = new BoundingSphereD(Vector3D.Zero, 1f);
            WaterData water = null;
            if (session.WaterApiLoaded && !w.ActiveAmmoDef.AmmoDef.IgnoreWater && ai.InPlanetGravity && ai.MyPlanet != null && session.WaterMap.TryGetValue(ai.MyPlanet.EntityId, out water))
                waterSphere = new BoundingSphereD(ai.MyPlanet.PositionComp.WorldAABB.Center, water.MinRadius);

            w.FoundTopMostTarget = false;

            TargetInfo alphaInfo = null;
            int offset = 0;

            MyEntity fTarget;
            if (ai.Construct.Data.Repo.FocusData.Target > 0 && MyEntities.TryGetEntityById(ai.Construct.Data.Repo.FocusData.Target, out fTarget) && ai.Targets.TryGetValue(fTarget, out alphaInfo))
                offset++;

            TargetInfo gridInfo = null;
            var forceTarget = false;
            if (targetGrid != null)
                if (ai.Targets.TryGetValue(targetGrid, out gridInfo))
                    forceTarget = true;

            var hasOffset = offset > 0;
            var numOfTargets = ai.SortedTargets.Count;
            var adjTargetCount = forceFoci && hasOffset ? offset : numOfTargets + offset;

            var deck = GetDeck(ref target.TargetDeck, ref target.TargetPrevDeckLen,  0, numOfTargets, w.System.Values.Targeting.TopTargets, ref w.TargetData.WeaponRandom.AcquireRandom);
            try
            {
                for (int x = 0; x < adjTargetCount; x++)
                {
                    var focusTarget = hasOffset && x < offset;
                    var lastOffset = offset - 1;
                    if (!focusTarget && (attemptReset || aConst.SkipAimChecks)) break;
                    TargetInfo info = null;
                    if (forceTarget && !focusTarget) info = gridInfo;
                    else
                    {
                        if (focusTarget)
                        {
                            if (x == 0 && alphaInfo != null) info = alphaInfo;
                            if (!attackFriends && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Friends) continue;
                        }
                        else info = ai.SortedTargets[deck[x - offset]];
                    }
                    if (info?.Target == null || info.Target.MarkedForClose || hasOffset && x > lastOffset && (info.Target == alphaInfo?.Target) || !attackNeutrals && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral || !attackNoOwner && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                        continue;
                    if (movingMode && info.VelLenSqr < 1 || !fireOnStation && info.IsStatic || stationOnly && !info.IsStatic)
                        continue;

                    var character = info.Target as IMyCharacter;
                    var targetRadius = character != null ? info.TargetRadius * 5 : info.TargetRadius;
                    if (targetRadius < minTargetRadius || info.TargetRadius > maxTargetRadius && maxTargetRadius < 8192 || !focusTarget && info.OffenseRating <= 0) continue;

                    var targetCenter = info.Target.PositionComp.WorldAABB.Center;
                    var targetDistSqr = Vector3D.DistanceSquared(targetCenter, weaponPos);
                    if (targetDistSqr > (w.MaxTargetDistance + info.TargetRadius) * (w.MaxTargetDistance + info.TargetRadius) || targetDistSqr < w.MinTargetDistanceSqr) continue;

                    if (water != null)
                    {
                        if (new BoundingSphereD(ai.MyPlanet.PositionComp.WorldAABB.Center, water.MinRadius).Contains(new BoundingSphereD(targetCenter, targetRadius)) == ContainmentType.Contains)
                            continue;
                    }
                    session.TargetChecks++;
                    Vector3D targetLinVel = info.Target.Physics?.LinearVelocity ?? Vector3D.Zero;
                    Vector3D targetAccel = accelPrediction ? info.Target.Physics?.LinearAcceleration ?? Vector3D.Zero : Vector3.Zero;
                    
                    if (info.IsGrid)
                    {

                        if (!s.TrackGrids || !overRides.Grids || (!overRides.LargeGrid && info.LargeGrid) || (!overRides.SmallGrid && !info.LargeGrid) || !focusTarget && info.FatCount < 2) continue;
                        session.CanShoot++;
                        Vector3D newCenter;
                        if (!w.TurretController && !w.RotorTurretTracking)
                        {

                            var validEstimate = true;
                            newCenter = w.System.Prediction != HardPointDef.Prediction.Off && (!aConst.IsBeamWeapon && aConst.DesiredProjectileSpeed > 0) ? Weapon.TrajectoryEstimation(w, targetCenter, targetLinVel, targetAccel, Vector3D.Zero, out validEstimate) : targetCenter;
                            var targetSphere = info.Target.PositionComp.WorldVolume;
                            targetSphere.Center = newCenter;

                            if (!validEstimate || (!aConst.SkipAimChecks || w.System.LockOnFocus) && !MathFuncs.TargetSphereInCone(ref targetSphere, ref w.AimCone)) continue;
                        }
                        else if (!Weapon.CanShootTargetObb(w, info.Target, targetLinVel, targetAccel, out newCenter)) continue;

                        if (w.Comp.Ai.FriendlyShieldNear)
                        {
                            var targetDir = newCenter - weaponPos;
                            if (w.HitFriendlyShield(weaponPos, newCenter, targetDir))
                                continue;
                        }

                        var targetNormDir = Vector3D.Normalize(targetCenter - barrelPos);
                        var predictedMuzzlePos = barrelPos + (targetNormDir * w.MuzzleDistToBarrelCenter);
                        w.FoundTopMostTarget = true;

                        if (!AcquireBlock(s, w.Comp.Ai, target, info, predictedMuzzlePos, w.TargetData.WeaponRandom, Acquire, ref waterSphere, ref w.XorRnd, w, !focusTarget, overRides)) continue;
                        targetType = TargetType.Other;
                        target.TransferTo(w.Target, w.Comp.Session.Tick);

                        if (targetType == TargetType.Other && w.Target.TargetState == Target.TargetStates.IsEntity)
                            ai.Session.NewThreat(w);

                        return;
                    }
                    var meteor = info.Target as MyMeteor;
                    if (meteor != null && (!s.TrackMeteors || !overRides.Meteors)) continue;

                    if (character != null && (!s.TrackCharacters || !overRides.Biologicals || character.IsDead || character.Integrity <= 0 || session.AdminMap.ContainsKey(character))) continue;
                    Vector3D predictedPos;
                    if (!Weapon.CanShootTarget(w, ref targetCenter, targetLinVel, targetAccel, out predictedPos, true, info.Target)) continue;

                    if (w.Comp.Ai.FriendlyShieldNear)
                    {
                        var targetDir = predictedPos - weaponPos;
                        if (w.HitFriendlyShield(weaponPos, predictedPos, targetDir))
                            continue;
                    }

                    session.TopRayCasts++;

                    if (w.LastHitInfo?.HitEntity != null && (!w.System.Values.HardPoint.Other.MuzzleCheck || !w.MuzzleHitSelf()))
                    {

                        TargetInfo hitInfo;
                        if (w.LastHitInfo.HitEntity == info.Target || ai.Targets.TryGetValue((MyEntity)w.LastHitInfo.HitEntity, out hitInfo) && (hitInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies || hitInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral || hitInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership))
                        {

                            double rayDist;
                            Vector3D.Distance(ref weaponPos, ref targetCenter, out rayDist);
                            var shortDist = rayDist * (1 - w.LastHitInfo.Fraction);
                            var origDist = rayDist * w.LastHitInfo.Fraction;
                            var topEntId = info.Target.GetTopMostParent().EntityId;
                            target.Set(info.Target, w.LastHitInfo.Position, shortDist, origDist, topEntId);
                            targetType = TargetType.Other;
                            target.TransferTo(w.Target, w.Comp.Session.Tick);
                            
                            w.FoundTopMostTarget = true;

                            if (targetType == TargetType.Other && w.Target.TargetState == Target.TargetStates.IsEntity)
                                ai.Session.NewThreat(w);

                            return;
                        }
                    }

                    if (forceTarget) break;
                }

                if (!attemptReset || !w.Target.HasTarget) targetType = TargetType.None;
                else targetType = w.Target.TargetState == Target.TargetStates.IsProjectile ? TargetType.Projectile : TargetType.Other;

            }
            catch (Exception ex) { Log.Line($"Exception in AcquireTopMostEntity: {ex}"); targetType = TargetType.None; }
        }

        private static bool AcquireBlock(WeaponSystem system, Ai ai, Target target, TargetInfo info, Vector3D weaponPos, WeaponRandomGenerator wRng, RandomType type, ref BoundingSphereD waterSphere, ref XorShiftRandomStruct xRnd, Weapon w = null, bool checkPower = true, ProtoWeaponOverrides overRides = null)
        {
            if (system.TargetSubSystems)
            {
                var subSystems = system.Values.Targeting.SubSystems;
                var targetLinVel = info.Target.Physics?.LinearVelocity ?? Vector3D.Zero;
                var targetAccel = (int)system.Values.HardPoint.AimLeadingPrediction > 1 ? info.Target.Physics?.LinearAcceleration ?? Vector3D.Zero : Vector3.Zero;
                var focusSubSystem = overRides != null && overRides.FocusSubSystem || w != null && w.Comp.Data.Repo.Values.Set.Overrides.FocusSubSystem;
                var subSystem = overRides?.SubSystem ?? w?.Comp.Data.Repo.Values.Set.Overrides.SubSystem ?? Any;

                foreach (var blockType in subSystems)
                {
                    var bt = focusSubSystem ? subSystem : blockType;

                    ConcurrentDictionary<BlockTypes, ConcurrentCachingList<MyCubeBlock>> blockTypeMap;
                    system.Session.GridToBlockTypeMap.TryGetValue((MyCubeGrid)info.Target, out blockTypeMap);
                    if (bt != Any && blockTypeMap != null && blockTypeMap[bt].Count > 0)
                    {
                        var subSystemList = blockTypeMap[bt];
                        if (system.ClosestFirst)
                        {
                            if (target.Top5.Count > 0 && (bt != target.LastBlockType || target.Top5[0].CubeGrid != subSystemList[0].CubeGrid))
                                target.Top5.Clear();

                            target.LastBlockType = bt;
                            if (GetClosestHitableBlockOfType(subSystemList, ai, target, info, weaponPos, targetLinVel, targetAccel, ref waterSphere, w, checkPower))
                                return true;
                        }
                        else if (FindRandomBlock(system, ai, target, weaponPos, info, subSystemList, w, wRng, type, ref waterSphere, ref xRnd,  checkPower)) return true;
                    }

                    if (focusSubSystem) break;
                }

                if (system.OnlySubSystems || focusSubSystem && subSystem != Any) return false;
            }
            GridMap gridMap;
            return system.Session.TopEntityToInfoMap.TryGetValue((MyCubeGrid)info.Target, out gridMap) && gridMap.MyCubeBocks != null && FindRandomBlock(system, ai, target, weaponPos, info, gridMap.MyCubeBocks, w, wRng, type, ref waterSphere, ref xRnd, checkPower);
        }

        private static bool FindRandomBlock(WeaponSystem system, Ai ai, Target target, Vector3D weaponPos, TargetInfo info, ConcurrentCachingList<MyCubeBlock> subSystemList, Weapon w, WeaponRandomGenerator wRng, RandomType type, ref BoundingSphereD waterSphere, ref XorShiftRandomStruct xRnd, bool checkPower = true)
        {
            var totalBlocks = subSystemList.Count;

            var topEnt = info.Target.GetTopMostParent();

            var entSphere = topEnt.PositionComp.WorldVolume;
            var distToEnt = MyUtils.GetSmallestDistanceToSphere(ref weaponPos, ref entSphere);
            var weaponCheck = w?.ActiveAmmoDef != null && !w.ActiveAmmoDef.AmmoDef.Const.SkipAimChecks;
            var topBlocks = system.Values.Targeting.TopBlocks;
            var lastBlocks = topBlocks > 10 && distToEnt < 1000 ? topBlocks : 10;
            var isPriroity = false;
            if (lastBlocks < 250)
            {
                TargetInfo priorityInfo;
                MyEntity fTarget;
                if (ai.Construct.Data.Repo.FocusData.Target > 0 && MyEntities.TryGetEntityById(ai.Construct.Data.Repo.FocusData.Target, out fTarget) && ai.Targets.TryGetValue(fTarget, out priorityInfo) && priorityInfo.Target?.GetTopMostParent() == topEnt)
                {
                    isPriroity = true;
                    lastBlocks = totalBlocks < 250 ? totalBlocks : 250;
                }

            }

            if (totalBlocks < lastBlocks) lastBlocks = totalBlocks;

            var deck = GetDeck(ref target.BlockDeck, ref target.BlockPrevDeckLen,  0, totalBlocks, topBlocks, ref xRnd);

            var physics = system.Session.Physics;
            var iGrid = topEnt as IMyCubeGrid;
            var gridPhysics = iGrid?.Physics;
            Vector3D targetLinVel = gridPhysics?.LinearVelocity ?? Vector3D.Zero;
            Vector3D targetAccel = (int)system.Values.HardPoint.AimLeadingPrediction > 1 ? info.Target.Physics?.LinearAcceleration ?? Vector3D.Zero : Vector3.Zero;
            var foundBlock = false;
            var blocksChecked = 0;
            var blocksSighted = 0;
            var hitTmpList = system.Session.HitInfoTmpList;
            for (int i = 0; i < totalBlocks; i++)
            {
                if (weaponCheck && (blocksChecked > lastBlocks || isPriroity && (blocksSighted > 100 || blocksChecked > 50 && system.Session.RandomRayCasts > 500 || blocksChecked > 25 && system.Session.RandomRayCasts > 1000)))
                    break;

                var card = deck[i];
                var block = subSystemList[card];

                if (block.MarkedForClose || checkPower && !(block is IMyWarhead) && !block.IsWorking) continue;

                system.Session.BlockChecks++;

                var blockPos = block.CubeGrid.GridIntegerToWorld(block.Position);
                double rayDist;
                if (weaponCheck)
                {
                    double distSqr;
                    Vector3D.DistanceSquared(ref blockPos, ref weaponPos, out distSqr);
                    if (distSqr > w.MaxTargetDistanceSqr || distSqr < w.MinTargetDistanceSqr)
                        continue;

                    blocksChecked++;
                    ai.Session.CanShoot++;
                    Vector3D predictedPos;
                    if (!Weapon.CanShootTarget(w, ref blockPos, targetLinVel, targetAccel, out predictedPos, w.RotorTurretTracking)) continue;

                    if (system.Session.WaterApiLoaded && waterSphere.Radius > 2 && waterSphere.Contains(predictedPos) != ContainmentType.Disjoint)
                        continue;

                    blocksSighted++;

                    system.Session.RandomRayCasts++;

                    var targetDir = blockPos - w.BarrelOrigin;
                    Vector3D targetDirNorm;
                    Vector3D.Normalize(ref targetDir, out targetDirNorm);
                    var testPos = w.BarrelOrigin + (targetDirNorm * w.MuzzleDistToBarrelCenter);

                    IHitInfo iHitInfo = null;
                    physics.CastRay(testPos, blockPos, hitTmpList, 15);
                    var skip = false;
                    for (int j = 0; j < hitTmpList.Count; j++)
                    {
                        var hitInfo = hitTmpList[j];

                        var entity = hitInfo.HitEntity as MyEntity;
                        var character = entity as IMyCharacter;
                        
                        TargetInfo otherInfo;
                        var enemyCharacter = character != null && (!ai.Targets.TryGetValue(entity, out otherInfo) || !(otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies || otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral || otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership));

                        if (entity == null || entity is MyVoxelBase || character != null && !enemyCharacter)
                        {
                            skip = true;
                            break;
                        }

                        var topHitEntity = entity.GetTopMostParent();
                        var hitGrid = topHitEntity as MyCubeGrid;

                        if (hitGrid != null)
                        {
                            if (hitGrid.MarkedForClose || (hitGrid != block.CubeGrid && ai.AiType == AiTypes.Grid && hitGrid.IsSameConstructAs(ai.GridEntity)) || !hitGrid.DestructibleBlocks || hitGrid.Immune || hitGrid.GridGeneralDamageModifier <= 0) continue;
                            var isTarget = hitGrid == block.CubeGrid || hitGrid.IsSameConstructAs(block.CubeGrid);

                            var bigOwners = hitGrid.BigOwners;
                            var noOwner = bigOwners.Count == 0;

                            var validTarget = isTarget || noOwner || ai.Targets.TryGetValue(topHitEntity, out otherInfo) && (otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies || otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral || otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership);
                            
                            skip = !validTarget;

                            if (isTarget)
                                iHitInfo = hitInfo;

                            if (isTarget || skip)
                                break;
                        }
                    }

                    if (skip || iHitInfo == null)
                        continue;

                    Vector3D.Distance(ref weaponPos, ref blockPos, out rayDist);
                    var shortDist = rayDist * (1 - iHitInfo.Fraction);
                    var origDist = rayDist * iHitInfo.Fraction;
                    var topEntId = block.GetTopMostParent().EntityId;
                    target.Set(block, iHitInfo.Position, shortDist, origDist, topEntId);
                    foundBlock = true;
                    break;
                }

                Vector3D.Distance(ref weaponPos, ref blockPos, out rayDist);
                target.Set(block, block.PositionComp.WorldAABB.Center, rayDist, rayDist, block.GetTopMostParent().EntityId);
                foundBlock = true;
                break;
            }
            return foundBlock;
        }

        internal static bool GetClosestHitableBlockOfType(ConcurrentCachingList<MyCubeBlock> cubes, Ai ai, Target target, TargetInfo info, Vector3D currentPos, Vector3D targetLinVel, Vector3D targetAccel, ref BoundingSphereD waterSphere, Weapon w = null, bool checkPower = true)
        {
            var minValue = double.MaxValue;
            var minValue0 = double.MaxValue;
            var minValue1 = double.MaxValue;
            var minValue2 = double.MaxValue;
            var minValue3 = double.MaxValue;

            MyCubeBlock newEntity = null;
            MyCubeBlock newEntity0 = null;
            MyCubeBlock newEntity1 = null;
            MyCubeBlock newEntity2 = null;
            MyCubeBlock newEntity3 = null;
            var bestCubePos = Vector3D.Zero;
            var top5Count = target.Top5.Count;
            var testPos = currentPos;
            var top5 = target.Top5;
            var physics = ai.Session.Physics;
            var hitTmpList = ai.Session.HitInfoTmpList;
            var weaponCheck = w?.ActiveAmmoDef != null && !w.ActiveAmmoDef.AmmoDef.Const.SkipAimChecks;
            IHitInfo iHitInfo = null;

            for (int i = 0; i < cubes.Count + top5Count; i++)
            {

                ai.Session.BlockChecks++;
                var index = i < top5Count ? i : i - top5Count;
                var cube = i < top5Count ? top5[index] : cubes[index];

                var grid = cube.CubeGrid;
                if (grid == null || grid.MarkedForClose) continue;
                if (cube.MarkedForClose || cube == newEntity || cube == newEntity0 || cube == newEntity1 || cube == newEntity2 || cube == newEntity3 || checkPower && !(cube is IMyWarhead) && !cube.IsWorking)
                    continue;

                var cubePos = grid.GridIntegerToWorld(cube.Position);
                var range = cubePos - testPos;
                var test = (range.X * range.X) + (range.Y * range.Y) + (range.Z * range.Z);

                if (ai.Session.WaterApiLoaded && waterSphere.Radius > 2 && waterSphere.Contains(cubePos) != ContainmentType.Disjoint)
                    continue;

                if (test < minValue3)
                {

                    IHitInfo hit = null;

                    var best = test < minValue;
                    var bestTest = false;
                    if (best)
                    {

                        if (weaponCheck)
                        {

                            ai.Session.CanShoot++;
                            Vector3D predictedPos;
                            if (Weapon.CanShootTarget(w, ref cubePos, targetLinVel, targetAccel, out predictedPos))
                            {

                                ai.Session.ClosestRayCasts++;

                                var targetDir = cubePos - w.BarrelOrigin;
                                Vector3D targetDirNorm;
                                Vector3D.Normalize(ref targetDir, out targetDirNorm);

                                var rayStart = w.BarrelOrigin + (targetDirNorm * w.MuzzleDistToBarrelCenter);

                                physics.CastRay(rayStart, cubePos, hitTmpList, 15);

                                var skip = false;
                                for (int j = 0; j < hitTmpList.Count; j++)
                                {
                                    var hitInfo = hitTmpList[j];

                                    var entity = hitInfo.HitEntity as MyEntity;
                                    var character = entity as IMyCharacter;

                                    TargetInfo otherInfo;
                                    var enemyCharacter = character != null && (!ai.Targets.TryGetValue(entity, out otherInfo) || !(otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies || otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral || otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership));

                                    if (entity == null || entity is MyVoxelBase || character != null && !enemyCharacter)
                                    {
                                        skip = true;
                                        break;
                                    }


                                    var topHitEntity = entity.GetTopMostParent();
                                    var hitGrid = topHitEntity as MyCubeGrid;

                                    if (hitGrid != null)
                                    {
                                        if (hitGrid.MarkedForClose || (hitGrid != grid && ai.AiType == AiTypes.Grid && hitGrid.IsSameConstructAs(ai.GridEntity)) || !hitGrid.DestructibleBlocks || hitGrid.Immune || hitGrid.GridGeneralDamageModifier <= 0) continue;

                                        var isTarget = hitGrid == grid || hitGrid.IsSameConstructAs(grid);

                                        var bigOwners = hitGrid.BigOwners;
                                        var noOwner = bigOwners.Count == 0;

                                        var validTarget = isTarget || noOwner || ai.Targets.TryGetValue(topHitEntity, out otherInfo) && (otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies || otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral || otherInfo.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership);

                                        skip = !validTarget;

                                        if (isTarget)
                                        {
                                            bestTest = true;
                                            hit = hitInfo;
                                        }

                                        if (isTarget || skip)
                                            break;
                                    }
                                }

                                if (skip || hit == null)
                                    continue;
                            }
                        }
                        else bestTest = true;
                    }

                    if (best && bestTest)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = minValue1;
                        newEntity2 = newEntity1;
                        minValue1 = minValue0;
                        newEntity1 = newEntity0;
                        minValue0 = minValue;
                        newEntity0 = newEntity;
                        minValue = test;

                        newEntity = cube;
                        bestCubePos = cubePos;
                        iHitInfo = hit;
                    }
                    else if (test < minValue0)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = minValue1;
                        newEntity2 = newEntity1;
                        minValue1 = minValue0;
                        newEntity1 = newEntity0;
                        minValue0 = test;

                        newEntity0 = cube;
                    }
                    else if (test < minValue1)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = minValue1;
                        newEntity2 = newEntity1;
                        minValue1 = test;

                        newEntity1 = cube;
                    }
                    else if (test < minValue2)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = test;

                        newEntity2 = cube;
                    }
                    else
                    {
                        minValue3 = test;
                        newEntity3 = cube;
                    }
                }

            }
            top5.Clear();
            if (newEntity != null && iHitInfo != null)
            {

                double rayDist;
                Vector3D.Distance(ref testPos, ref bestCubePos, out rayDist);
                var shortDist = rayDist * (1 - iHitInfo.Fraction);
                var origDist = rayDist * iHitInfo.Fraction;
                var topEntId = newEntity.GetTopMostParent().EntityId;
                target.Set(newEntity, iHitInfo.Position, shortDist, origDist, topEntId);
                top5.Add(newEntity);
            }
            else if (newEntity != null)
            {

                double rayDist;
                Vector3D.Distance(ref testPos, ref bestCubePos, out rayDist);
                var shortDist = rayDist;
                var origDist = rayDist;
                var topEntId = newEntity.GetTopMostParent().EntityId;
                target.Set(newEntity, bestCubePos, shortDist, origDist, topEntId);
                top5.Add(newEntity);
            }
            else target.Reset(ai.Session.Tick, Target.States.NoTargetsSeen, w == null);

            if (newEntity0 != null) top5.Add(newEntity0);
            if (newEntity1 != null) top5.Add(newEntity1);
            if (newEntity2 != null) top5.Add(newEntity2);
            if (newEntity3 != null) top5.Add(newEntity3);

            return top5.Count > 0;
        }

        internal static void AcquireProjectile(Weapon w, out TargetType targetType)
        {
            var ai = w.Comp.Ai;
            var s = w.System;
            var physics = s.Session.Physics;
            var target = w.NewTarget;
            var weaponPos = w.BarrelOrigin;

            var collection = ai.GetProCache();
            var numOfTargets = collection.Count;
            var lockedOnly = w.System.Values.Targeting.LockedSmartOnly;
            var smartOnly = w.System.Values.Targeting.IgnoreDumbProjectiles;
            if (s.ClosestFirst)
            {
                int length = collection.Count;
                for (int h = length / 2; h > 0; h /= 2)
                {
                    for (int i = h; i < length; i += 1)
                    {
                        var tempValue = collection[i];
                        double temp;
                        Vector3D.DistanceSquared(ref collection[i].Position, ref weaponPos, out temp);

                        int j;
                        for (j = i; j >= h && Vector3D.DistanceSquared(collection[j - h].Position, weaponPos) > temp; j -= h)
                            collection[j] = collection[j - h];

                        collection[j] = tempValue;
                    }
                }
            }

            var numToRandomize = s.ClosestFirst ? w.System.Values.Targeting.TopTargets : numOfTargets;
            var deck = GetDeck(ref target.TargetDeck, ref target.TargetPrevDeckLen,  0, numOfTargets, numToRandomize, ref w.TargetData.WeaponRandom.AcquireRandom);

            for (int x = 0; x < numOfTargets; x++)
            {
                var card = deck[x];
                var lp = collection[card];
                var cube = lp.Info.Target.TargetEntity as MyCubeBlock;
                if (smartOnly && !lp.IsSmart || lockedOnly && (!lp.IsSmart || cube != null && w.Comp.Ai.AiType == AiTypes.Grid && cube.CubeGrid.IsSameConstructAs(w.Comp.Ai.GridEntity)) || lp.MaxSpeed > s.MaxTargetSpeed || lp.MaxSpeed <= 0 || lp.State != Projectile.ProjectileState.Alive || Vector3D.DistanceSquared(lp.Position, weaponPos) > w.MaxTargetDistanceSqr || Vector3D.DistanceSquared(lp.Position, weaponPos) < w.MinTargetDistanceBufferSqr) continue;

                Vector3D predictedPos;
                if (Weapon.CanShootTarget(w, ref lp.Position, lp.Velocity, lp.MaxAccelVelocity, out predictedPos))
                {
                    var needsCast = false;
                    for (int i = 0; i < ai.Obstructions.Count; i++)
                    {
                        var ent = ai.Obstructions[i];
                        var obsSphere = ent.PositionComp.WorldVolume;

                        var dir = lp.Position - weaponPos;
                        var beam = new RayD(ref weaponPos, ref dir);

                        if (beam.Intersects(obsSphere) != null)
                        {
                            var transform = ent.PositionComp.WorldMatrixRef;
                            var box = ent.PositionComp.LocalAABB;
                            var obb = new MyOrientedBoundingBoxD(box, transform);
                            if (obb.Intersects(ref beam) != null)
                            {
                                needsCast = true;
                                break;
                            }
                        }
                    }

                    if (needsCast)
                    {
                        IHitInfo hitInfo;
                        physics.CastRay(weaponPos, lp.Position, out hitInfo, 15);
                        if (hitInfo?.HitEntity == null && (!w.System.Values.HardPoint.Other.MuzzleCheck || !w.MuzzleHitSelf()))
                        {
                            double hitDist;
                            Vector3D.Distance(ref weaponPos, ref lp.Position, out hitDist);
                            var shortDist = hitDist;
                            var origDist = hitDist;
                            const long topEntId = long.MaxValue;
                            target.Set(null, lp.Position, shortDist, origDist, topEntId, lp);
                            targetType = TargetType.Projectile;
                            target.TransferTo(w.Target, w.Comp.Session.Tick);
                            return;
                        }
                    }
                    else
                    {
                        Vector3D? hitInfo;
                        if (ai.AiType == AiTypes.Grid && GridIntersection.BresenhamGridIntersection(ai.GridEntity, ref weaponPos, ref lp.Position, out hitInfo, w.Comp.Cube, w.Comp.Ai))
                            continue;

                        double hitDist;
                        Vector3D.Distance(ref weaponPos, ref lp.Position, out hitDist);
                        var shortDist = hitDist;
                        var origDist = hitDist;
                        const long topEntId = long.MaxValue;
                        target.Set(null, lp.Position, shortDist, origDist, topEntId, lp);
                        targetType = TargetType.Projectile;
                        target.TransferTo(w.Target, w.Comp.Session.Tick);
                        return;
                    }
                }
            }
            targetType = TargetType.None;
        }

        private static bool Obstruction(ref TargetInfo info, ref Vector3D targetPos, Projectile p)
        {
            var ai = p.Info.Ai;
            var obstruction = false;
            for (int j = 0; j < ai.Obstructions.Count; j++)
            {
                var ent = ai.Obstructions[j];

                var voxel = ent as MyVoxelBase;
                var dir = (targetPos - p.Position);
                var entWorldVolume = ent.PositionComp.WorldVolume;
                if (voxel != null)
                {

                    if (!ai.PlanetSurfaceInRange && (entWorldVolume.Contains(p.Position) != ContainmentType.Disjoint || new RayD(ref p.Position, ref dir).Intersects(entWorldVolume) != null))
                    {
                        var dirNorm = Vector3D.Normalize(dir);
                        var targetDist = Vector3D.Distance(p.Position, targetPos);
                        var tRadius = info.Target.PositionComp.LocalVolume.Radius;
                        var testPos = p.Position + (dirNorm * (targetDist - tRadius));
                        var lineTest = new LineD(p.Position, testPos);
                        Vector3D? voxelHit = null;
                        using (voxel.Pin())
                            voxel.RootVoxel.GetIntersectionWithLine(ref lineTest, out voxelHit);

                        obstruction = voxelHit.HasValue;
                        if (obstruction)
                            break;
                    }
                }
                else
                {
                    if (new RayD(ref p.Position, ref dir).Intersects(entWorldVolume) != null)
                    {
                        var transform = ent.PositionComp.WorldMatrixRef;
                        var box = ent.PositionComp.LocalAABB;
                        var obb = new MyOrientedBoundingBoxD(box, transform);
                        var lineTest = new LineD(p.Position, targetPos);
                        if (obb.Intersects(ref lineTest) != null)
                        {
                            obstruction = true;
                            break;
                        }
                    }
                }
            }

            if (!obstruction)
            {
                var dir = (targetPos - p.Position);
                var ray = new RayD(ref p.Position, ref dir);
                foreach (var sub in ai.SubGridCache)
                {
                    var subDist = sub.PositionComp.WorldVolume.Intersects(ray);
                    if (subDist.HasValue)
                    {
                        var transform = ai.TopEntity.PositionComp.WorldMatrixRef;
                        var box = ai.TopEntity.PositionComp.LocalAABB;
                        var obb = new MyOrientedBoundingBoxD(box, transform);
                        if (obb.Intersects(ref ray) != null)
                            obstruction = sub.RayCastBlocks(p.Position, targetPos) != null;
                    }

                    if (obstruction) break;
                }

                if (!obstruction && ai.PlanetSurfaceInRange && ai.MyPlanet != null)
                {
                    double targetDist;
                    Vector3D.Distance(ref p.Position, ref targetPos, out targetDist);
                    var dirNorm = dir / targetDist;

                    var tRadius = info.Target.PositionComp.LocalVolume.Radius;
                    targetDist = targetDist > tRadius ? (targetDist - tRadius) : targetDist;

                    var targetEdgePos = targetPos + (-dirNorm * tRadius);

                    if (targetDist > 300)
                    {
                        var lineTest1 = new LineD(p.Position, p.Position + (dirNorm * 150), 150);
                        var lineTest2 = new LineD(targetEdgePos, targetEdgePos + (-dirNorm * 150), 150);
                        obstruction = VoxelIntersect.CheckSurfacePointsOnLine(ai.MyPlanet, ref lineTest1, 3);
                        if (!obstruction)
                            obstruction = VoxelIntersect.CheckSurfacePointsOnLine(ai.MyPlanet, ref lineTest2, 3);
                    }
                    else
                    {
                        var lineTest = new LineD(p.Position, targetEdgePos, targetDist);
                        obstruction = VoxelIntersect.CheckSurfacePointsOnLine(ai.MyPlanet, ref lineTest, 3);
                    }
                }
            }
            return obstruction;
        }
    }
}
