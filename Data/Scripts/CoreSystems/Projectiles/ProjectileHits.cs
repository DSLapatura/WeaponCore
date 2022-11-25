using System;
using System.Collections.Generic;
using CoreSystems.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.HitEntity.Type;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.EwarDef.EwarType;
using static CoreSystems.Support.DeferedVoxels;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using Jakaria.API;
using static CoreSystems.Platform.Weapon;
using static VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GameDefinition;
using static CoreSystems.Projectiles.Projectile;

namespace CoreSystems.Projectiles
{
    public partial class Projectiles
    {
        internal void InitialHitCheck()
        {
            var vhCount = ValidateHits.Count;
            var minCount = Session.Settings.Enforcement.BaseOptimizations ? 96 : 99999;
            var targetStride = vhCount / 20;
            var stride = vhCount < minCount ? 100000 : targetStride > 48 ? targetStride : 48;

            MyAPIGateway.Parallel.For(0, ValidateHits.Count, x => {

                var p = ValidateHits[x];
                var info = p.Info;
                var target = info.Target;
                var ai = info.Ai;
                var w = info.Weapon;
                var coreEntity = w.Comp.CoreEntity;
                var topEntity = w.Comp.TopEntity;
                var aDef = info.AmmoDef;
                var aConst = info.AmmoDef.Const;
                var shieldByPass = aConst.ShieldDamageBypassMod > 0;
                var genericFields = info.EwarActive && (aConst.EwarType == Dot || aConst.EwarType == Push || aConst.EwarType == Pull || aConst.EwarType == Tractor);

                p.FinalizeIntersection = false;
                p.Info.ShieldInLine = false;

                var isBeam = aConst.IsBeamWeapon;
                var lineCheck = aConst.CollisionIsLine && !info.EwarAreaPulse;
                var offensiveEwar = (info.EwarActive && aConst.NonAntiSmartEwar);
                bool projetileInShield = false;
                var tick = Session.Tick;
                var entityCollection = p.MyEntityList;
                var collectionCount = aConst.CollisionIsLine ? p.MySegmentList.Count : entityCollection.Count;

                var beamLen = p.Beam.Length;
                var beamFrom = p.Beam.From;
                var beamTo = p.Beam.To;
                var direction = p.Beam.Direction;
                var beamLenSqr = beamLen * beamLen;

                var ray = new RayD(ref beamFrom, ref direction);
                var firingCube = coreEntity as MyCubeBlock;
                var goCritical = aConst.IsCriticalReaction;
                var selfDamage = aConst.SelfDamage;
                var ignoreVoxels = aDef.IgnoreVoxels;
                var isGrid = ai.AiType == Ai.AiTypes.Grid;

                var closestFutureDistSqr = double.MaxValue;

                WaterData water = null;
                if (Session.WaterApiLoaded && info.MyPlanet != null)
                    Session.WaterMap.TryGetValue(info.MyPlanet.EntityId, out water);

                MyEntity closestFutureEnt = null;
                IMyTerminalBlock iShield = null;
                for (int i = 0; i < collectionCount; i++) {
                    var ent = aConst.CollisionIsLine ? p.MySegmentList[i].Element : entityCollection[i];

                    var grid = ent as MyCubeGrid;
                    var entIsSelf = grid != null && firingCube != null && (grid == firingCube.CubeGrid || firingCube.CubeGrid.IsSameConstructAs(grid));

                    if (entIsSelf && aConst.IsSmart && !info.Storage.SmartReady || ent.MarkedForClose || !ent.InScene || ent == ai.MyShield || !isGrid && ent == topEntity) continue;

                    var character = ent as IMyCharacter;
                    if (info.EwarActive && character != null && !genericFields) continue;

                    var entSphere = ent.PositionComp.WorldVolume;
                    if (aConst.CheckFutureIntersection)
                    {
                        var distSqrToSphere = Vector3D.DistanceSquared(beamFrom, entSphere.Center);
                        if (distSqrToSphere > beamLenSqr)
                        {
                            if (distSqrToSphere < closestFutureDistSqr)
                            {
                                closestFutureDistSqr = distSqrToSphere;
                                closestFutureEnt = ent;                        
                            }
                        }
                    }

                    if (grid != null || character != null)
                    {
                        var extBeam = new LineD(beamFrom - direction * (entSphere.Radius * 2), beamTo);
                        var transform = ent.PositionComp.WorldMatrixRef;
                        var box = ent.PositionComp.LocalAABB;
                        var obb = new MyOrientedBoundingBoxD(box, transform);
                        if (lineCheck && obb.Intersects(ref extBeam) == null || !lineCheck && !obb.Intersects(ref p.PruneSphere)) continue;
                    }

                    var safeZone = ent as MySafeZone;
                    if (safeZone != null && safeZone.Enabled)
                    {

                        var action = (Session.SafeZoneAction)safeZone.AllowedActions;
                        if ((action & Session.SafeZoneAction.Damage) == 0 || (action & Session.SafeZoneAction.Shooting) == 0)
                        {

                            bool intersects;
                            if (safeZone.Shape == MySafeZoneShape.Sphere)
                            {
                                var sphere = new BoundingSphereD(safeZone.PositionComp.WorldVolume.Center, safeZone.Radius);
                                var dist = ray.Intersects(sphere);
                                intersects = dist != null && dist <= beamLen;
                            }
                            else
                                intersects = new MyOrientedBoundingBoxD(safeZone.PositionComp.LocalAABB, safeZone.PositionComp.WorldMatrixRef).Intersects(ref p.Beam) != null;

                            if (intersects)
                            {

                                p.State = ProjectileState.Depleted;
                                p.EndState = EndStates.EarlyEnd;

                                if (p.EnableAv)
                                    info.AvShot.ForceHitParticle = true;
                                break;
                            }
                        }
                    }

                    HitEntity hitEntity = null;
                    var checkShield = Session.ShieldApiLoaded && Session.ShieldHash == ent.DefinitionId?.SubtypeId && ent.Render.Visible;
                    MyTuple<IMyTerminalBlock, MyTuple<bool, bool, float, float, float, int>, MyTuple<MatrixD, MatrixD>>? shieldInfo = null;

                    if (checkShield && !info.EwarActive || info.EwarActive && (aConst.EwarType == Dot || aConst.EwarType == Emp))
                    {

                        shieldInfo = Session.SApi.MatchEntToShieldFastExt(ent, true);
                        if (shieldInfo != null && (firingCube == null || !firingCube.CubeGrid.IsSameConstructAs(shieldInfo.Value.Item1.CubeGrid) && !goCritical))
                        {
                            if (shieldInfo.Value.Item2.Item1)
                            {
                                var shrapnelSpawn = p.Info.IsFragment && p.Info.Age < 1;
                                if (Vector3D.Transform(!shrapnelSpawn ? info.Origin : coreEntity.PositionComp.WorldMatrixRef.Translation, shieldInfo.Value.Item3.Item1).LengthSquared() > 1)
                                {

                                    var dist = MathFuncs.IntersectEllipsoid(shieldInfo.Value.Item3.Item1, shieldInfo.Value.Item3.Item2, new RayD(beamFrom, direction));
                                    if (target.TargetState == Target.TargetStates.IsProjectile && Vector3D.Transform(target.Projectile.Position, shieldInfo.Value.Item3.Item1).LengthSquared() <= 1)
                                        projetileInShield = true;

                                    var shieldIntersect = dist != null && (dist.Value < beamLen || info.EwarActive);
                                    info.ShieldKeepBypass = shieldIntersect;
                                    if (shieldIntersect && !info.ShieldBypassed)
                                    {

                                        hitEntity = HitEntityPool.Get();
                                        hitEntity.EventType = Shield;
                                        var hitPos = beamFrom + (direction * dist.Value);
                                        hitEntity.HitPos = beamFrom + (direction * dist.Value);
                                        hitEntity.HitDist = dist;
                                        if (shieldInfo.Value.Item2.Item2)
                                        {

                                            var faceInfo = Session.SApi.GetFaceInfo(shieldInfo.Value.Item1, hitPos);
                                            var modifiedBypassMod = ((1 - aConst.ShieldDamageBypassMod) + faceInfo.Item5);
                                            var validRange = modifiedBypassMod >= 0 && modifiedBypassMod <= 1 || faceInfo.Item1;
                                            var notSupressed = validRange && modifiedBypassMod < 1 && faceInfo.Item5 < 1;
                                            var bypassAmmo = shieldByPass && notSupressed;
                                            var bypass = bypassAmmo || faceInfo.Item1;

                                            info.ShieldResistMod = faceInfo.Item4;

                                            if (bypass)
                                            {
                                                info.ShieldBypassed = true;
                                                modifiedBypassMod = bypassAmmo && faceInfo.Item1 ? 0f : modifiedBypassMod;
                                                info.ShieldBypassMod = bypassAmmo ? modifiedBypassMod : 0.15f;
                                            }
                                            else p.Info.ShieldBypassMod = 1f;
                                        }
                                        else if (shieldByPass)
                                        {
                                            info.ShieldBypassed = true;
                                            info.ShieldResistMod = 1f;
                                            info.ShieldBypassMod = aConst.ShieldDamageBypassMod;
                                        }
                                    }
                                    else continue;
                                }
                            }
                            else
                                iShield = shieldInfo.Value.Item1;
                        }
                    }

                    var voxel = ent as MyVoxelBase;
                    var destroyable = ent as IMyDestroyableObject;

                    if (voxel != null && voxel == voxel?.RootVoxel && !ignoreVoxels)
                    {
                        VoxelIntersectBranch voxelState = VoxelIntersectBranch.None;

                        if ((ent == info.MyPlanet && !(p.LinePlanetCheck || aConst.DynamicGuidance)) || !p.LinePlanetCheck && isBeam)
                            continue;

                        Vector3D? voxelHit = null;
                        if (tick - info.VoxelCache.HitRefreshed < 60)
                        {
                            var hitSphere = info.VoxelCache.HitSphere;
                            var cacheDist = ray.Intersects(hitSphere);
                            if (cacheDist <= beamLen)
                            {
                                var sphereRadius = hitSphere.Radius;
                                var sphereRadiusSqr = sphereRadius * sphereRadius;

                                var overPenDist = beamLen - cacheDist.Value;
                                var proposedDist = overPenDist >= sphereRadius ? sphereRadius : overPenDist;
                                var testPos1 = beamFrom + (direction * (cacheDist.Value + proposedDist));
                                var testPos2 = beamFrom + (direction * (cacheDist.Value + (proposedDist * 0.5d)));

                                var testPos2DistSqr = Vector3D.DistanceSquared(hitSphere.Center, testPos2);
                                var testPos1DistSqr = Vector3D.DistanceSquared(hitSphere.Center, testPos1);
                                var hitPos = testPos2DistSqr < sphereRadiusSqr && testPos2DistSqr < testPos1DistSqr ? testPos2 : testPos1DistSqr < sphereRadiusSqr ? testPos1 : p.Beam.From + (p.Beam.Direction * cacheDist.Value);
                                
                                voxelHit = hitPos;
                                voxelState = VoxelIntersectBranch.PseudoHit1;
                            }
                            else if (cacheDist.HasValue)
                                info.VoxelCache.MissSphere.Center = beamTo;
                        }

                        if (voxelState != VoxelIntersectBranch.PseudoHit1)
                        {

                            if (voxel == info.MyPlanet && info.VoxelCache.MissSphere.Contains(beamTo) == ContainmentType.Disjoint)
                            {

                                if (p.LinePlanetCheck)
                                {
                                    if (water != null && info.FirstWaterHitTick == 0)
                                    {
                                        var waterOuterSphere = new BoundingSphereD(info.MyPlanet.PositionComp.WorldAABB.Center, water.MaxRadius);
                                        if (ray.Intersects(waterOuterSphere).HasValue || waterOuterSphere.Contains(beamFrom) == ContainmentType.Contains || waterOuterSphere.Contains(beamTo) == ContainmentType.Contains)
                                        {
                                            if (WaterModAPI.LineIntersectsWater(p.Beam, water.Planet) != 0)
                                            {
                                                voxelHit = WaterModAPI.GetClosestSurfacePoint(beamTo, water.Planet);
                                                voxelState = VoxelIntersectBranch.PseudoHit2;
                                                info.FirstWaterHitTick = tick;
                                            }
                                        }
                                    }

                                    if (voxelState != VoxelIntersectBranch.PseudoHit2)
                                    {
                                        var surfacePos = info.MyPlanet.GetClosestSurfacePointGlobal(ref p.Position);
                                        var planetCenter = info.MyPlanet.PositionComp.WorldAABB.Center;
                                        var prevDistanceToSurface = p.DistanceToSurfaceSqr;
                                        Vector3D.DistanceSquared(ref surfacePos, ref p.Position, out p.DistanceToSurfaceSqr);

                                        double surfaceToCenter;
                                        Vector3D.DistanceSquared(ref surfacePos, ref planetCenter, out surfaceToCenter);
                                        double posToCenter;
                                        Vector3D.DistanceSquared(ref p.Position, ref planetCenter, out posToCenter);
                                        double startPosToCenter;
                                        Vector3D.DistanceSquared(ref info.Origin, ref planetCenter, out startPosToCenter);

                                        var distToSurfaceLessThanProLengthSqr = p.DistanceToSurfaceSqr <= beamLenSqr;
                                        var pastSurfaceDistMoreThanToTravel = prevDistanceToSurface > p.DistanceToTravelSqr;

                                        var surfacePosAboveEndpoint = surfaceToCenter > posToCenter;
                                        var posMovingCloserToCenter = posToCenter > startPosToCenter;

                                        var isThisRight = posMovingCloserToCenter && pastSurfaceDistMoreThanToTravel;

                                        if (surfacePosAboveEndpoint || distToSurfaceLessThanProLengthSqr || isThisRight || surfaceToCenter > Vector3D.DistanceSquared(planetCenter, p.LastPosition))
                                        {
                                            var estiamtedSurfaceDistance = ray.Intersects(info.VoxelCache.PlanetSphere);
                                            var fullCheck = info.VoxelCache.PlanetSphere.Contains(p.Info.Origin) != ContainmentType.Disjoint || !estiamtedSurfaceDistance.HasValue;

                                            if (!fullCheck && estiamtedSurfaceDistance.HasValue && (estiamtedSurfaceDistance.Value <= beamLen || info.VoxelCache.PlanetSphere.Radius < 1))
                                            {

                                                double distSqr;
                                                var estimatedHit = ray.Position + (ray.Direction * estiamtedSurfaceDistance.Value);
                                                Vector3D.DistanceSquared(ref info.VoxelCache.FirstPlanetHit, ref estimatedHit, out distSqr);

                                                if (distSqr > 625)
                                                    fullCheck = true;
                                                else
                                                {
                                                    voxelHit = estimatedHit;
                                                    voxelState = VoxelIntersectBranch.PseudoHit2;
                                                }
                                            }

                                            if (fullCheck)
                                                voxelState = VoxelIntersectBranch.DeferFullCheck;

                                            if (voxelHit.HasValue && Vector3D.DistanceSquared(voxelHit.Value, info.VoxelCache.PlanetSphere.Center) > info.VoxelCache.PlanetSphere.Radius * info.VoxelCache.PlanetSphere.Radius)
                                                info.VoxelCache.GrowPlanetCache(voxelHit.Value);
                                        }

                                    }

                                }
                            }
                            else if (voxelHit == null && info.VoxelCache.MissSphere.Contains(p.Beam.To) == ContainmentType.Disjoint)
                            {
                                voxelState = VoxelIntersectBranch.DeferedMissUpdate;
                            }
                        }

                        if (voxelState == VoxelIntersectBranch.PseudoHit1 || voxelState == VoxelIntersectBranch.PseudoHit2)
                        {
                            if (!voxelHit.HasValue)
                            {

                                if (info.VoxelCache.MissSphere.Contains(beamTo) == ContainmentType.Disjoint)
                                    info.VoxelCache.MissSphere.Center = beamTo;
                                continue;
                            }

                            hitEntity = HitEntityPool.Get();

                            var hitPos = voxelHit.Value;
                            hitEntity.HitPos = hitPos;

                            double dist;
                            Vector3D.Distance(ref beamFrom, ref hitPos, out dist);
                            hitEntity.HitDist = dist;
                            hitEntity.EventType = info.FirstWaterHitTick != tick ? Voxel : Water;
                        }
                        else if (voxelState == VoxelIntersectBranch.DeferedMissUpdate || voxelState == VoxelIntersectBranch.DeferFullCheck) {
                            lock (DeferedVoxels)
                            {
                                DeferedVoxels.Add(new DeferedVoxels { Projectile = p, Branch = voxelState, Voxel = voxel });
                            }
                        }
                    }
                    else if (ent.Physics != null && !ent.Physics.IsPhantom && !ent.IsPreview && grid != null)
                    {

                        if (grid != null)
                        {

                            hitEntity = HitEntityPool.Get();
                            if (entIsSelf && !selfDamage && !info.Storage.SmartReady)
                            {
                                if (!isBeam && beamLen <= grid.GridSize * 2 && !goCritical)
                                {
                                    MyCube cube;
                                    if (!(grid.TryGetCube(grid.WorldToGridInteger(p.Position), out cube) && isGrid && cube.CubeBlock != firingCube.SlimBlock || grid.TryGetCube(grid.WorldToGridInteger(p.LastPosition), out cube) && isGrid && cube.CubeBlock != firingCube.SlimBlock))
                                    {
                                        HitEntityPool.Return(hitEntity);
                                        continue;
                                    }
                                }

                                if (!p.Info.EwarAreaPulse)
                                {

                                    var forwardPos = p.Info.Age != 1 ? beamFrom : beamFrom + (direction * Math.Min(grid.GridSizeHalf, info.DistanceTraveled - info.PrevDistanceTraveled));
                                    grid.RayCastCells(forwardPos, p.Beam.To, hitEntity.Vector3ICache, null, true, true);

                                    if (hitEntity.Vector3ICache.Count > 0)
                                    {

                                        bool hitself = false;
                                        for (int j = 0; j < hitEntity.Vector3ICache.Count; j++)
                                        {

                                            MyCube myCube;
                                            if (grid.TryGetCube(hitEntity.Vector3ICache[j], out myCube))
                                            {

                                                if (goCritical || isGrid && ((IMySlimBlock)myCube.CubeBlock).Position != firingCube.Position)
                                                {

                                                    hitself = true;
                                                    break;
                                                }
                                            }
                                        }

                                        if (!hitself)
                                        {
                                            HitEntityPool.Return(hitEntity);
                                            continue;
                                        }
                                        IHitInfo hitInfo = null;
                                        if (!goCritical)
                                        {
                                            Session.Physics.CastRay(forwardPos, beamTo, out hitInfo, CollisionLayers.DefaultCollisionLayer);
                                            var hitGrid = hitInfo?.HitEntity?.GetTopMostParent() as MyCubeGrid;
                                            if (hitGrid == null || firingCube == null || !firingCube.CubeGrid.IsSameConstructAs(hitGrid))
                                            {
                                                HitEntityPool.Return(hitEntity);
                                                continue;
                                            }
                                        }

                                        hitEntity.HitPos = hitInfo?.Position ?? beamFrom;
                                        var posI = hitEntity.Vector3ICache[0];
                                        hitEntity.Blocks.Add(new HitEntity.RootBlocks { Block = grid.GetCubeBlock(hitEntity.Vector3ICache[0]), QueryPos = posI});
                                    }
                                }
                            }
                            else
                                grid.RayCastCells(beamFrom, beamTo, hitEntity.Vector3ICache, null, true, true);

                            if (!offensiveEwar)
                            {

                                if (iShield != null && grid != null && grid.IsSameConstructAs(iShield.CubeGrid))
                                    hitEntity.DamageMulti = 16;

                                hitEntity.EventType = Grid;
                            }
                            else if (!info.EwarAreaPulse)
                                hitEntity.EventType = Effect;
                            else
                                hitEntity.EventType = Field;
                        }
                    }
                    else if (destroyable != null)
                    {

                        hitEntity = HitEntityPool.Get();
                        hitEntity.EventType = Destroyable;
                    }

                    if (hitEntity != null)
                    {
                        var hitEnt = hitEntity.EventType != Shield ? ent : (MyEntity) shieldInfo.Value.Item1;
                        if (hitEnt != null)
                        {
                            p.FinalizeIntersection = true;
                            hitEntity.Info = info;
                            hitEntity.Entity = hitEnt;
                            hitEntity.ShieldEntity = ent;
                            hitEntity.Intersection = p.Beam;
                            hitEntity.SphereCheck = !lineCheck;
                            hitEntity.PruneSphere = p.PruneSphere;
                            hitEntity.SelfHit = entIsSelf;
                            hitEntity.DamageOverTime = aConst.EwarType == Dot;
                            info.HitList.Add(hitEntity);
                        }
                        else
                        {
                            Log.Line($"hitEntity was null: {hitEntity.EventType}");
                            HitEntityPool.Return(hitEntity);
                        }
                    }
                }

                if (aConst.IsDrone)
                    info.Storage.ClosestObstacle = closestFutureEnt;

                if (target.TargetState == Target.TargetStates.IsProjectile && aConst.NonAntiSmartEwar && !projetileInShield)
                {
                    var detonate = p.State == Projectile.ProjectileState.Detonate;
                    var hitTolerance = detonate ? aConst.EndOfLifeRadius : aConst.ByBlockHitRadius > aConst.CollisionSize ? aConst.ByBlockHitRadius : aConst.CollisionSize;
                    var useLine = aConst.CollisionIsLine && !detonate && aConst.ByBlockHitRadius <= 0;

                    var sphere = new BoundingSphereD(target.Projectile.Position, aConst.CollisionSize);
                    sphere.Include(new BoundingSphereD(target.Projectile.LastPosition, 1));

                    bool rayCheck = false;
                    if (useLine)
                    {
                        var dist = sphere.Intersects(new RayD(p.LastPosition, info.Direction));
                        if (dist <= hitTolerance || isBeam && dist <= beamLen)
                            rayCheck = true;
                    }

                    var testSphere = p.PruneSphere;
                    testSphere.Radius = hitTolerance;

                    if (rayCheck || sphere.Intersects(testSphere))
                    {
                        ProjectileHit(p, target.Projectile, lineCheck, ref p.Beam);
                    }
                }

                if (aConst.CollisionIsLine)
                    p.MySegmentList.Clear();
                else 
                    entityCollection.Clear();

                if (p.FinalizeIntersection) {
                    lock (FinalHitCheck)
                        FinalHitCheck.Add(p);
                }

            }, stride);
            ValidateHits.Clear();
        }

