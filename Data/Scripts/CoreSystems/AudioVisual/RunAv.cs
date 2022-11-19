using System;
using System.Collections.Generic;
using CoreSystems.Platform;
using Jakaria.API;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;
using static VRageRender.MyBillboard;

namespace CoreSystems.Support
{
    class RunAv
    {
        internal readonly MyBillboard[][] BillBoardLinePool = new MyBillboard[5][];
        internal readonly Dictionary<ulong, MyParticleEffect> BeamEffects = new Dictionary<ulong, MyParticleEffect>();
        internal readonly Stack<AvShot> AvShotPool = new Stack<AvShot>(1024);
        internal readonly Stack<AvEffect> AvEffectPool = new Stack<AvEffect>(128);
        internal readonly Stack<LineReqCache> LineReqPool = new Stack<LineReqCache>(1024);
        internal readonly Stack<AfterGlow> Glows = new Stack<AfterGlow>(128);
        internal readonly Stack<MyEntity3DSoundEmitter> FireEmitters = new Stack<MyEntity3DSoundEmitter>();
        internal readonly Stack<MyEntity3DSoundEmitter> TravelEmitters = new Stack<MyEntity3DSoundEmitter>();
        internal readonly Stack<MyEntity3DSoundEmitter> PersistentEmitters = new Stack<MyEntity3DSoundEmitter>();


        internal readonly List<AvEffect> Effects1 = new List<AvEffect>(128);
        internal readonly List<AvEffect> Effects2 = new List<AvEffect>(128);
        internal readonly List<ParticleEvent> ParticlesToProcess = new List<ParticleEvent>(128);
        internal readonly List<AvShot> AvShots = new List<AvShot>(1024);
        internal readonly List<LineReqCache> LineRequests = new List<LineReqCache>(1024);
        internal readonly List<MyBillboard> LinesToAdd = new List<MyBillboard>(1024);
        internal readonly int BillBoardPoolDelay = 5;

        internal Session Session;

        internal int BillPoolLineIndex = -1;
        internal int BillLineIndex = -1;
        //internal int BillPoolQuadIndex = -1;
        //internal int BillQuadIndex = -1;

        internal int ExplosionCounter;
        internal int MaxExplosions = 100;

        internal bool ExplosionReady
        {
            get
            {
                if (ExplosionCounter + 1 <= MaxExplosions)
                {
                    ExplosionCounter++;
                    return true;
                }
                return false;
            }
        }

        internal RunAv(Session session)
        {
            Session = session;
            for (int i = 0; i < BillBoardLinePool.Length; i++)
                BillBoardLinePool[i] = new MyBillboard[30000];
        }


        private int _onScreens;
        private int _shrinks;
        private int _glows;
        private int _models;

