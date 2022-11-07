using System;
using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game;
using VRageMath;
using static CoreSystems.Support.NewProjectile;
using static CoreSystems.Support.WeaponDefinition.AmmoDef;

namespace CoreSystems.Projectiles
{
    public partial class Projectiles
    {
        private void GenProjectiles()
        {
            for (int i = 0; i < NewProjectiles.Count; i++)
            {
                var gen = NewProjectiles[i];
                var muzzle = gen.Muzzle;
                var w = muzzle.Weapon;
                var comp = w.Comp;
                var repo = comp.Data.Repo;
                var ai = comp.Ai;
                var wTarget = w.Target;

                var a = gen.AmmoDef;
                var weaponAmmoDef = w.ActiveAmmoDef.AmmoDef;
                var aConst = a.Const;
                var t = gen.Type;
                var virts = gen.NewVirts;
                var aimed = repo.Values.State.PlayerId == Session.PlayerId && comp.ActivePlayer || comp.TypeSpecific == CoreComponent.CompTypeSpecific.Phantom;

                var patternCycle = gen.PatternCycle;
                var targetable = weaponAmmoDef.Const.Health > 0 && !weaponAmmoDef.Const.IsBeamWeapon;
                var p = Session.Projectiles.ProjectilePool.Count > 0 ? Session.Projectiles.ProjectilePool.Pop() : new Projectile();
                var info = p.Info;
                var storage = info.Storage;
                var target = info.Target;

                info.Id = Session.Projectiles.CurrentProjectileId++;
                info.Weapon = w;
                info.CompSceneVersion = comp.SceneVersion;
                info.Ai = comp.Ai;
                info.AimedShot = aimed;
                info.AmmoDef = a;
                info.DoDamage = Session.IsServer && (!aConst.ClientPredictedAmmo || t == Kind.Client || !comp.ActivePlayer ); // shrapnel do not run this loop, but do inherit DoDamage from parent.

                target.Projectile = wTarget.Projectile;
                target.TargetEntity = t != Kind.Client ? wTarget.TargetEntity : gen.TargetEnt;

                if (t == Kind.Client)
                {
                    target.TargetState = target.TargetEntity != null ? Target.TargetStates.IsEntity : Target.TargetStates.None;
                    target.TargetPos = target.TargetEntity != null ? target.TargetEntity.PositionComp.WorldAABB.Center : Vector3D.Zero;
                }
                else
                {
                    target.TargetState = wTarget.TargetState;
                    target.TargetPos = wTarget.TargetPos;
                }

                p.TargetPosition = wTarget.TargetPos;


                storage.DummyTargets = null;
                if ((aConst.IsDrone || aConst.IsSmart))
                {
                    storage.TargetPosition = wTarget.TargetPos;
                    storage.OriginLookAtPos = w.MyPivotPos;

                    if (comp.FakeMode)
                        Session.PlayerDummyTargets.TryGetValue(repo.Values.State.PlayerId, out storage.DummyTargets);

                    if (aConst.ProjectileSync)
                        storage.SyncId = (long)w.Reload.EndId << 32 | w.ProjectileCounter & 0xFFFFFFFFL;
                }

                info.BaseDamagePool = aConst.BaseDamage;

                info.Random = new XorShiftRandomStruct((ulong)(w.TargetData.WeaponRandom.CurrentSeed + (w.Reload.EndId + w.ProjectileCounter++)));

                info.LockOnFireState = (w.LockOnFireState || !aConst.OverrideTarget && wTarget.TargetState == Target.TargetStates.IsEntity);
                info.ShooterVel = ai.TopEntityVel;

                info.OriginUp = t != Kind.Client ? muzzle.UpDirection : gen.OriginUp;
                info.MaxTrajectory = t != Kind.Client ? aConst.MaxTrajectoryGrows && w.FireCounter < a.Trajectory.MaxTrajectoryTime ? aConst.TrajectoryStep * w.FireCounter : aConst.MaxTrajectory : gen.MaxTrajectory;
                info.MuzzleId = t != Kind.Virtual ? muzzle.MuzzleId : -1;
                info.UniqueMuzzleId = muzzle.UniqueId;
                w.WeaponCache.VirutalId = t != Kind.Virtual ? -1 : w.WeaponCache.VirutalId;
                info.Origin = t != Kind.Client ? t != Kind.Virtual ? muzzle.Position : w.MyPivotPos : gen.Origin;
                info.Direction = t != Kind.Client ? t != Kind.Virtual ? gen.Direction : w.MyPivotFwd : gen.Direction;

                if (t == Kind.Client && !aConst.IsBeamWeapon) 
                    p.Velocity = gen.Velocity;
                
                float shotFade;
                if (aConst.HasShotFade && !aConst.VirtualBeams)
                {
                    if (patternCycle > a.AmmoGraphics.Lines.Tracer.VisualFadeStart)
                        shotFade = MathHelper.Clamp(((patternCycle - a.AmmoGraphics.Lines.Tracer.VisualFadeStart)) * aConst.ShotFadeStep, 0, 1);
                    else if (w.System.DelayCeaseFire && w.CeaseFireDelayTick != Session.Tick)
                        shotFade = MathHelper.Clamp(((Session.Tick - w.CeaseFireDelayTick) - a.AmmoGraphics.Lines.Tracer.VisualFadeStart) * aConst.ShotFadeStep, 0, 1);
                    else shotFade = 0;
                }
                else shotFade = 0;
                info.ShotFade = shotFade;

                var updateGravity = aConst.FeelsGravity && info.Ai.InPlanetGravity;
                if (updateGravity && Session.Tick - w.GravityTick > 119)
                {
                    w.GravityTick = Session.Tick;
                    float interference;
                    w.GravityPoint = Session.Physics.CalculateNaturalGravityAt(w.MyPivotPos, out interference);
                    w.GravityUnitDir = w.GravityPoint;
                    w.GravityLength = w.GravityUnitDir.Normalize();
                }

                p.Gravity = updateGravity ? w.GravityPoint * aConst.GravityMultiplier : Vector3D.Zero;

                if (t != Kind.Virtual)
                {
                    info.PrimeEntity = aConst.PrimeModel ? aConst.PrimeEntityPool.Get() : null;
                    info.TriggerEntity = aConst.TriggerModel ? Session.TriggerEntityPool.Get() : null;

                    if (targetable)
                        Session.Projectiles.AddTargets.Add(p);
                }
                else
                {
                    w.WeaponCache.VirtualHit = false;
                    w.WeaponCache.Hits = 0;
                    w.WeaponCache.HitEntity.Entity = null;
                    for (int j = 0; j < virts.Count; j++)
                    {
                        var v = virts[j];
                        p.VrPros.Add(v.Info);
                        if (!a.Const.RotateRealBeam) w.WeaponCache.VirutalId = 0;
                        else if (v.Rotate)
                        {
                            info.Origin = v.Muzzle.Position;
                            info.Direction = v.Muzzle.Direction;
                            w.WeaponCache.VirutalId = v.VirtualId;
                        }
                    }
                    virts.Clear();
                    VirtInfoPools.Return(virts);
                }
                Session.Projectiles.ActiveProjetiles.Add(p);
                p.Start();

                var monitor = comp.ProjectileMonitors[w.PartId];
                if (monitor.Count > 0)
                {
                    Session.MonitoredProjectiles[p.Info.Id] = p;
                    for (int j = 0; j < monitor.Count; j++)
                        monitor[j].Invoke(comp.CoreEntity.EntityId, w.PartId, info.Id, target.TargetId, p.Position, true);
                }
            }
            NewProjectiles.Clear();
        }