        internal void DeferedVoxelCheck()
        {
            for (int i = 0; i < DeferedVoxels.Count; i++)
            {

                var p = DeferedVoxels[i].Projectile;
                var branch = DeferedVoxels[i].Branch;
                var voxel = DeferedVoxels[i].Voxel;
                Vector3D? voxelHit = null;

                if (branch == VoxelIntersectBranch.DeferFullCheck)
                {

                    if (p.Beam.Length > 85)
                    {
                        IHitInfo hit;
                        if (p.Info.Ai.Session.Physics.CastRay(p.Beam.From, p.Beam.To, out hit, CollisionLayers.VoxelCollisionLayer, false) && hit != null)
                            voxelHit = hit.Position;
                    }
                    else
                    {

                        using (voxel.Pin())
                        {
                            if (!voxel.GetIntersectionWithLine(ref p.Beam, out voxelHit, true, IntersectionFlags.DIRECT_TRIANGLES) && VoxelIntersect.PointInsideVoxel(voxel, p.Info.Ai.Session.TmpStorage, p.Beam.From))
                                voxelHit = p.Beam.From;
                        }
                    }

                    if (voxelHit.HasValue && p.Info.IsFragment && p.Info.Age == 0)
                    {
                        if (!VoxelIntersect.PointInsideVoxel(voxel, p.Info.Ai.Session.TmpStorage, voxelHit.Value + (p.Beam.Direction * 1.25f)))
                            voxelHit = null;
                    }
                }
                else if (branch == VoxelIntersectBranch.DeferedMissUpdate)
                {

                    using (voxel.Pin())
                    {

                        if (p.Info.AmmoDef.Const.IsBeamWeapon && p.Info.AmmoDef.Const.RealShotsPerMin < 10)
                        {
                            IHitInfo hit;
                            if (p.Info.Ai.Session.Physics.CastRay(p.Beam.From, p.Beam.To, out hit, CollisionLayers.VoxelCollisionLayer, false) && hit != null)
                                voxelHit = hit.Position;
                        }
                        else if (!voxel.GetIntersectionWithLine(ref p.Beam, out voxelHit, true, IntersectionFlags.DIRECT_TRIANGLES) && VoxelIntersect.PointInsideVoxel(voxel, p.Info.Ai.Session.TmpStorage, p.Beam.From))
                            voxelHit = p.Beam.From;
                    }
                }

                if (!voxelHit.HasValue)
                {

                    if (p.Info.VoxelCache.MissSphere.Contains(p.Beam.To) == ContainmentType.Disjoint)
                        p.Info.VoxelCache.MissSphere.Center = p.Beam.To;
                    continue;
                }

                p.Info.VoxelCache.Update(voxel, ref voxelHit, Session.Tick);

                if (voxelHit == null)
                    continue;
                if (!p.FinalizeIntersection)
                {
                    p.FinalizeIntersection = true;
                    FinalHitCheck.Add(p);
                }
                var hitEntity = HitEntityPool.Get();
                var lineCheck = p.Info.AmmoDef.Const.CollisionIsLine && !p.Info.EwarAreaPulse;
                hitEntity.Info = p.Info;
                hitEntity.Entity = voxel;
                hitEntity.Intersection = p.Beam;
                hitEntity.SphereCheck = !lineCheck;
                hitEntity.PruneSphere = p.PruneSphere;
                hitEntity.DamageOverTime = p.Info.AmmoDef.Const.EwarType == Dot;

                var hitPos = voxelHit.Value;
                hitEntity.HitPos = hitPos;

                double dist;
                Vector3D.Distance(ref p.Beam.From, ref hitPos, out dist);
                hitEntity.HitDist = dist;

                hitEntity.EventType = Voxel;
                p.Info.HitList.Add(hitEntity);
            }
            DeferedVoxels.Clear();
        }