        internal void End()
        {
            if (Effects1.Count > 0) RunAvEffects1();
            if (Effects2.Count > 0) RunAvEffects2();
            if (ParticlesToProcess.Count > 0) Session.ProcessParticles();

            for (int i = AvShots.Count - 1; i >= 0; i--)
            {
                var av = AvShots[i];
                var refreshed = av.LastTick == Session.Tick && !av.MarkForClose;
                if (refreshed)
                {
                    if (av.PrimeEntity != null)
                    {
                        _models++;

                        if (av.OnScreen != AvShot.Screen.None)
                        {
                            if (!av.PrimeEntity.InScene && !av.Cloaked)
                            {
                                av.PrimeEntity.InScene = true;
                                av.PrimeEntity.Render.UpdateRenderObject(true, false);
                            }
                            av.PrimeEntity.PositionComp.SetWorldMatrix(ref av.PrimeMatrix, null, false, false, false);
                        }

                        if ((av.Cloaked || av.OnScreen == AvShot.Screen.None) && av.PrimeEntity.InScene)
                        {
                            av.PrimeEntity.InScene = false;
                            av.PrimeEntity.Render.RemoveRenderObjects();
                        }
                    }

                    if (av.Triggered && av.TriggerEntity != null)
                    {
                        if (!av.AmmoDef.Ewar.Field.HideModel && (!av.TriggerEntity.InScene))
                        {
                            av.TriggerEntity.InScene = true;
                            av.TriggerEntity.Render.UpdateRenderObject(true, false);
                        }
                        av.TriggerEntity.PositionComp.SetWorldMatrix(ref av.TriggerMatrix, null, false, false, false);

                        if (av.OnScreen != AvShot.Screen.None && av.AmmoDef.Const.FieldParticle && av.FieldEffect != null)
                            av.FieldEffect.WorldMatrix = av.PrimeMatrix;
                    }

                    if (av.HasTravelSound)
                    {
                        if (!av.TravelSound)
                        {
                            double distSqr;
                            Vector3D.DistanceSquared(ref av.TracerFront, ref Session.CameraPos, out distSqr);
                            if (distSqr <= av.AmmoDef.Const.AmmoTravelSoundDistSqr && av.AccelClearance)
                                av.TravelSoundStart();
                        }
                        else av.TravelEmitter.SetPosition(av.TracerFront);
                    }

                    if (av.HitParticle == AvShot.ParticleState.Custom)
                    {
                        av.HitParticle = AvShot.ParticleState.Dirty;
                        if (av.OnScreen != AvShot.Screen.None)
                        {
                            var pos = Session.Tick - av.Hit.HitTick <= 1 && !MyUtils.IsZero(av.Hit.SurfaceHit) ? av.Hit.SurfaceHit : av.TracerFront;
                            var particle = av.AmmoDef.AmmoGraphics.Particles.Hit;
                            var keenStrikesAgain = particle.Offset == Vector3D.MaxValue;
                            var matrix = !keenStrikesAgain ? MatrixD.CreateTranslation(pos) : MatrixD.CreateWorld(pos, av.VisualDir, av.OriginUp);

                            MyParticleEffect hitEffect;
                            if (MyParticlesManager.TryCreateParticleEffect(av.AmmoDef.Const.HitParticleStr, ref matrix, ref pos, uint.MaxValue, out hitEffect))
                            {
                                hitEffect.UserScale = av.AmmoDef.AmmoGraphics.Particles.Hit.Extras.Scale;
                                hitEffect.Velocity = av.Hit.HitVelocity;

                                if (hitEffect.Loop)
                                    hitEffect.Stop();
                            }
                        }
                    }
                    if (av.Hit.Entity != null && av.AmmoDef.AmmoGraphics.Decals.MaxAge > 0 && !Vector3D.IsZero(av.Hit.SurfaceHit) && av.AmmoDef.Const.TextureHitMap.Count > 0)
                    {
                        MySurfaceImpactEnum surfaceImpact;
                        MyStringHash materialType;
                        var beam = new LineD(av.TracerFront + -(av.Direction * av.StepSize), av.TracerFront + (av.Direction * 0.1f));
                        MyAPIGateway.Projectiles.GetSurfaceAndMaterial(av.Hit.Entity, ref beam, ref av.Hit.SurfaceHit, 0, out surfaceImpact, out materialType);

                        MyStringHash projectileMaterial;
                        if (av.AmmoDef.Const.TextureHitMap.TryGetValue(materialType, out projectileMaterial))
                        {
                            MyStringHash voxelMaterial = MyStringHash.NullOrEmpty;
                            var voxelBase = av.Hit.Entity as MyVoxelBase;
                            if (voxelBase != null)
                            {
                                Vector3D position = av.Hit.SurfaceHit;
                                MyVoxelMaterialDefinition materialAt = voxelBase.GetMaterialAt(ref position);
                                if (materialAt != null)
                                    voxelMaterial = materialAt.Id.SubtypeId;
                            }

                            var hitInfo = new MyHitInfo
                            {
                                Position = av.Hit.SurfaceHit + (av.Direction * 0.01),
                                Normal = av.Direction,
                            };

                            MyDecals.HandleAddDecal(av.Hit.Entity, hitInfo, Vector3.Zero, materialType, projectileMaterial, null, -1, voxelMaterial, false, MyDecalFlags.IgnoreOffScreenDeletion, MyAPIGateway.Session.GameplayFrameCounter + av.AmmoDef.AmmoGraphics.Decals.MaxAge);
                        }
                    }

                    if (av.Hit.EventType == HitEntity.Type.Water)
                    {
                        var splashHit = av.Hit.SurfaceHit;//Hopefully we can get a more precise surface intercept or correction?
                        var ammoInfo = av.AmmoDef;
                        var radius = ammoInfo.Const.CollisionSize > ammoInfo.Const.LargestHitSize ? (float)ammoInfo.Const.CollisionSize : (float)ammoInfo.Const.LargestHitSize;
                        if (radius < 3)
                            radius = 3;

                        WaterModAPI.CreateSplash(splashHit, radius, true);
                    }

                    if (av.Model != AvShot.ModelState.None)
                    {
                        if (av.AmmoEffect != null && av.AmmoDef.Const.AmmoParticle && av.AmmoDef.Const.PrimeModel)
                        {
                            ApproachConstants def = av.StageIdx == -1 ? null : av.AmmoDef.Const.Approaches[av.StageIdx];
                            var particleDef =  def == null || !def.AlternateTravelParticle ? av.AmmoDef.AmmoGraphics.Particles.Ammo : def.Definition.AlternateParticle;

                            var offVec = av.TracerFront + Vector3D.Rotate(particleDef.Offset, av.PrimeMatrix);
                            av.AmmoEffect.WorldMatrix = av.PrimeMatrix;
                            av.AmmoEffect.SetTranslation(ref offVec);
                        }
                    }
                    else if (av.AmmoEffect != null && av.AmmoDef.Const.AmmoParticle)
                    {
                        av.AmmoEffect.SetTranslation(ref av.TracerFront);
                    }
                }

                if (av.EndState.Dirty)
                    av.AvClose();
            }
        }