        private void SpawnFragments()
        {
            if (Session.FragmentsNeedingEntities.Count > 0)
                PrepFragmentEntities();

            int spawned = 0;
            for (int j = 0; j < ShrapnelToSpawn.Count; j++)
            {
                int count;
                ShrapnelToSpawn[j].Spawn(out count);
                spawned += count;
            }
            ShrapnelToSpawn.Clear();

            if (AddTargets.Count > 0)
                AddProjectileTargets();

            UpdateState(ActiveProjetiles.Count - spawned);
        }

        internal void PrepFragmentEntities()
        {
            for (int i = 0; i < Session.FragmentsNeedingEntities.Count; i++)
            {
                var frag = Session.FragmentsNeedingEntities[i];
                if (frag.AmmoDef.Const.PrimeModel && frag.PrimeEntity == null) frag.PrimeEntity = frag.AmmoDef.Const.PrimeEntityPool.Get();
                if (frag.AmmoDef.Const.TriggerModel && frag.TriggerEntity == null) frag.TriggerEntity = Session.TriggerEntityPool.Get();
            }
            Session.FragmentsNeedingEntities.Clear();
        }

        internal void AddProjectileTargets(Projectile reAdd = null) // This also for fragments and readds, not sure if there is better way
        {
            for (int i = 0; reAdd == null && i < AddTargets.Count || reAdd != null && i == 0; i++)
            {
                var p = reAdd ?? AddTargets[i];
                var info = p.Info;
                var overrides = info.Weapon.Comp.Data.Repo.Values.Set.Overrides;
                var ai = info.Ai;
                var target = info.Target;
                var topEntity = info.Weapon.Comp.TopEntity;
                var ammoDef = info.AmmoDef;

                for (int t = 0; t < ai.TargetAis.Count; t++)
                {

                    var targetAi = ai.TargetAis[t];

                    if (targetAi.PointDefense)
                    {
                        var targetSphere = targetAi.TopEntity.PositionComp.WorldVolume;

                        targetSphere.Radius = targetSphere.Radius * 3 < 300 ? 300 : targetSphere.Radius * 3;

                        var dumbAdd = false;

                        var notSmart = ammoDef.Trajectory.Guidance == TrajectoryDef.GuidanceType.None || overrides.Override && p.HadTarget == Projectile.HadTargetState.None;
                        if (notSmart)
                        {
                            if (Vector3.Dot(info.Direction, info.Origin - targetAi.TopEntity.PositionComp.WorldMatrixRef.Translation) < 0)
                            {
                                var testRay = new RayD(info.Origin, info.Direction);
                                var quickCheck = Vector3D.IsZero(targetAi.TopEntityVel, 0.025) && targetSphere.Intersects(testRay) != null;

                                if (!quickCheck)
                                {
                                    var deltaPos = targetSphere.Center - info.Origin;
                                    var deltaVel = targetAi.TopEntityVel - ai.TopEntityVel;
                                    var timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, ammoDef.Const.DesiredProjectileSpeed);
                                    var predictedPos = targetSphere.Center + (float)timeToIntercept * deltaVel;
                                    targetSphere.Center = predictedPos;
                                }

                                if (quickCheck || targetSphere.Intersects(testRay) != null)
                                    dumbAdd = true;
                            }
                        }

                        var cubeTarget = target.TargetEntity as MyCubeBlock;

                        var condition1 = cubeTarget == null && targetAi.TopEntity == topEntity;
                        var condition2 = targetAi.AiType == Ai.AiTypes.Grid && (targetAi.GridEntity.IsStatic || cubeTarget != null && targetAi.GridEntity.IsSameConstructAs(cubeTarget.CubeGrid));
                        Ai.TargetInfo tInfo;
                        var condition3 = !condition1 && !condition2 && cubeTarget != null && !notSmart && targetSphere.Contains(cubeTarget.CubeGrid.PositionComp.WorldVolume) != ContainmentType.Disjoint && !targetAi.Targets.TryGetValue(cubeTarget.CubeGrid, out tInfo);
                        var condition4 = target.TargetState == Target.TargetStates.IsFake;

                        var validAi = !notSmart && (condition1 || condition2 || condition3 || condition4);

                        if ((dumbAdd || validAi) && (reAdd == null || !targetAi.LiveProjectile.Contains(p)))
                        {
                            targetAi.DeadProjectiles.Remove(p);
                            targetAi.LiveProjectile.Add(p);
                            targetAi.LiveProjectileTick = Session.Tick;
                            targetAi.NewProjectileTick = Session.Tick;
                            p.Watchers.Add(targetAi);
                        }
                    }

                }
            }
            AddTargets.Clear();
        }
    }
}