        internal void FinalizeHits()
        {
            var vhCount = FinalHitCheck.Count;
            var minCount = Session.Settings.Enforcement.BaseOptimizations ? 96 : 99999;
            var targetStride = vhCount / 20;
            var stride = vhCount < minCount ? 100000 : targetStride > 48 ? targetStride : 48;

            MyAPIGateway.Parallel.For(0, FinalHitCheck.Count, x =>
            {
                var p = FinalHitCheck[x];

                p.Intersecting = GenerateHitInfo(p);
                var info = p.Info;

                if (p.Intersecting)
                {
                    var aConst = info.AmmoDef.Const;
                    if (aConst.VirtualBeams)
                    {

                        info.Weapon.WeaponCache.VirtualHit = true;
                        info.Weapon.WeaponCache.HitEntity.Entity = info.Hit.Entity;
                        info.Weapon.WeaponCache.HitEntity.HitPos = info.Hit.SurfaceHit;
                        info.Weapon.WeaponCache.Hits = p.VrPros.Count;
                        info.Weapon.WeaponCache.HitDistance = Vector3D.Distance(p.LastPosition, info.Hit.SurfaceHit);

                        if (info.Hit.Entity is MyCubeGrid) info.Weapon.WeaponCache.HitBlock = info.Hit.Block;
                    }

                    lock (Session.Hits)
                    {
                        if (Session.IsClient && info.AimedShot && aConst.ClientPredictedAmmo && !info.IsFragment)
                        {
                            SendClientHit(p, true);
                        }
                        Session.Hits.Add(p);
                    }
                    return;
                }

                info.HitList.Clear();
            },stride);
            FinalHitCheck.Clear();
        }