        internal void Run()
        {
            if (Session.Tick180)
            {
                Log.LineShortDate($"(DRAWS) --------------- AvShots:[{AvShots.Count}] OnScreen:[{_onScreens}] Shrinks:[{_shrinks}] Glows:[{_glows}] Models:[{_models}] P:[{Session.Projectiles.ActiveProjetiles.Count}] P-Pool:[{Session.Projectiles.ProjectilePool.Count}] AvPool:[{AvShotPool.Count}] (AvBarrels 1:[{Effects1.Count}] 2:[{Effects2.Count}])", "stats");
                _glows = 0;
                _shrinks = 0;
            }

            _onScreens = 0;
            _models = 0;
            for (int i = AvShots.Count - 1; i >= 0; i--)
            {
                var av = AvShots[i];
                if (av.OnScreen != AvShot.Screen.None) _onScreens++;
                var refreshed = av.LastTick == Session.Tick;
                var aConst = av.AmmoDef.Const;

                if (refreshed && av.Tracer != AvShot.TracerState.Off && av.OnScreen != AvShot.Screen.None)
                {
                    var color = av.Color;
                    var segColor = av.SegmentColor;

                    if (av.ShotFade > 0)
                    {
                        var fade = MathHelper.Clamp(1f - av.ShotFade, 0.005f, 1f);
                        color *= fade;
                        segColor *= fade;
                    }

                    if (!aConst.OffsetEffect)
                    {
                        if (av.Tracer != AvShot.TracerState.Shrink)
                        {
                            if (aConst.TracerMode == AmmoConstants.Texture.Normal)
                            {
                                var line = LineReqPool.Count > 0 ? LineReqPool.Pop() : new LineReqCache();
                                line.Material = aConst.TracerTextures[0];
                                line.Color = color;
                                line.StartPos = av.TracerBack;
                                line.Direction = av.VisualDir;
                                line.Length = (float) av.VisualLength;
                                line.Thickness = (float) av.TracerWidth;
                                LineRequests.Add(line);

                                //MyTransparentGeometry.AddLineBillboard(aConst.TracerTextures[0], color, av.TracerBack, av.VisualDir, (float)av.VisualLength, (float)av.TracerWidth);
                            }
                            else if (aConst.TracerMode != AmmoConstants.Texture.Resize)
                            {
                                var line = LineReqPool.Count > 0 ? LineReqPool.Pop() : new LineReqCache();
                                line.Material = aConst.TracerTextures[av.TextureIdx];
                                line.Color = color;
                                line.StartPos = av.TracerBack;
                                line.Direction = av.VisualDir;
                                line.Length = (float)av.VisualLength;
                                line.Thickness = (float)av.TracerWidth;
                                LineRequests.Add(line);

                                //MyTransparentGeometry.AddLineBillboard(aConst.TracerTextures[av.TextureIdx], color, av.TracerBack, av.VisualDir, (float)av.VisualLength, (float)av.TracerWidth);

                            }
                            else
                            {

                                var seg = av.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation;
                                var stepPos = av.TracerBack;
                                var segTextureCnt = aConst.SegmentTextures.Length;
                                var gapTextureCnt = aConst.TracerTextures.Length;
                                var segStepLen = seg.SegmentLength / segTextureCnt;
                                var gapStepLen = seg.SegmentGap / gapTextureCnt;
                                var gapEnabled = gapStepLen > 0;
                                int j = 0;
                                double travel = 0;
                                while (travel < av.VisualLength)
                                {

                                    var mod = j++ % 2;
                                    var gap = gapEnabled && (av.SegmentGaped && mod == 0 || !av.SegmentGaped && mod == 1);
                                    var first = travel <= 0;

                                    double width;
                                    double rawLen;
                                    Vector4 dyncColor;
                                    if (!gap)
                                    {
                                        rawLen = first ? av.SegmentLenTranserved * Session.ClientAvDivisor : seg.SegmentLength * Session.ClientAvDivisor;
                                        if (rawLen <= 0)
                                            break;
                                        width = av.SegmentWidth;
                                        dyncColor = segColor;
                                    }
                                    else
                                    {
                                        rawLen = first ? av.SegmentLenTranserved * Session.ClientAvDivisor : seg.SegmentGap * Session.ClientAvDivisor;
                                        if (rawLen <= 0)
                                            break;
                                        width = av.TracerWidth;
                                        dyncColor = color;
                                    }

                                    var notLast = travel + rawLen < av.VisualLength;
                                    var len = notLast ? rawLen : av.VisualLength - travel;
                                    var clampStep = !gap ? MathHelperD.Clamp((int)((len / segStepLen) + 0.5) - 1, 0, segTextureCnt - 1) : MathHelperD.Clamp((int)((len / gapStepLen) + 0.5) - 1, 0, gapTextureCnt - 1);
                                    var material = !gap ? aConst.SegmentTextures[(int)clampStep] : aConst.TracerTextures[(int)clampStep];

                                    var line = LineReqPool.Count > 0 ? LineReqPool.Pop() : new LineReqCache();
                                    line.Material = material;
                                    line.Color = dyncColor;
                                    line.StartPos = stepPos;
                                    line.Direction = av.VisualDir;
                                    line.Length = (float)len;
                                    line.Thickness = (float)width;
                                    LineRequests.Add(line);

                                    //MyTransparentGeometry.AddLineBillboard(material, dyncColor, stepPos, av.VisualDir, (float)len, (float)width);
                                    if (!notLast)
                                        travel = av.VisualLength;
                                    else
                                        travel += len;
                                    stepPos += (av.VisualDir * len);
                                }
                            }
                        }
                    }
                    else
                    {
                        var list = av.Offsets;
                        for (int x = 0; x < list.Count; x++)
                        {
                            Vector3D fromBeam;
                            Vector3D toBeam;

                            if (x == 0)
                            {
                                fromBeam = av.OffsetMatrix.Translation;
                                toBeam = Vector3D.Transform(list[x], av.OffsetMatrix);
                            }
                            else
                            {
                                fromBeam = Vector3D.Transform(list[x - 1], av.OffsetMatrix);
                                toBeam = Vector3D.Transform(list[x], av.OffsetMatrix);
                            }

                            Vector3 dir = (toBeam - fromBeam);
                            var length = dir.Length();
                            var normDir = dir / length;

                            var line = LineReqPool.Count > 0 ? LineReqPool.Pop() : new LineReqCache();
                            line.Material = aConst.TracerTextures[0];
                            line.Color = color;
                            line.StartPos = fromBeam;
                            line.Direction = normDir;
                            line.Length = length;
                            line.Thickness = (float)av.TracerWidth;
                            LineRequests.Add(line);

                            //MyTransparentGeometry.AddLineBillboard(aConst.TracerTextures[0], color, fromBeam, normDir, length, (float)av.TracerWidth);

                            if (Vector3D.DistanceSquared(av.OffsetMatrix.Translation, toBeam) > av.TracerLengthSqr) break;
                        }
                        list.Clear();
                    }
                }

                var shrinkCnt = av.TracerShrinks.Count;
                if (shrinkCnt > _shrinks) _shrinks = shrinkCnt;

                if (shrinkCnt > 0)
                    RunShrinks(av);

                var glowCnt = av.GlowSteps.Count;

                if (glowCnt > _glows)
                    _glows = glowCnt;

                if (av.Trail != AvShot.TrailState.Off)
                {
                    var steps = av.DecayTime;
                    var widthScaler = !aConst.TrailColorFade;
                    var remove = false;
                    for (int j = glowCnt - 1; j >= 0; j--)
                    {
                        var glow = av.GlowSteps[j];

                        if (!refreshed)
                            glow.Line = new LineD(glow.Line.From + av.ShootVelStep, glow.Line.To + av.ShootVelStep, glow.Line.Length);

                        if (av.OnScreen != AvShot.Screen.None)
                        {
                            var reduction = (av.GlowShrinkSize * glow.Step);
                            var width = widthScaler ? (aConst.TrailWidth - reduction) * av.TrailScaler : aConst.TrailWidth * av.TrailScaler;
                            var color = aConst.LinearTrailColor;
                            if (!widthScaler)
                            {
                                color *= MathHelper.Clamp(1f - reduction, 0.01f, 1f);
                            }

                            var line = LineReqPool.Count > 0 ? LineReqPool.Pop() : new LineReqCache();
                            line.Material = aConst.TracerTextures[0];
                            line.Color = color;
                            line.StartPos = glow.Line.From;
                            line.Direction = glow.Line.Direction;
                            line.Length = (float) glow.Line.Length;
                            line.Thickness = width;
                            LineRequests.Add(line);

                            //MyTransparentGeometry.AddLineBillboard(aConst.TrailTextures[0], color, glow.Line.From, glow.Line.Direction, (float)glow.Line.Length, width);
                        }

                        if (++glow.Step >= steps)
                        {
                            glow.Parent = null;
                            glow.Step = 0;
                            remove = true;
                            glowCnt--;
                            Glows.Push(glow);
                        }
                    }

                    if (remove) av.GlowSteps.Dequeue();
                }

                if (glowCnt == 0 && shrinkCnt == 0 && av.MarkForClose)
                {
                    av.Close(AvShotPool);
                    AvShots.RemoveAtFast(i);
                }
            }

            AddBillBoardLines();
        }