        internal static void SendClientHit(Projectile p, bool hit)
        {
            var info = p.Info;
            var aConst = p.Info.AmmoDef.Const;

            var isBeam = aConst.IsBeamWeapon;
            var vel = isBeam ? Vector3D.Zero : !MyUtils.IsZero(p.Velocity) ? p.Velocity : p.PrevVelocity;

            var firstHitEntity = hit ? info.HitList[0] : null;
            var hitDist = hit ? firstHitEntity?.HitDist ?? info.MaxTrajectory : info.MaxTrajectory;
            var distToTarget = aConst.IsBeamWeapon ? hitDist : info.MaxTrajectory - info.DistanceTraveled;

            var intersectOrigin = isBeam ? new Vector3D(p.Beam.From + (info.Direction * distToTarget)) : p.LastPosition;

            info.Ai.Session.SendFixedGunHitEvent(hit, info.Weapon.Comp.CoreEntity, info.Hit.Entity, intersectOrigin, vel, info.OriginUp, info.MuzzleId, info.Weapon.System.WeaponIdHash, aConst.AmmoIdxPos, (float)(isBeam ? info.MaxTrajectory : distToTarget));
            info.AimedShot = false; //to prevent hits on another grid from triggering again
        }

        internal void ProjectileHit(Projectile attacker, Projectile target, bool lineCheck, ref LineD beam)
        {
            var hitEntity = HitEntityPool.Get();
            hitEntity.Info = attacker.Info;
            hitEntity.EventType = HitEntity.Type.Projectile;
            hitEntity.Hit = true;
            hitEntity.Projectile = target;
            hitEntity.SphereCheck = !lineCheck;
            hitEntity.PruneSphere = attacker.PruneSphere;
            double dist;
            Vector3D.Distance(ref beam.From, ref target.Position, out dist);
            hitEntity.HitDist = dist;

            hitEntity.Intersection = new LineD(attacker.LastPosition, attacker.LastPosition + (attacker.Info.Direction * dist));
            hitEntity.HitPos = hitEntity.Intersection.To;

            lock (attacker.Info.HitList)
                attacker.Info.HitList.Add(hitEntity);

            attacker.FinalizeIntersection = true;
        }

        internal bool GenerateHitInfo(Projectile p)
        {
            var info = p.Info;
            var count = info.HitList.Count;
            if (count > 1)
            {
                try { info.HitList.Sort((x, y) => GetEntityCompareDist(x, y, info)); } // Unable to sort because the IComparer.Compare() method returns inconsistent results
                catch (Exception ex) { Log.Line($"p.Info.HitList.Sort failed: {ex} - weapon:{info.Weapon.System.PartName} - ammo:{info.AmmoDef.AmmoRound} - hitCount:{info.HitList.Count}", null, true); } 
            }
            else GetEntityCompareDist(info.HitList[0], null, info);
            var pulseTrigger = false;
            var voxelFound = false;
            for (int i = info.HitList.Count - 1; i >= 0; i--)
            {
                var ent = info.HitList[i];

                if (ent.EventType == Voxel)
                    voxelFound = true;

                if (!ent.Hit)
                {
                    if (ent.PulseTrigger) pulseTrigger = true;
                    info.HitList.RemoveAtFast(i);
                    HitEntityPool.Return(ent);
                }
                else break;
            }

            if (pulseTrigger)
            {

                info.EwarAreaPulse = true;
                p.DistanceToTravelSqr = info.DistanceTraveled * info.DistanceTraveled;
                p.PrevVelocity = p.Velocity;
                p.Velocity = Vector3D.Zero;
                info.Hit.SurfaceHit = p.Position + info.Direction * info.AmmoDef.Const.EwarTriggerRange;
                info.Hit.LastHit = info.Hit.SurfaceHit;
                info.HitList.Clear();
                return false;
            }


            var finalCount = info.HitList.Count;
            if (finalCount > 0)
            {
                var aConst = info.AmmoDef.Const;
                if (voxelFound && info.HitList[0].EventType != Voxel && aConst.IsBeamWeapon)
                    info.VoxelCache.HitRefreshed = 0;

                var checkHit = (!aConst.IsBeamWeapon || !info.ShieldBypassed || finalCount > 1); ;

                var blockingEnt = !info.ShieldBypassed || finalCount == 1 ? 0 : 1;
                var hitEntity = info.HitList[blockingEnt];

                if (!checkHit)
                    hitEntity.HitPos = p.Beam.To;

                Vector3D? lastHitVel = Vector3D.Zero;
                if (hitEntity.EventType == Shield)
                {
                    var cube = hitEntity.Entity as MyCubeBlock;
                    if (cube?.CubeGrid?.Physics != null)
                        lastHitVel = cube.CubeGrid.Physics.LinearVelocity;
                }
                else if (hitEntity.Projectile != null)
                    lastHitVel = hitEntity.Projectile?.Velocity;
                else if (hitEntity.Entity?.Physics != null)
                    lastHitVel = hitEntity.Entity?.Physics.LinearVelocity;
                else lastHitVel = Vector3D.Zero;

                var grid = hitEntity.Entity as MyCubeGrid;

                IMySlimBlock hitBlock = null;
                Vector3D? visualHitPos;
                if (grid != null)
                {
                    if (aConst.VirtualBeams)
                        hitBlock = hitEntity.Blocks[0].Block;

                    IHitInfo hitInfo = null;
                    if (Session.HandlesInput && hitEntity.HitPos.HasValue && Vector3D.DistanceSquared(hitEntity.HitPos.Value, Session.CameraPos) < 22500 && Session.CameraFrustrum.Contains(hitEntity.HitPos.Value) != ContainmentType.Disjoint)
                    {
                        var entSphere = hitEntity.Entity.PositionComp.WorldVolume;
                        var from = hitEntity.Intersection.From + (hitEntity.Intersection.Direction * MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref hitEntity.Intersection.From, ref entSphere));
                        var to = hitEntity.HitPos.Value + (hitEntity.Intersection.Direction * 3f);
                        Session.Physics.CastRay(from, to, out hitInfo, 15);
                    }
                    visualHitPos = hitInfo?.HitEntity != null ? hitInfo.Position : hitEntity.HitPos;
                }
                else visualHitPos = hitEntity.HitPos;
                info.Hit = new Hit { Block = hitBlock, Entity = hitEntity.Entity, EventType = hitEntity.EventType, LastHit = visualHitPos ?? Vector3D.Zero, SurfaceHit = visualHitPos ?? Vector3D.Zero, HitVelocity = lastHitVel ?? Vector3D.Zero, HitTick = Session.Tick};

                //if (Session.DebugVersion && info.Ai.AiType == Ai.AiTypes.Player)
                //    Session.AddHandHitDebug(p.Beam.From, p.Beam.To, false);

                if (p.EnableAv)
                {
                    info.AvShot.LastHitShield = hitEntity.EventType == Shield;
                    info.AvShot.Hit = info.Hit;
                    if (info.AimedShot && Session.TrackingAi != null && Session.TargetUi.HitIncrease < 0.1d && info.Weapon.Comp.Ai.ControlComp == null && (info.AmmoDef.Const.FixedFireAmmo || info.Weapon.Comp.Data.Repo.Values.Set.Overrides.Control != ProtoWeaponOverrides.ControlModes.Auto))
                        Session.TargetUi.SetHit(info);
                }

                return true;
            }
            return false;
        }