        private void RunShrinks(AvShot av)
        {
            var s = av.TracerShrinks.Dequeue();
            if (av.LastTick != Session.Tick)
            {
                if (!av.AmmoDef.Const.OffsetEffect)
                {
                    if (av.OnScreen != AvShot.Screen.None)
                    {
                        var line = LineReqPool.Count > 0 ? LineReqPool.Pop() : new LineReqCache();
                        line.Material = av.AmmoDef.Const.TracerTextures[0];
                        line.Color = s.Color;
                        line.StartPos = s.NewFront;
                        line.Direction = av.VisualDir;
                        line.Length = s.Length;
                        line.Thickness = s.Thickness;
                        LineRequests.Add(line);

                        //MyTransparentGeometry.AddLineBillboard(av.AmmoDef.Const.TracerTextures[0], s.Color, s.NewFront, av.VisualDir, s.Length, s.Thickness);

                    }
                }
                else if (av.OnScreen != AvShot.Screen.None)
                    av.DrawLineOffsetEffect(s.NewFront, -av.Direction, s.Length, s.Thickness, s.Color);

                if (av.Trail != AvShot.TrailState.Off && av.Back)
                    av.RunGlow(ref s, true);
            }

            if (av.TracerShrinks.Count == 0) av.ResetHit();
        }

        internal void RunAvEffects1()
        {
            for (int i = Effects1.Count - 1; i >= 0; i--)
            {

                var avEffect = Effects1[i];
                var weapon = avEffect.Weapon;
                var muzzle = avEffect.Muzzle;
                var ticksAgo = weapon.Comp.Session.Tick - avEffect.StartTick;
                var bAv = weapon.System.Values.HardPoint.Graphics.Effect1;
                var effect = weapon.Effects1[muzzle.MuzzleId];

                var effectExists = effect != null;
                if (effectExists && avEffect.EndTick == 0 && weapon.StopBarrelAvTick >= Session.Tick - 1)
                    avEffect.EndTick = weapon.StopBarrelAvTick;

                var info = weapon.Dummies[muzzle.MuzzleId].Info;
                var somethingEnded = avEffect.EndTick != 0 && avEffect.EndTick <= Session.Tick || !weapon.PlayTurretAv || info.Entity == null || info.Entity.MarkedForClose || weapon.Comp.Ai == null || weapon.MuzzlePart.Entity?.Parent == null && weapon.Comp.GunBase == null || weapon.Comp.CoreEntity.MarkedForClose || weapon.MuzzlePart.Entity == null || weapon.MuzzlePart.Entity.MarkedForClose;

                var effectStale = effectExists && (effect.IsEmittingStopped || effect.IsStopped) || !effectExists && ticksAgo > 0;
                if (effectStale || somethingEnded || !weapon.Comp.IsWorking)
                {
                    if (effectExists)
                    {
                        effect.Stop(bAv.Extras.Restart);
                        weapon.Effects1[muzzle.MuzzleId] = null;
                    }
                    muzzle.Av1Looping = false;
                    muzzle.LastAv1Tick = 0;
                    Effects1.RemoveAtFast(i);
                    avEffect.Clean(AvEffectPool);
                    continue;
                }

                if (weapon.Comp.Ai.VelocityUpdateTick != weapon.Comp.Session.Tick)
                {
                    weapon.Comp.Ai.TopEntityVolume.Center = weapon.Comp.TopEntity.PositionComp.WorldVolume.Center;
                    weapon.Comp.Ai.TopEntityVel = weapon.Comp.TopEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                    weapon.Comp.Ai.IsStatic = weapon.Comp.TopEntity.Physics?.IsStatic ?? false;
                    weapon.Comp.Ai.VelocityUpdateTick = weapon.Comp.Session.Tick;
                }


                var particles = weapon.System.Values.HardPoint.Graphics.Effect1;
                var renderId = info.Entity.Render.GetRenderObjectID();
                var matrix = info.DummyMatrix;
                var pos = info.Position;
                matrix.Translation = info.LocalPosition + particles.Offset;

                if (!effectExists && ticksAgo <= 0)
                {
                    MyParticleEffect newEffect;
                    if (MyParticlesManager.TryCreateParticleEffect(particles.Name, ref matrix, ref pos, renderId, out newEffect))
                    {
                        newEffect.UserScale = particles.Extras.Scale;
                        if (newEffect.Loop)
                        {
                            weapon.Effects1[muzzle.MuzzleId] = newEffect;
                            muzzle.Av1Looping = true;
                        }
                        else
                        {
                            muzzle.Av1Looping = false;
                            muzzle.LastAv1Tick = 0;
                            Effects1.RemoveAtFast(i);
                            avEffect.Clean(AvEffectPool);
                        }
                    }
                }
                else if (effectExists)
                {
                    effect.WorldMatrix = matrix;
                }
            }
        }