        internal int GetEntityCompareDist(HitEntity x, HitEntity y, ProInfo info)
        {
            var xDist = double.MaxValue;
            var yDist = double.MaxValue;
            var beam = x.Intersection;
            var count = y != null ? 2 : 1;
            var aConst = info.AmmoDef.Const;
            var eWarPulse = aConst.Ewar && aConst.Pulse;
            var triggerEvent = eWarPulse && !info.EwarAreaPulse && aConst.EwarTriggerRange > 0;
            for (int i = 0; i < count; i++)
            {
                var isX = i == 0;

                MyEntity ent;
                HitEntity hitEnt;
                if (isX)
                {
                    hitEnt = x;
                    ent = hitEnt.Entity;
                }
                else
                {
                    hitEnt = y;
                    ent = hitEnt.Entity;
                }

                var dist = double.MaxValue;
                var shield = ent as IMyTerminalBlock;
                var grid = ent as MyCubeGrid;
                var voxel = ent as MyVoxelBase;

                if (triggerEvent && ent != null && (info.Ai.Targets.ContainsKey(ent) || shield != null))
                    hitEnt.PulseTrigger = true;
                else if (hitEnt.Projectile != null)
                    dist = hitEnt.HitDist.Value;
                else if (shield != null)
                {
                    hitEnt.Hit = true;
                    dist = hitEnt.HitDist.Value;
                    info.ShieldInLine = true;
                }
                else if (grid != null)
                {
                    if (hitEnt.Hit)
                    {
                        dist = Vector3D.Distance(hitEnt.Intersection.From, hitEnt.HitPos.Value);
                        hitEnt.HitDist = dist;
                    }
                    else if (hitEnt.HitPos != null)
                    {
                        dist = Vector3D.Distance(hitEnt.Intersection.From, hitEnt.HitPos.Value);
                        hitEnt.HitDist = dist;
                        hitEnt.Hit = true;
                    }
                    else
                    {
                        if (hitEnt.SphereCheck || info.EwarActive && eWarPulse)
                        {
                            var ewarActive = hitEnt.EventType == Field || hitEnt.EventType == Effect;
                            var hitPos = !ewarActive ? hitEnt.PruneSphere.Center + (hitEnt.Intersection.Direction * hitEnt.PruneSphere.Radius) : hitEnt.PruneSphere.Center;
                            if (hitEnt.SelfHit && (Vector3D.DistanceSquared(hitPos, hitEnt.Info.Origin) <= grid.GridSize * grid.GridSize) && hitEnt.EventType != Field) 
                                continue;

                            if (!ewarActive)
                                GetAndSortBlocksInSphere(hitEnt.Info.AmmoDef, hitEnt.Info.Weapon.System, grid, hitEnt.PruneSphere, false, hitEnt.Blocks);

                            if (hitEnt.Blocks.Count > 0 || ewarActive)
                            {
                                dist = 0;
                                hitEnt.HitDist = dist;
                                hitEnt.Hit = true;
                                hitEnt.HitPos = hitPos;
                            }
                        }
                        else
                        {

                            var closestBlockFound = false;
                            IMySlimBlock lastBlockHit = null;
                            var ewarWeaponDamage = info.EwarActive && aConst.SelfDamage && hitEnt.EventType == Effect;
                            for (int j = 0; j < hitEnt.Vector3ICache.Count; j++)
                            {
                                var posI = hitEnt.Vector3ICache[j];
                                var firstBlock = grid.GetCubeBlock(posI) as IMySlimBlock;
                                if (firstBlock != null && firstBlock != lastBlockHit && !firstBlock.IsDestroyed && (hitEnt.Info.Ai.AiType != Ai.AiTypes.Grid || firstBlock != hitEnt.Info.Weapon.Comp.Cube.SlimBlock || ewarWeaponDamage && firstBlock == hitEnt.Info.Weapon.Comp.Cube.SlimBlock))
                                {
                                    lastBlockHit = firstBlock;
                                    hitEnt.Blocks.Add(new HitEntity.RootBlocks {Block = firstBlock, QueryPos = posI});
                                    if (closestBlockFound) continue;
                                    MyOrientedBoundingBoxD obb;
                                    var fat = firstBlock.FatBlock;
                                    if (fat != null)
                                        obb = new MyOrientedBoundingBoxD(fat.Model.BoundingBox, fat.PositionComp.WorldMatrixRef);
                                    else
                                    {
                                        Vector3 halfExt;
                                        firstBlock.ComputeScaledHalfExtents(out halfExt);
                                        var blockBox = new BoundingBoxD(-halfExt, halfExt);
                                        var gridMatrix = grid.PositionComp.WorldMatrixRef;
                                        gridMatrix.Translation = grid.GridIntegerToWorld(firstBlock.Position);
                                        obb = new MyOrientedBoundingBoxD(blockBox, gridMatrix);
                                    }

                                    var hitDist = obb.Intersects(ref beam) ?? Vector3D.Distance(beam.From, obb.Center);
                                    var hitPos = beam.From + (beam.Direction * hitDist);

                                    if (hitEnt.SelfHit && !info.Storage.SmartReady)
                                    {
                                        if (Vector3D.DistanceSquared(hitPos, hitEnt.Info.Origin) <= grid.GridSize * 3)
                                        {
                                            hitEnt.Blocks.Clear();
                                        }
                                        else
                                        {
                                            dist = hitDist;
                                            hitEnt.HitDist = dist;
                                            hitEnt.Hit = true;
                                            hitEnt.HitPos = hitPos;
                                        }
                                        break;
                                    }

                                    dist = hitDist;
                                    hitEnt.HitDist = dist;
                                    hitEnt.Hit = true;
                                    hitEnt.HitPos = hitPos;
                                    closestBlockFound = true;
                                }
                            }
                        }
                    }
                }
                else if (voxel != null)
                {
                    hitEnt.Hit = true;
                    dist = hitEnt.HitDist.Value;
                    hitEnt.HitDist = dist;
                    dist += 1.25;
                }
                else if (ent is IMyDestroyableObject)
                {

                    if (hitEnt.Hit)
                    {
                        dist = Vector3D.Distance(hitEnt.Intersection.From, hitEnt.HitPos.Value);
                    }
                    else
                    {
                        if (hitEnt.SphereCheck || info.EwarActive && eWarPulse)
                        {

                            var ewarActive = hitEnt.EventType == Field || hitEnt.EventType == Effect;
                            dist = 0;
                            hitEnt.HitDist = dist;
                            hitEnt.Hit = true;
                            var hitPos = !ewarActive ? hitEnt.PruneSphere.Center + (hitEnt.Intersection.Direction * hitEnt.PruneSphere.Radius) : hitEnt.PruneSphere.Center;
                            hitEnt.HitPos = hitPos;
                        }
                        else
                        {

                            var transform = ent.PositionComp.WorldMatrixRef;
                            var box = ent.PositionComp.LocalAABB;
                            var obb = new MyOrientedBoundingBoxD(box, transform);
                            dist = obb.Intersects(ref beam) ?? double.MaxValue;
                            if (dist < double.MaxValue)
                            {
                                hitEnt.Hit = true;
                                hitEnt.HitPos = beam.From + (beam.Direction * dist);
                                hitEnt.HitDist = dist;
                            }
                        }
                    }
                }

                if (isX) xDist = dist;
                else yDist = dist;
            }
            return xDist.CompareTo(yDist);
        }