        internal void RunAvEffects2()
        {
            for (int i = Effects2.Count - 1; i >= 0; i--)
            {
                var av = Effects2[i];
                var weapon = av.Weapon;
                var muzzle = av.Muzzle;
                var ticksAgo = weapon.Comp.Session.Tick - av.StartTick;
                var bAv = weapon.System.Values.HardPoint.Graphics.Effect2;

                var effect = weapon.Effects2[muzzle.MuzzleId];
                var effectExists = effect != null;
                if (effectExists && av.EndTick == 0 && weapon.StopBarrelAvTick >= Session.Tick - 1)
                    av.EndTick = weapon.StopBarrelAvTick;

                var info = weapon.Dummies[muzzle.MuzzleId].Info;
                var somethingEnded = av.EndTick != 0 && av.EndTick <= Session.Tick || !weapon.PlayTurretAv || info.Entity == null || info.Entity.MarkedForClose || weapon.Comp.Ai == null || weapon.MuzzlePart.Entity?.Parent == null && weapon.Comp.GunBase == null || weapon.Comp.CoreEntity.MarkedForClose || weapon.MuzzlePart.Entity == null || weapon.MuzzlePart.Entity.MarkedForClose;

                var effectStale = effectExists && (effect.IsEmittingStopped || effect.IsStopped) || !effectExists && ticksAgo > 0;

                if (effectStale || somethingEnded || !weapon.Comp.IsWorking)
                {
                    if (effectExists)
                    {
                        effect.Stop(bAv.Extras.Restart);
                        weapon.Effects2[muzzle.MuzzleId] = null;
                    }
                    muzzle.Av2Looping = false;
                    muzzle.LastAv2Tick = 0;
                    Effects2.RemoveAtFast(i);
                    av.Clean(AvEffectPool);
                    continue;
                }

                if (weapon.Comp.Ai.VelocityUpdateTick != weapon.Comp.Session.Tick)
                {
                    weapon.Comp.Ai.TopEntityVolume.Center = weapon.Comp.TopEntity.PositionComp.WorldVolume.Center;
                    weapon.Comp.Ai.TopEntityVel = weapon.Comp.TopEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                    weapon.Comp.Ai.IsStatic = weapon.Comp.TopEntity.Physics?.IsStatic ?? false;
                    weapon.Comp.Ai.VelocityUpdateTick = weapon.Comp.Session.Tick;
                }

                var particles = weapon.System.Values.HardPoint.Graphics.Effect2;
                var renderId = info.Entity.Render.GetRenderObjectID();
                var matrix = info.DummyMatrix;
                var pos = info.Position;
                matrix.Translation = info.LocalPosition + particles.Offset;

                if (!effectExists && ticksAgo <= 0)
                {
                    MyParticleEffect newEffect;
                    if (MyParticlesManager.TryCreateParticleEffect(particles.Name, ref matrix, ref pos, renderId, out newEffect))
                    {
                        newEffect.UserScale = particles.Extras.Scale;

                        if (newEffect.Loop)
                        {
                            weapon.Effects2[muzzle.MuzzleId] = newEffect;
                            muzzle.Av2Looping = true;
                        }
                        else
                        {
                            muzzle.Av2Looping = false;
                            muzzle.LastAv2Tick = 0;
                            Effects2.RemoveAtFast(i);
                            av.Clean(AvEffectPool);
                        }
                    }
                }
                else if (effectExists)
                {

                    effect.WorldMatrix = matrix;
                }
            }
        }
        /*
        internal void AddBillboardsOriented()
        {
            BillPoolQuadIndex = BillBoardPoolDelay + 1 < BillBoardPoolDelay ? BillBoardPoolDelay + 1 : 0;
            BillQuadIndex = 0;
            for (int i = 0; i < QuadRequests.Count; i++)
            {
                var q = QuadRequests[i];
                var billBoardPool = BillBoardQuadPool[BillPoolQuadIndex];

                if (BillQuadIndex++ > billBoardPool.Length)
                    continue;

                MyQuadD quad;
                Vector3 lv3 = q.Left;
                Vector3 uv3 = q.Up;

                MyUtils.GetBillboardQuadOriented(out quad, ref q.TextPos, q.Width / 2f, q.Height / 2f, ref lv3, ref uv3);

                var billBoard = billBoardPool[BillLineIndex];
                if (billBoard == null)
                    billBoardPool[BillLineIndex] = billBoard = new MyBillboard();

                billBoard.UVOffset = q.UvOff / q.TextureSize;
                billBoard.UVSize = q.UvSize / q.TextureSize;
                billBoard.LocalType = LocalTypeEnum.Custom;
                billBoard.Reflectivity = 0f;
                billBoard.SoftParticleDistanceScale = 0f;
                billBoard.DistanceSquared = (float)Vector3D.DistanceSquared(q.TextPos, Session.CameraPos);
                billBoard.Material = q.V1;
                billBoard.Position0 = quad.Point0;
                billBoard.Position1 = quad.Point1;
                billBoard.Position2 = quad.Point2;
                billBoard.Position3 = quad.Point3;
                billBoard.ColorIntensity = 1f;
                billBoard.Color = q.FontColor;
                billBoard.BlendType = q.Blend;
                billBoard.ParentID = uint.MaxValue;
                billBoard.CustomViewProjection = -1;
                QuadsToAdd.Add(billBoard);
            }
            MyTransparentGeometry.AddBillboards(QuadsToAdd, false);
            QuadsToAdd.Clear();
            QuadRequests.Clear();
        }
        */
        internal void AddBillBoardLines()
        {
            BillPoolLineIndex = BillBoardPoolDelay + 1 < BillBoardPoolDelay ? BillBoardPoolDelay + 1 : 0;
            var billBoardPool = BillBoardLinePool[BillPoolLineIndex];

            BillLineIndex = 0;
            for (int i = 0; i < LineRequests.Count; i++)
            {
                var q = LineRequests[i];

                if (++BillLineIndex >= billBoardPool.Length)
                    continue;

                if (billBoardPool[BillLineIndex] == null)
                    billBoardPool[BillLineIndex] = new MyBillboard();

                var billBoard = billBoardPool[BillLineIndex];

                billBoard.BlendType = q.Blend;
                billBoard.UVOffset = Vector2.Zero;
                billBoard.UVSize = Vector2.One;
                billBoard.LocalType = LocalTypeEnum.Custom;
                billBoard.ParentID = uint.MaxValue;

                var polyLine = new MyPolyLineD
                {
                    LineDirectionNormalized = q.Direction,
                    Point0 = q.StartPos,
                    Point1 = q.StartPos + q.Direction * q.Length,
                    Thickness = q.Thickness
                };

                var scaledVector = Vector3D.Cross(polyLine.LineDirectionNormalized, Vector3D.Normalize(Session.CameraPos - polyLine.Point0)) * polyLine.Thickness;
                var retQuad = new MyQuadD
                {
                    Point0 = polyLine.Point0 - scaledVector,
                    Point1 = polyLine.Point1 - scaledVector,
                    Point2 = polyLine.Point1 + scaledVector,
                    Point3 = polyLine.Point0 + scaledVector
                };

                var color = q.Color;

                billBoard.ColorIntensity = 1f;
                billBoard.Material = q.Material;
                billBoard.Position0 = retQuad.Point0;
                billBoard.Position1 = retQuad.Point1;
                billBoard.Position2 = retQuad.Point2;
                billBoard.Position3 = retQuad.Point3;
                billBoard.DistanceSquared = (float)Vector3D.DistanceSquared(Session.CameraPos, q.StartPos);
                billBoard.Color = color;
                billBoard.Reflectivity = 0;
                billBoard.CustomViewProjection = -1;
                billBoard.SoftParticleDistanceScale = 1f;

                LinesToAdd.Add(billBoard);
                LineReqPool.Push(q);
            }
            MyTransparentGeometry.AddBillboards(LinesToAdd, false);
            LinesToAdd.Clear();
            LineRequests.Clear();
        }

        public class QuadReqCache
        {
            public MyStringId V1;
            public Vector4 FontColor;
            public Vector3D TextPos;
            public Vector3D Up;
            public Vector3D Left;
            public Vector2 UvOff;
            public Vector2 UvSize;
            public float Width;
            public float Height;
            public float TextureSize;
            public BlendTypeEnum Blend;
        }

        public class LineReqCache
        {
            public MyStringId Material;
            public Vector4 Color;
            public Vector3D StartPos;
            public Vector3D Direction;
            public float Length;
            public float Thickness;
            public float Intensity;
            public BlendTypeEnum Blend;
        }

        /*
        public static void GetBillboardTriOriented(out MyTriangle tri, ref Vector3D position, Vector2 uvP0, Vector2 uvP1, Vector2 uvP2, float width, float height, ref Vector3 leftVector, ref Vector3 upVector)
        {
            Vector3D billboardAxisX = leftVector * width;
            Vector3D billboardAxisY = upVector * height;

            //    Coordinates of four points of a billboard's quad
            //tri.Edge0 = position - uvP0.X * billboardAxisX - uvP0.Y * billboardAxisY;
            //tri.Edge1 = position - uvP1.X * billboardAxisX - uvP1.Y * billboardAxisY;
            //tri.Edge2 = position - uvP2.X * billboardAxisX - uvP2.Y * billboardAxisY;
            //quad.Point3 = position - billboardAxisY + billboardAxisX;
        }
        */
        internal class AvEffect
        {
            internal Weapon Weapon;
            internal Weapon.Muzzle Muzzle;
            internal uint StartTick;
            internal uint EndTick;

            internal void Clean(Stack<AvEffect> avEffectPool)
            {
                Weapon = null;
                Muzzle = null;
                StartTick = 0;
                EndTick = 0;
                avEffectPool.Push(this);
            }
        }
    }
}