        //TODO: In order to fix SphereShapes collisions with grids, this needs to be adjusted to take into account the Beam of the projectile
        internal static void GetAndSortBlocksInSphere(WeaponDefinition.AmmoDef ammoDef, WeaponSystem system, MyCubeGrid grid, BoundingSphereD sphere, bool fatOnly, List<HitEntity.RootBlocks> blocks)
        {
            var matrixNormalizedInv = grid.PositionComp.WorldMatrixNormalizedInv;
            Vector3D result;
            Vector3D.Transform(ref sphere.Center, ref matrixNormalizedInv, out result);
            var localSphere = new BoundingSphere(result, (float)sphere.Radius);
            var fieldType = ammoDef.Ewar.Type;
            var hitPos = sphere.Center;
            if (fatOnly)
            {
                TopMap map;
                if (system.Session.TopEntityToInfoMap.TryGetValue(grid, out map))
                {
                    foreach (var cube in map.MyCubeBocks)
                    {
                        switch (fieldType)
                        {
                            case JumpNull:
                                if (!(cube is MyJumpDrive)) continue;
                                break;
                            case EnergySink:
                                if (!(cube is IMyPowerProducer)) continue;
                                break;
                            case Anchor:
                                if (!(cube is MyThrust)) continue;
                                break;
                            case Nav:
                                if (!(cube is MyGyro)) continue;
                                break;
                            case Offense:
                                var valid = cube is IMyGunBaseUser || cube is MyConveyorSorter && system.Session.PartPlatforms.ContainsKey(cube.BlockDefinition.Id);
                                if (!valid) continue;
                                break;
                            case Emp:
                            case Dot:
                                if (fieldType == Emp && cube is IMyUpgradeModule && system.Session.CoreShieldBlockTypes.Contains(cube.BlockDefinition))
                                    continue;
                                break;
                            default: continue;
                        }
                        var block = cube.SlimBlock as IMySlimBlock;
                        if (!new BoundingBox(block.Min * grid.GridSize - grid.GridSizeHalf, block.Max * grid.GridSize + grid.GridSizeHalf).Intersects(localSphere))
                            continue;
                        blocks.Add(new HitEntity.RootBlocks {Block = block, QueryPos = block.Position});
                    }
                }
            }
            else
            {
                //usage:
                //var dict = (Dictionary<Vector3I, IMySlimBlock>)GetHackDict((IMySlimBlock) null);
                var tmpList = system.Session.SlimPool.Get();
                Session.GetBlocksInsideSphereFast(grid, ref sphere, true, tmpList);

                for (int i = 0; i < tmpList.Count; i++)
                {
                    var block = tmpList[i];
                    blocks.Add(new HitEntity.RootBlocks { Block = block, QueryPos = block.Position});
                }

                system.Session.SlimPool.Return(tmpList);
            }

            blocks.Sort((a, b) =>
            {
                var aPos = grid.GridIntegerToWorld(a.Block.Position);
                var bPos = grid.GridIntegerToWorld(b.Block.Position);
                return Vector3D.DistanceSquared(aPos, hitPos).CompareTo(Vector3D.DistanceSquared(bPos, hitPos));
            });
        }
        public static object GetHackDict<TVal>(TVal valueType) => new Dictionary<Vector3I, TVal>();

    }
}
