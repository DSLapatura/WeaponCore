using System;
using System.Collections;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.TrajectoryDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.EwarDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.ShapeDef.Shapes;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.DamageScaleDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.FragmentDef.TimedSpawnDef;
using static CoreSystems.Support.ValueProcessors;
using static CoreSystems.Support.WeaponDefinition.HardPointDef;
using static CoreSystems.Support.CoreComponent;

namespace CoreSystems.Support
{
    public class AmmoConstants
    {
        public enum Texture
        {
            Normal,
            Cycle,
            Chaos,
            Resize,
            Wave,
        }

        private const string Arc = "Arc";
        private const string BackSlash = "\\";
        private const string BaseDmgStr = "BaseDamage";
        private const string AreaDmgStr = "AreaEffectDamage";
        private const string AreaRadStr = "AreaEffectRadius";
        private const string DetDmgStr = "DetonationDamage";
        private const string DetRadStr = "DetonationRadius";
        private const string HealthStr = "Health";
        private const string MaxTrajStr = "MaxTrajectory";
        private const string SpeedStr = "DesiredSpeed";
        private const string EnergyCostStr = "EnergyCost";
        private const string GravityStr = "GravityMultiplier";
        private const string ShieldModStr = "ShieldModifier";
        private const string EnergyBaseDmgStr = "EnergyBaseDamage";
        private const string EnergyAreaDmgStr = "EnergyAreaEffectDamage";
        private const string EnergyDetDmgStr = "EnergyDetonationDamage";
        private const string EnergyShieldDmgStr = "EnergyShieldDamage";
        private const string ClientPredAmmoStr = "DisableClientPredictedAmmo";
        private const string FallOffDistanceStr = "FallOffDistance";
        private const string FallOffMinMultStr = "FallOffMinMultipler";
        private const string ShieldBypassStr = "ShieldBypass";
        private const string MassStr = "Mass";
        private const string HealthHitModStr = "HealthHitModifier";
        private const string ByBlockHitMaxAbsorbStr = "ByBlockHitMaxAbsorb";
        private const string EndOfLifeMaxAbsorbStr = "EndOfLifeMaxAbsorb";

        private readonly Dictionary<string, BaseProcessor> _modifierMap = new Dictionary<string, BaseProcessor>()
        {
            {BaseDmgStr, new NonZeroFloatProcessor() },
            {AreaDmgStr, new FloatProcessor() },
            {AreaRadStr, new DoubleProcessor() },
            {DetDmgStr, new FloatProcessor() },
            {DetRadStr, new FloatProcessor() },
            {HealthStr, new FloatProcessor() },
            {MaxTrajStr, new FloatProcessor() },
            {SpeedStr, new FloatProcessor() },
            {EnergyCostStr, new FloatProcessor() },
            {GravityStr, new FloatProcessor() },
            {ShieldModStr, new DoubleProcessor() },
            {EnergyBaseDmgStr, new BoolProcessor() },
            {EnergyAreaDmgStr, new BoolProcessor() },
            {EnergyDetDmgStr, new BoolProcessor() },
            {EnergyShieldDmgStr, new BoolProcessor() },
            {ClientPredAmmoStr, new BoolProcessor() },
            {FallOffDistanceStr, new FloatProcessor() },
            {FallOffMinMultStr, new FloatProcessor() },
            {ShieldBypassStr, new FloatProcessor() },
            {MassStr, new FloatProcessor() },
            {HealthHitModStr, new DoubleProcessor() },
            {ByBlockHitMaxAbsorbStr, new FloatProcessor() },
            {EndOfLifeMaxAbsorbStr, new FloatProcessor() },
        };

        public readonly MyConcurrentPool<MyEntity> PrimeEntityPool;
        public readonly Dictionary<MyDefinitionBase, float> CustomBlockDefinitionBasesToScales;
        public readonly Dictionary<MyStringHash, MyStringHash> TextureHitMap = new Dictionary<MyStringHash, MyStringHash>();
        public readonly MySoundPair TravelSoundPair;
        public readonly Stack<int[]> PatternShuffleArray = new Stack<int[]>();
        public readonly MySoundPair ShotSoundPair;
        public readonly MySoundPair HitSoundPair;
        public readonly MySoundPair DetSoundPair;
        public readonly MySoundPair ShieldSoundPair;
        public readonly MySoundPair VoxelSoundPair;
        public readonly MySoundPair PlayerSoundPair;
        public readonly MySoundPair FloatingSoundPair;
        public readonly MyAmmoMagazineDefinition MagazineDef;
        public readonly ApproachConstants[] Approaches;
        public readonly AmmoDef[] AmmoPattern;
        public readonly MyStringId[] TracerTextures;
        public readonly MyStringId[] TrailTextures;
        public readonly MyStringId[] SegmentTextures;
        public readonly MyPhysicalInventoryItem AmmoItem;
        public readonly MyPhysicalInventoryItem EjectItem;
        public readonly Vector4 TrailColor;
        public readonly Vector3D FragOffset;
        public readonly EwarType EwarType;
        public readonly Texture TracerMode;
        public readonly Texture TrailMode;
        public readonly PointTypes FragPointType;
        public readonly string ModelPath;
        public readonly string HitParticleStr;
        public readonly string DetParticleStr;
        public readonly string DetSoundStr;
        public readonly string ShotSoundStr;
        public readonly int ApproachesCount;
        public readonly int MaxObjectsHit;
        public readonly int TargetLossTime;
        public readonly int MaxLifeTime;
        public readonly int MinArmingTime;
        public readonly int MaxTargets;
        public readonly int PulseInterval;
        public readonly int PulseChance;
        public readonly int PulseGrowTime;
        public readonly int EnergyMagSize;
        public readonly int FragmentId = -1;
        public readonly int MaxChaseTime;
        public readonly int MagazineSize;
        public readonly int WeaponPatternCount;
        public readonly int FragPatternCount;
        public readonly int AmmoIdxPos;
        public readonly int MagsToLoad;
        public readonly int MaxAmmo;
        public readonly int DecayTime;
        public readonly int FragMaxChildren;
        public readonly int FragStartTime;
        public readonly int FragInterval;
        public readonly int MaxFrags;
        public readonly int FragGroupSize;
        public readonly int FragGroupDelay;
        public readonly int DeformDelay;

        public readonly bool CheckFutureIntersection;
        public readonly bool OverrideTarget;
        public readonly bool HasEjectEffect;
        public readonly bool Pulse;
        public readonly bool PrimeModel;
        public readonly bool TriggerModel;
        public readonly bool CollisionIsLine;
        public readonly bool SelfDamage;
        public readonly bool VoxelDamage;
        public readonly bool OffsetEffect;
        public readonly bool Trail;
        public readonly bool TrailColorFade;
        public readonly bool IsMine;
        public readonly bool IsField;
        public readonly bool AmmoParticle;
        public readonly bool HitParticle;
        public readonly bool CustomDetParticle;
        public readonly bool FieldParticle;
        public readonly bool AmmoSkipAccel;
        public readonly bool LineWidthVariance;
        public readonly bool LineColorVariance;
        public readonly bool SegmentWidthVariance;
        public readonly bool SegmentColorVariance;
        public readonly bool OneHitParticle;
        public readonly bool DamageScaling;
        public readonly bool ArmorScaling;
        public readonly bool ArmorCoreActive;
        public readonly bool FallOffScaling;
        public readonly bool CustomDamageScales;
        public readonly bool SpeedVariance;
        public readonly bool RangeVariance;
        public readonly bool VirtualBeams;
        public readonly bool IsBeamWeapon;
        public readonly bool ConvergeBeams;
        public readonly bool RotateRealBeam;
        public readonly bool AmmoParticleNoCull;
        public readonly bool FieldParticleNoCull;
        public readonly bool HitParticleNoCull;
        public readonly bool DrawLine;
        public readonly bool Ewar;
        public readonly bool NonAntiSmartEwar;
        public readonly bool TargetOffSet;
        public readonly bool HasBackKickForce;
        public readonly bool BurstMode;
        public readonly bool EnergyAmmo;
        public readonly bool Reloadable;
        public readonly bool MustCharge;
        public readonly bool HasShotReloadDelay;
        public readonly bool HitSound;
        public readonly bool AmmoTravelSound;
        public readonly bool ShotSound;
        public readonly bool IsHybrid;
        public readonly bool IsTurretSelectable;
        public readonly bool CanZombie;
        public readonly bool FeelsGravity;
        public readonly bool MaxTrajectoryGrows;
        public readonly bool HasShotFade;
        public readonly bool CustomExplosionSound;
        public readonly bool GuidedAmmoDetected;
        public readonly bool AntiSmartDetected;
        public readonly bool TargetOverrideDetected;
        public readonly bool AlwaysDraw;
        public readonly bool FixedFireAmmo;
        public readonly bool ClientPredictedAmmo;
        public readonly bool IsCriticalReaction;
        public readonly bool AmmoModsFound;
        public readonly bool EnergyBaseDmg;
        public readonly bool EnergyAreaDmg;
        public readonly bool EnergyDetDmg;
        public readonly bool EnergyShieldDmg;
        public readonly bool SlowFireFixedWeapon;
        public readonly bool HasNegFragmentOffset;
        public readonly bool HasFragmentOffset;
        public readonly bool FragReverse;
        public readonly bool FragDropVelocity;
        public readonly bool FragOnEnd;
        public readonly bool ArmOnlyOnHit;
        public readonly bool FragIgnoreArming;
        public readonly bool FragOnArmed;
        public readonly bool LongTrail;
        public readonly bool ShortTrail;
        public readonly bool TinyTrail;
        public readonly bool RareTrail;
        public readonly bool EndOfLifeAv;
        public readonly bool EndOfLifeAoe;
        public readonly bool TimedFragments;
        public readonly bool HasFragProximity;
        public readonly bool FragParentDies;
        public readonly bool FragPointAtTarget;
        public readonly bool ProjectileSync;
        public readonly bool HasFragGroup;
        public readonly bool HasFragment;
        public readonly bool FragmentPattern;
        public readonly bool WeaponPattern;
        public readonly bool SkipAimChecks;
        public readonly bool RequiresTarget;
        public readonly bool HasAdvFragOffset;
        public readonly bool DetonationSound;
        public readonly bool CanReportTargetStatus;
        public readonly bool VoxelSound;
        public readonly bool PlayerSound;
        public readonly bool FloatingSound;
        public readonly bool ShieldSound;
        public readonly bool IsDrone;
        public readonly bool IsSmart;
        public readonly bool AccelClearance;
        public readonly bool DynamicGuidance;
        public readonly float PowerPerTick;
        public readonly float DirectAimCone;
        public readonly float FragRadial;
        public readonly float FragDegrees;
        public readonly float FragmentOffset;
        public readonly float FallOffDistance;
        public readonly float FallOffMinMultiplier;
        public readonly float EnergyCost;
        public readonly float ChargSize;
        public readonly float RealShotsPerMin;
        public readonly float TargetLossDegree;
        public readonly float TrailWidth;
        public readonly float ShieldDamageBypassMod;
        public readonly float MagMass;
        public readonly float MagVolume;
        public readonly float Health;
        public readonly float BaseDamage;
        public readonly float Mass;
        public readonly float DetMaxAbsorb;
        public readonly float AoeMaxAbsorb;
        public readonly float ByBlockHitDamage;
        public readonly float EndOfLifeDamage;
        public readonly float EndOfLifeRadius;
        public readonly float DesiredProjectileSpeed;
        public readonly float HitSoundDistSqr;
        public readonly float AmmoTravelSoundDistSqr;
        public readonly float ShotSoundDistSqr;
        public readonly float AmmoSoundMaxDistSqr;
        public readonly float BaseDps;
        public readonly float AreaDps;
        public readonly float EffectiveDps;
        public readonly float PerfectDps;
        public readonly float DetDps;
        public readonly float PeakDps;
        public readonly float RealShotsPerSec;
        public readonly float ShotsPerSec;
        public readonly float MaxTrajectory;
        public readonly float ShotFadeStep;
        public readonly float TrajectoryStep;
        public readonly float GravityMultiplier;
        public readonly float EndOfLifeDepth;
        public readonly float ByBlockHitDepth;
        public readonly float DeltaVelocityPerTick;
        public readonly float DetonationSoundDistSqr;
        public readonly double LargestHitSize;
        public readonly double EwarRadius;
        public readonly double EwarStrength;
        public readonly double ByBlockHitRadius;
        public readonly double ShieldModifier;
        public readonly double MaxLateralThrust;
        public readonly double EwarTriggerRange;
        public readonly double TracerLength;
        public readonly double CollisionSize;
        public readonly double SmartsDelayDistSqr;
        public readonly double SegmentStep;
        public readonly double HealthHitModifier;
        public readonly double VoxelHitModifier;
        public readonly double MaxOffset;
        public readonly double MinOffsetLength;
        public readonly double MaxOffsetLength;
        public readonly double FragProximity;
        public readonly double SmartOffsetSqr;
        public readonly double HeatModifier;

        internal AmmoConstants(WeaponSystem.AmmoType ammo, WeaponDefinition wDef, Session session, WeaponSystem system, int ammoIndex)
        {

            AmmoIdxPos = ammoIndex;
            MyInventory.GetItemVolumeAndMass(ammo.AmmoDefinitionId, out MagMass, out MagVolume);
            MagazineDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(ammo.AmmoDefinitionId);

            IsCriticalReaction = wDef.HardPoint.HardWare.CriticalReaction.Enable;

            ComputeTextures(ammo, out TracerTextures, out SegmentTextures, out TrailTextures, out TracerMode, out TrailMode);

            if (ammo.AmmoDefinitionId.SubtypeId.String != "Energy" || ammo.AmmoDefinitionId.SubtypeId.String == string.Empty) AmmoItem = new MyPhysicalInventoryItem { Amount = 1, Content = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_AmmoMagazine>(ammo.AmmoDefinitionId.SubtypeName) };

            if (!string.IsNullOrEmpty(ammo.EjectionDefinitionId.SubtypeId.String))
            {
                var itemEffect = ammo.AmmoDef.Ejection.Type == AmmoDef.EjectionDef.SpawnType.Item;
                if (itemEffect)
                    EjectItem = new MyPhysicalInventoryItem { Amount = 1, Content = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Component>(ammo.EjectionDefinitionId.SubtypeId.String) };
                HasEjectEffect = itemEffect && EjectItem.Content != null;
            }
            else if (ammo.AmmoDef.Ejection.Type == AmmoDef.EjectionDef.SpawnType.Particle && !string.IsNullOrEmpty(ammo.AmmoDef.AmmoGraphics.Particles.Eject.Name))
                HasEjectEffect = true;

            if (AmmoItem.Content != null && !session.AmmoItems.ContainsKey(AmmoItem.ItemId))
                session.AmmoItems[AmmoItem.ItemId] = AmmoItem;

            var fragGuidedAmmo = false;
            var fragAntiSmart = false;
            var fragTargetOverride = false;
            for (int i = 0; i < wDef.Ammos.Length; i++)
            {
                var ammoType = wDef.Ammos[i];

                if (ammoType.AmmoRound.Equals(ammo.AmmoDef.Fragment.AmmoRound))
                {
                    FragmentId = i;
                    var hasGuidance = ammoType.Trajectory.Guidance != None;
                    if (hasGuidance)
                        fragGuidedAmmo = true;

                    if (ammoType.Ewar.Type == EwarType.AntiSmart)
                        fragAntiSmart = true;

                    if (hasGuidance && ammoType.Trajectory.Smarts.OverideTarget)
                        fragTargetOverride = true;
                }
            }

            HasFragment = FragmentId > -1;

            LoadModifiers(session, ammo, out AmmoModsFound);
            float shieldBypassRaw;
            GetModifiableValues(ammo.AmmoDef, out BaseDamage, out Health, out GravityMultiplier, out MaxTrajectory, out EnergyBaseDmg, out EnergyAreaDmg, out EnergyDetDmg, out EnergyShieldDmg, out ShieldModifier, out FallOffDistance, out FallOffMinMultiplier, out Mass, out shieldBypassRaw);

            FixedFireAmmo = system.TurretMovement == WeaponSystem.TurretType.Fixed && ammo.AmmoDef.Trajectory.Guidance == None;
            IsMine = ammo.AmmoDef.Trajectory.Guidance == DetectFixed || ammo.AmmoDef.Trajectory.Guidance == DetectSmart || ammo.AmmoDef.Trajectory.Guidance == DetectTravelTo;
            IsField = ammo.AmmoDef.Ewar.Mode == EwarMode.Field || ammo.AmmoDef.Trajectory.DeaccelTime > 0;
            IsHybrid = ammo.AmmoDef.HybridRound;
            IsDrone = ammo.AmmoDef.Trajectory.Guidance == DroneAdvanced;
            IsSmart = ammo.AmmoDef.Trajectory.Guidance == Smart || ammo.AmmoDef.Trajectory.Guidance == DetectSmart;
            IsTurretSelectable = !ammo.IsShrapnel && ammo.AmmoDef.HardPointUsable;

            ProjectileSync = ammo.AmmoDef.Synchronize && session.MpActive && (IsDrone || IsSmart);

            AccelClearance = ammo.AmmoDef.Trajectory.Smarts.AccelClearance;
            OverrideTarget = ammo.AmmoDef.Trajectory.Smarts.OverideTarget;
            RequiresTarget = ammo.AmmoDef.Trajectory.Guidance != None && !OverrideTarget || system.TrackTargets;


            AmmoParticleNoCull = ammo.AmmoDef.AmmoGraphics.Particles.Ammo.DisableCameraCulling;
            HitParticleNoCull = ammo.AmmoDef.AmmoGraphics.Particles.Hit.DisableCameraCulling;
            FieldParticleNoCull = ammo.AmmoDef.Ewar.Field.Particle.DisableCameraCulling;

            AmmoParticle = !string.IsNullOrEmpty(ammo.AmmoDef.AmmoGraphics.Particles.Ammo.Name);
            HitParticle = !string.IsNullOrEmpty(ammo.AmmoDef.AmmoGraphics.Particles.Hit.Name);
            HitParticleStr = ammo.AmmoDef.AmmoGraphics.Particles.Hit.Name;
            EndOfLifeAv = !ammo.AmmoDef.AreaOfDamage.EndOfLife.NoVisuals && ammo.AmmoDef.AreaOfDamage.EndOfLife.Enable;

            DrawLine = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Enable;
            LineColorVariance = ammo.AmmoDef.AmmoGraphics.Lines.ColorVariance.Start > 0 && ammo.AmmoDef.AmmoGraphics.Lines.ColorVariance.End > 0;
            LineWidthVariance = ammo.AmmoDef.AmmoGraphics.Lines.WidthVariance.Start > 0 || ammo.AmmoDef.AmmoGraphics.Lines.WidthVariance.End > 0;
            SegmentColorVariance = TracerMode == Texture.Resize && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.ColorVariance.Start > 0 && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.ColorVariance.End > 0;
            SegmentWidthVariance = TracerMode == Texture.Resize && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.WidthVariance.Start > 0 || ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.WidthVariance.End > 0;

            SegmentStep = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Speed * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            SpeedVariance = ammo.AmmoDef.Trajectory.SpeedVariance.Start > 0 || ammo.AmmoDef.Trajectory.SpeedVariance.End > 0;
            RangeVariance = ammo.AmmoDef.Trajectory.RangeVariance.Start > 0 || ammo.AmmoDef.Trajectory.RangeVariance.End > 0;

            TargetOffSet = ammo.AmmoDef.Trajectory.Smarts.Inaccuracy > 0;
            TargetLossTime = ammo.AmmoDef.Trajectory.TargetLossTime > 0 ? ammo.AmmoDef.Trajectory.TargetLossTime : int.MaxValue;
            CanZombie = TargetLossTime > 0 && TargetLossTime != int.MaxValue && !IsMine;
            MaxLifeTime = ammo.AmmoDef.Trajectory.MaxLifeTime > 0 ? ammo.AmmoDef.Trajectory.MaxLifeTime : int.MaxValue;
            DeltaVelocityPerTick = ammo.AmmoDef.Trajectory.AccelPerSec * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;

            MaxChaseTime = ammo.AmmoDef.Trajectory.Smarts.MaxChaseTime > 0 ? ammo.AmmoDef.Trajectory.Smarts.MaxChaseTime : int.MaxValue;
            MaxObjectsHit = ammo.AmmoDef.ObjectsHit.MaxObjectsHit > 0 ? ammo.AmmoDef.ObjectsHit.MaxObjectsHit : int.MaxValue;
            ArmOnlyOnHit = ammo.AmmoDef.AreaOfDamage.EndOfLife.ArmOnlyOnHit;

            MaxTargets = ammo.AmmoDef.Trajectory.Smarts.MaxTargets;
            TargetLossDegree = ammo.AmmoDef.Trajectory.TargetLossDegree > 0 ? (float)Math.Cos(MathHelper.ToRadians(ammo.AmmoDef.Trajectory.TargetLossDegree)) : 0;
            CheckFutureIntersection = ammo.AmmoDef.Trajectory.Smarts.CheckFutureIntersection;


            Fragments(ammo, out HasFragmentOffset, out HasNegFragmentOffset, out FragmentOffset, out FragRadial, out FragDegrees, out FragReverse, out FragDropVelocity, out FragMaxChildren, out FragIgnoreArming, out FragOnArmed, out FragOnEnd, out HasAdvFragOffset, out FragOffset);
            TimedSpawn(ammo, out TimedFragments, out FragStartTime, out FragInterval, out MaxFrags, out FragGroupSize, out FragGroupDelay, out FragProximity, out HasFragProximity, out FragParentDies, out FragPointAtTarget, out HasFragGroup, out FragPointType, out DirectAimCone);

            FallOffDistance = AmmoModsFound && _modifierMap[FallOffDistanceStr].HasData() ? _modifierMap[FallOffDistanceStr].GetAsFloat : ammo.AmmoDef.DamageScales.FallOff.Distance;

            ArmorCoreActive = session.ArmorCoreActive;

            AmmoSkipAccel = ammo.AmmoDef.Trajectory.AccelPerSec <= 0;
            FeelsGravity = GravityMultiplier > 0;
            SmartOffsetSqr = ammo.AmmoDef.Trajectory.Smarts.Inaccuracy * ammo.AmmoDef.Trajectory.Smarts.Inaccuracy;
            HasBackKickForce = ammo.AmmoDef.BackKickForce > 0;
            MaxLateralThrust = MathHelperD.Clamp(ammo.AmmoDef.Trajectory.Smarts.MaxLateralThrust, 0.01, 1);

            CustomDetParticle = !string.IsNullOrEmpty(ammo.AmmoDef.AreaOfDamage.EndOfLife.CustomParticle);
            DetParticleStr = !string.IsNullOrEmpty(ammo.AmmoDef.AreaOfDamage.EndOfLife.CustomParticle) ? ammo.AmmoDef.AreaOfDamage.EndOfLife.CustomParticle : "Explosion_Missile";
            CustomExplosionSound = !string.IsNullOrEmpty(ammo.AmmoDef.AreaOfDamage.EndOfLife.CustomSound);
            DetSoundStr = CustomExplosionSound ? ammo.AmmoDef.AreaOfDamage.EndOfLife.CustomSound : !ammo.IsShrapnel ? "WepSmallMissileExpl" : string.Empty;
            FieldParticle = !string.IsNullOrEmpty(ammo.AmmoDef.Ewar.Field.Particle.Name);

            Fields(ammo.AmmoDef, out PulseInterval, out PulseChance, out Pulse, out PulseGrowTime);
            AreaEffects(ammo.AmmoDef, out ByBlockHitDepth, out EndOfLifeDepth, out EwarType, out ByBlockHitDamage, out ByBlockHitRadius, out EndOfLifeDamage, out EndOfLifeRadius, out EwarStrength, out LargestHitSize, out EwarRadius, out Ewar, out NonAntiSmartEwar, out EwarTriggerRange, out MinArmingTime, out AoeMaxAbsorb, out DetMaxAbsorb, out EndOfLifeAoe);
            Beams(ammo.AmmoDef, out IsBeamWeapon, out VirtualBeams, out RotateRealBeam, out ConvergeBeams, out OneHitParticle, out OffsetEffect);

            var givenSpeed = AmmoModsFound && _modifierMap[SpeedStr].HasData() ? _modifierMap[SpeedStr].GetAsFloat : ammo.AmmoDef.Trajectory.DesiredSpeed;
            DesiredProjectileSpeed = !IsBeamWeapon ? givenSpeed : MaxTrajectory * MyEngineConstants.UPDATE_STEPS_PER_SECOND;
            HeatModifier = ammo.AmmoDef.HeatModifier > 0 ? ammo.AmmoDef.HeatModifier : 1;

            ComputeShieldBypass(shieldBypassRaw, out ShieldDamageBypassMod);
            ComputeApproaches(ammo, wDef, out ApproachesCount, out Approaches);
            ComputeAmmoPattern(ammo, system, wDef, fragGuidedAmmo, fragAntiSmart, fragTargetOverride, out AntiSmartDetected, out TargetOverrideDetected, out AmmoPattern, out WeaponPatternCount, out FragPatternCount, out GuidedAmmoDetected, out WeaponPattern, out FragmentPattern);

            DamageScales(ammo.AmmoDef, out DamageScaling, out FallOffScaling, out ArmorScaling, out CustomDamageScales, out CustomBlockDefinitionBasesToScales, out SelfDamage, out VoxelDamage, out HealthHitModifier, out VoxelHitModifier, out DeformDelay);
            CollisionShape(ammo.AmmoDef, out CollisionIsLine, out CollisionSize, out TracerLength);
            SmartsDelayDistSqr = (CollisionSize * ammo.AmmoDef.Trajectory.Smarts.TrackingDelay) * (CollisionSize * ammo.AmmoDef.Trajectory.Smarts.TrackingDelay);
            PrimeEntityPool = Models(ammo.AmmoDef, wDef, out PrimeModel, out TriggerModel, out ModelPath);

            Energy(ammo, system, wDef, out EnergyAmmo, out MustCharge, out Reloadable, out EnergyCost, out EnergyMagSize, out ChargSize, out BurstMode, out HasShotReloadDelay, out PowerPerTick);
            Sound(ammo, system, session, out HitSound, out HitSoundPair, out AmmoTravelSound, out TravelSoundPair, out ShotSound, out ShotSoundPair, out DetonationSound, out DetSoundPair, out HitSoundDistSqr, out AmmoTravelSoundDistSqr, out AmmoSoundMaxDistSqr,
                out ShotSoundDistSqr, out DetonationSoundDistSqr, out ShotSoundStr, out VoxelSound, out VoxelSoundPair, out FloatingSound, out FloatingSoundPair, out PlayerSound, out PlayerSoundPair, out ShieldSound, out ShieldSoundPair);

            MagazineSize = EnergyAmmo ? EnergyMagSize : MagazineDef.Capacity;
            MagsToLoad = wDef.HardPoint.Loading.MagsToLoad > 0 ? wDef.HardPoint.Loading.MagsToLoad : 1;
            MaxAmmo = MagsToLoad * MagazineSize;

            GetPeakDps(ammo, system, wDef, out PeakDps, out EffectiveDps, out PerfectDps, out ShotsPerSec, out RealShotsPerSec, out BaseDps, out AreaDps, out DetDps, out RealShotsPerMin);
            var clientPredictedAmmoDisabled = AmmoModsFound && _modifierMap[ClientPredAmmoStr].HasData() && _modifierMap[ClientPredAmmoStr].GetAsBool;
            var predictionEligible = session.IsClient || session.DedicatedServer;


            var predictedShotLimit = system.PartType != HardwareDef.HardwareType.HandWeapon ? 120 : 450;
            var predictedReloadLimit = system.PartType != HardwareDef.HardwareType.HandWeapon ? 120 : 60;

            ClientPredictedAmmo = predictionEligible && FixedFireAmmo && !ammo.IsShrapnel && RealShotsPerMin <= predictedShotLimit && !clientPredictedAmmoDisabled;

            if (!ClientPredictedAmmo && predictionEligible)
                Log.Line($"{ammo.AmmoDef.AmmoRound} is NOT enabled for client prediction");

            SlowFireFixedWeapon = system.TurretMovement == WeaponSystem.TurretType.Fixed && (RealShotsPerMin <= predictedShotLimit || Reloadable && system.WConst.ReloadTime >= predictedReloadLimit);

            if (!SlowFireFixedWeapon && system.TurretMovement == WeaponSystem.TurretType.Fixed && predictionEligible)
                Log.Line($"{ammo.AmmoDef.AmmoRound} does not qualify for fixed weapon client reload verification");

            SkipAimChecks = (ammo.AmmoDef.Trajectory.Guidance == Smart || ammo.AmmoDef.Trajectory.Guidance == DroneAdvanced) && system.TurretMovement == WeaponSystem.TurretType.Fixed;
            Trail = ammo.AmmoDef.AmmoGraphics.Lines.Trail.Enable;
            HasShotFade = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeStart > 0 && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeEnd > 1;
            MaxTrajectoryGrows = ammo.AmmoDef.Trajectory.MaxTrajectoryTime > 1;
            ComputeSteps(ammo, out ShotFadeStep, out TrajectoryStep, out AlwaysDraw);

            TrailWidth = ammo.AmmoDef.AmmoGraphics.Lines.Trail.CustomWidth > 0 ? ammo.AmmoDef.AmmoGraphics.Lines.Trail.CustomWidth : ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Width;
            DecayTime = ammo.AmmoDef.AmmoGraphics.Lines.Trail.DecayTime;
            LongTrail = DecayTime > 20;
            TinyTrail = DecayTime <= 5;
            ShortTrail = !TinyTrail && DecayTime <= 10;
            RareTrail = DecayTime > 0 && ShotsPerSec * 60 <= 6;
            TrailColorFade = ammo.AmmoDef.AmmoGraphics.Lines.Trail.UseColorFade;
            TrailColor = ammo.AmmoDef.AmmoGraphics.Lines.Trail.Color;

            MaxOffset = ammo.AmmoDef.AmmoGraphics.Lines.OffsetEffect.MaxOffset;
            MinOffsetLength = ammo.AmmoDef.AmmoGraphics.Lines.OffsetEffect.MinLength;
            MaxOffsetLength = ammo.AmmoDef.AmmoGraphics.Lines.OffsetEffect.MaxLength;
            CanReportTargetStatus = RequiresTarget && system.TrackGrids && !system.DesignatorWeapon && PeakDps > 0;
            DynamicGuidance = ammo.AmmoDef.Trajectory.Guidance != None && ammo.AmmoDef.Trajectory.Guidance != TravelTo && !IsBeamWeapon;

            if (CollisionSize > 5 && !session.LocalVersion) Log.Line($"{ammo.AmmoDef.AmmoRound} has large largeCollisionSize: {CollisionSize} meters");
            if (FeelsGravity && !IsSmart && system.TrackTargets && (system.Prediction == Prediction.Off || system.Prediction == Prediction.Basic) && ammo.AmmoDef.Trajectory.MaxTrajectory / ammo.AmmoDef.Trajectory.DesiredSpeed > 0.5f)
            {
                var flightTime = ammo.AmmoDef.Trajectory.MaxTrajectory / ammo.AmmoDef.Trajectory.DesiredSpeed;
                Log.Line($"{ammo.AmmoDef.AmmoRound} has {(int)(0.5 * 9.8 * flightTime * flightTime)}m grav drop at 1g.  {system.PartName} needs Accurate/Advanced aim prediction to account for gravity.");
            }
        }

        internal void Purge()
        {
            if (AmmoPattern != null)
            {
                for (int i = 0; i < AmmoPattern.Length; i++)
                    AmmoPattern[i] = null;
            }

            if (PatternShuffleArray != null)
            {
                for (int i = 0; i < PatternShuffleArray.Count; i++)
                    PatternShuffleArray.Pop();
            }


            CustomBlockDefinitionBasesToScales?.Clear();
            PrimeEntityPool?.Clean();
            _modifierMap.Clear();

            if (Approaches != null)
            {
                for (int i = 0; i < Approaches.Length; i++)
                {
                    var a = Approaches[i];
                    a.Clean();
                    Approaches[i] = null;
                }
            }
        }

        internal void ComputeShieldBypass(float shieldBypassRaw, out float shieldDamageBypassMod)
        {
            if (shieldBypassRaw <= 0)
                shieldDamageBypassMod = 0;
            else if (shieldBypassRaw >= 1)
                shieldDamageBypassMod = 0.00001f;
            else
                shieldDamageBypassMod = MathHelper.Clamp(1 - shieldBypassRaw, 0.00001f, 0.99999f);
        }

        internal void ComputeTextures(WeaponSystem.AmmoType ammo, out MyStringId[] tracerTextures, out MyStringId[] segmentTextures, out MyStringId[] trailTextures, out Texture tracerTexture, out Texture trailTexture)
        {
            var lineSegments = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Enable && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.SegmentLength > 0;

            if (lineSegments)
                tracerTexture = Texture.Resize;
            else if (ammo.AmmoDef.AmmoGraphics.Lines.Tracer.TextureMode == AmmoDef.GraphicDef.LineDef.Texture.Normal)
                tracerTexture = Texture.Normal;
            else if (ammo.AmmoDef.AmmoGraphics.Lines.Tracer.TextureMode == AmmoDef.GraphicDef.LineDef.Texture.Cycle)
                tracerTexture = Texture.Cycle;
            else if (ammo.AmmoDef.AmmoGraphics.Lines.Tracer.TextureMode == AmmoDef.GraphicDef.LineDef.Texture.Wave)
                tracerTexture = Texture.Wave;
            else tracerTexture = Texture.Chaos;
            trailTexture = (Texture)ammo.AmmoDef.AmmoGraphics.Lines.Trail.TextureMode;

            if (ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Textures != null && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Textures.Length > 0)
            {
                tracerTextures = new MyStringId[ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Textures.Length];
                for (int i = 0; i < ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Textures.Length; i++)
                {
                    var value = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Textures[i];
                    if (string.IsNullOrEmpty(value))
                        value = ammo.AmmoDef.AmmoGraphics.Lines.TracerMaterial;
                    tracerTextures[i] = MyStringId.GetOrCompute(value);
                }
            }
            else tracerTextures = new[] { MyStringId.GetOrCompute(ammo.AmmoDef.AmmoGraphics.Lines.TracerMaterial) };

            if (ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Textures != null && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Textures.Length > 0)
            {
                segmentTextures = new MyStringId[ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Textures.Length];
                for (int i = 0; i < ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Textures.Length; i++)
                {
                    var value = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Textures[i];
                    if (string.IsNullOrEmpty(value))
                        value = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Material;
                    segmentTextures[i] = MyStringId.GetOrCompute(value);
                }
            }
            else segmentTextures = new[] { MyStringId.GetOrCompute(ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Material) };

            if (ammo.AmmoDef.AmmoGraphics.Lines.Trail.Textures != null && ammo.AmmoDef.AmmoGraphics.Lines.Trail.Textures.Length > 0)
            {
                trailTextures = new MyStringId[ammo.AmmoDef.AmmoGraphics.Lines.Trail.Textures.Length];
                for (int i = 0; i < ammo.AmmoDef.AmmoGraphics.Lines.Trail.Textures.Length; i++)
                {
                    var value = ammo.AmmoDef.AmmoGraphics.Lines.Trail.Textures[i];
                    if (string.IsNullOrEmpty(value))
                        value = ammo.AmmoDef.AmmoGraphics.Lines.Trail.Material;
                    trailTextures[i] = MyStringId.GetOrCompute(value);
                }
            }
            else trailTextures = new[] { MyStringId.GetOrCompute(ammo.AmmoDef.AmmoGraphics.Lines.Trail.Material) };

            if (ammo.AmmoDef.AmmoGraphics.Decals.Map != null)
            {
                foreach (var textureMapDef in ammo.AmmoDef.AmmoGraphics.Decals.Map)
                    TextureHitMap[MyStringHash.GetOrCompute(textureMapDef.HitMaterial)] = MyStringHash.GetOrCompute(textureMapDef.DecalMaterial);
            }
        }




        private void ComputeSteps(WeaponSystem.AmmoType ammo, out float shotFadeStep, out float trajectoryStep, out bool alwaysDraw)
        {
            var changeFadeSteps = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeEnd - ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeStart;
            shotFadeStep = 1f / changeFadeSteps;

            trajectoryStep = MaxTrajectoryGrows ? MaxTrajectory / ammo.AmmoDef.Trajectory.MaxTrajectoryTime : MaxTrajectory;
            alwaysDraw = (Trail || HasShotFade) && RealShotsPerSec < 0.1;
        }

        private void Fragments(WeaponSystem.AmmoType ammo, out bool hasFragmentOffset, out bool hasNegFragmentOffset, out float fragmentOffset, out float fragRadial, out float fragDegrees, out bool fragReverse, out bool fragDropVelocity, out int fragMaxChildren, out bool fragIgnoreArming, out bool fragOnArmed, out bool fragOnEnd, out bool hasFragOffset, out Vector3D fragOffset)
        {
            hasFragmentOffset = !MyUtils.IsZero(ammo.AmmoDef.Fragment.Offset);
            hasNegFragmentOffset = ammo.AmmoDef.Fragment.Offset < 0;
            fragmentOffset = Math.Abs(ammo.AmmoDef.Fragment.Offset);
            fragRadial = MathHelper.ToRadians(MathHelper.Clamp(ammo.AmmoDef.Fragment.Radial, 0, 360));
            fragDegrees = MathHelper.ToRadians(MathHelper.Clamp(ammo.AmmoDef.Fragment.Degrees, 0, 360));
            fragReverse = ammo.AmmoDef.Fragment.Reverse;
            fragDropVelocity = ammo.AmmoDef.Fragment.DropVelocity;
            fragMaxChildren = ammo.AmmoDef.Fragment.MaxChildren > 0 ? ammo.AmmoDef.Fragment.MaxChildren : int.MaxValue;
            fragIgnoreArming = ammo.AmmoDef.Fragment.IgnoreArming;
            fragOnArmed = ammo.AmmoDef.AreaOfDamage.EndOfLife.Enable && ArmOnlyOnHit && !FragIgnoreArming && HasFragment;
            fragOnEnd = !FragOnArmed && !ammo.AmmoDef.Fragment.TimedSpawns.Enable && HasFragment;
            hasFragOffset = !Vector3D.IsZero(ammo.AmmoDef.Fragment.AdvOffset);
            fragOffset = ammo.AmmoDef.Fragment.AdvOffset;
        }

        private void TimedSpawn(WeaponSystem.AmmoType ammo, out bool timedFragments, out int startTime, out int interval, out int maxSpawns, out int groupSize, out int groupDelay, out double proximity, out bool hasProximity, out bool parentDies, out bool pointAtTarget, out bool hasGroup, out PointTypes pointType, out float directAimCone)
        {
            timedFragments = ammo.AmmoDef.Fragment.TimedSpawns.Enable && HasFragment;
            startTime = ammo.AmmoDef.Fragment.TimedSpawns.StartTime;
            interval = ammo.AmmoDef.Fragment.TimedSpawns.Interval;
            maxSpawns = ammo.AmmoDef.Fragment.TimedSpawns.MaxSpawns;
            proximity = ammo.AmmoDef.Fragment.TimedSpawns.Proximity;
            hasProximity = proximity > 0;
            parentDies = ammo.AmmoDef.Fragment.TimedSpawns.ParentDies;
            pointAtTarget = ammo.AmmoDef.Fragment.TimedSpawns.PointAtTarget;
            groupSize = ammo.AmmoDef.Fragment.TimedSpawns.GroupSize;
            groupDelay = ammo.AmmoDef.Fragment.TimedSpawns.GroupDelay;
            hasGroup = groupSize > 0 && groupDelay > 0;
            pointType = ammo.AmmoDef.Fragment.TimedSpawns.PointType;
            directAimCone = MathHelper.ToRadians(Math.Max(ammo.AmmoDef.Fragment.TimedSpawns.DirectAimCone,1));
        }

        private void ComputeApproaches(WeaponSystem.AmmoType ammo, WeaponDefinition wDef, out int approachesCount, out ApproachConstants[] approaches)
        {
            approachesCount = ammo.AmmoDef.Trajectory.Approaches?.Length ?? 0;

            approaches = approachesCount > 0 ? new ApproachConstants[approachesCount] : null;

            if (approaches != null && ammo.AmmoDef.Trajectory.Approaches != null)
            {
                for (int i = 0; i < approaches.Length; i++)
                {
                    approaches[i] = new ApproachConstants(ammo, i, wDef);
                }
            }

        }

        private void ComputeAmmoPattern(WeaponSystem.AmmoType ammo, WeaponSystem system, WeaponDefinition wDef, bool fragGuidedAmmo, bool fragAntiSmart, bool fragTargetOverride, out bool hasAntiSmart, out bool hasTargetOverride, out AmmoDef[] ammoPattern, out int weaponPatternCount, out int fragmentPatternCount, out bool hasGuidedAmmo, out bool weaponPattern, out bool fragmentPattern)
        {
            var pattern = ammo.AmmoDef.Pattern;
            var indexPos = 0;
            int indexCount;

            weaponPattern = pattern.Enable || pattern.Mode == AmmoDef.PatternDef.PatternModes.Both || pattern.Mode == AmmoDef.PatternDef.PatternModes.Weapon;
            fragmentPattern = pattern.Mode == AmmoDef.PatternDef.PatternModes.Both || pattern.Mode == AmmoDef.PatternDef.PatternModes.Fragment;
            var enabled = weaponPattern || fragmentPattern;

            if (!weaponPattern && !fragmentPattern)
                indexCount = 1;
            else
            {
                indexCount = pattern.Patterns.Length;
                if (!pattern.SkipParent) indexCount += 1;
            }

            weaponPatternCount = weaponPattern ? indexCount : 1;

            fragmentPatternCount = fragmentPattern ? indexCount : 1;
            if (!pattern.SkipParent && fragmentPattern) fragmentPatternCount--;
            ammoPattern = new AmmoDef[indexCount];

            if (!pattern.SkipParent && pattern.Mode != AmmoDef.PatternDef.PatternModes.Fragment)
                ammoPattern[indexPos++] = ammo.AmmoDef;

            var validPatterns = 0;
            var patternTargetOverride = false;
            var patternGuidedAmmo = false;
            var patternAntiSmart = false;

            if (enabled)
            {
                for (int j = 0; j < ammo.AmmoDef.Pattern.Patterns.Length; j++)
                {
                    var aPattern = ammo.AmmoDef.Pattern.Patterns[j];
                    if (string.IsNullOrEmpty(aPattern))
                        continue;

                    ++validPatterns;

                    for (int i = 0; i < wDef.Ammos.Length; i++)
                    {
                        var ammoDef = wDef.Ammos[i];
                        if (aPattern.Equals(ammoDef.AmmoRound))
                        {
                            
                            ammoPattern[indexPos++] = ammoDef;
                            var hasGuidance = ammoDef.Trajectory.Guidance != None;
                            if (!patternGuidedAmmo && hasGuidance)
                                patternGuidedAmmo = true;

                            if (!patternAntiSmart && ammoDef.Ewar.Type == EwarType.AntiSmart)
                                patternAntiSmart = true;
                            if (hasGuidance && ammoDef.Trajectory.Smarts.OverideTarget)
                                patternTargetOverride = true;
                        }
                    }
                }
            }

            if (validPatterns == 0) {
                weaponPattern = false;
                fragmentPattern = false;
            }

            hasGuidedAmmo = fragGuidedAmmo || patternGuidedAmmo || ammo.AmmoDef.Trajectory.Guidance != None;
            hasAntiSmart = fragAntiSmart || patternAntiSmart || ammo.AmmoDef.Ewar.Type == EwarType.AntiSmart;
            hasTargetOverride = fragTargetOverride || patternTargetOverride || OverrideTarget;
        }

        private void Fields(AmmoDef ammoDef, out int pulseInterval, out int pulseChance, out bool pulse, out int growTime)
        {
            pulseInterval = ammoDef.Ewar.Mode != EwarMode.Effect ? ammoDef.Ewar.Field.Interval : 0;
            growTime = ammoDef.Ewar.Field.GrowTime == 0 ? 60 : ammoDef.Ewar.Field.GrowTime;
            pulseChance = ammoDef.Ewar.Mode != EwarMode.Effect ? ammoDef.Ewar.Field.PulseChance : 0;
            pulse = ammoDef.Ewar.Mode != EwarMode.Effect && pulseInterval > 0 && pulseChance > 0 && !ammoDef.Beams.Enable;
        }

        private void AreaEffects(AmmoDef ammoDef, out float byBlockHitDepth, out float endOfLifeDepth, out EwarType ewarType, out float byBlockHitDamage, out double byBlockHitRadius, out float endOfLifeDamage, out float endOfLifeRadius, out double ewarEffectStrength, out double largestHitSize, out double ewarEffectSize, out bool eWar, out bool nonAntiSmart, out double eWarTriggerRange, out int minArmingTime, out float aoeMaxAbsorb, out float detMaxAbsorb, out bool endOfLifeAoe)
        {
            ewarType = ammoDef.Ewar.Type;

            if (AmmoModsFound && _modifierMap[AreaDmgStr].HasData())
                byBlockHitDamage = _modifierMap[AreaDmgStr].GetAsFloat;
            else
                byBlockHitDamage = ammoDef.AreaOfDamage.ByBlockHit.Damage;

            if (AmmoModsFound && _modifierMap[AreaRadStr].HasData())
                byBlockHitRadius = _modifierMap[AreaRadStr].GetAsDouble;
            else
                byBlockHitRadius = ammoDef.AreaOfDamage.ByBlockHit.Enable ? ammoDef.AreaOfDamage.ByBlockHit.Radius : 0;

            if (AmmoModsFound && _modifierMap[DetDmgStr].HasData())
                endOfLifeDamage = _modifierMap[DetDmgStr].GetAsFloat;
            else
                endOfLifeDamage = ammoDef.AreaOfDamage.EndOfLife.Damage;

            if (AmmoModsFound && _modifierMap[DetRadStr].HasData())
                endOfLifeRadius = _modifierMap[DetRadStr].GetAsFloat;
            else
                endOfLifeRadius = ammoDef.AreaOfDamage.EndOfLife.Enable ? (float)ammoDef.AreaOfDamage.EndOfLife.Radius : 0;

            if (AmmoModsFound && _modifierMap[ByBlockHitMaxAbsorbStr].HasData())
                aoeMaxAbsorb = _modifierMap[ByBlockHitMaxAbsorbStr].GetAsFloat;
            else
                aoeMaxAbsorb = ammoDef.AreaOfDamage.ByBlockHit.MaxAbsorb > 0 ? ammoDef.AreaOfDamage.ByBlockHit.MaxAbsorb : 0;

            if (AmmoModsFound && _modifierMap[EndOfLifeMaxAbsorbStr].HasData())
                detMaxAbsorb = _modifierMap[EndOfLifeMaxAbsorbStr].GetAsFloat;
            else
                detMaxAbsorb = ammoDef.AreaOfDamage.EndOfLife.MaxAbsorb > 0 ? ammoDef.AreaOfDamage.EndOfLife.MaxAbsorb : 0;

            ewarEffectStrength = ammoDef.Ewar.Strength;
            ewarEffectSize = ammoDef.Ewar.Radius;
            largestHitSize = Math.Max(byBlockHitRadius, Math.Max(endOfLifeRadius, ewarEffectSize));

            eWar = ammoDef.Ewar.Enable;
            nonAntiSmart = !eWar || ewarType != EwarType.AntiSmart;
            eWarTriggerRange = eWar && Pulse && ammoDef.Ewar.Field.TriggerRange > 0 ? ammoDef.Ewar.Field.TriggerRange : 0;
            minArmingTime = ammoDef.AreaOfDamage.EndOfLife.MinArmingTime;
            if (ammoDef.AreaOfDamage.ByBlockHit.Enable) byBlockHitDepth = ammoDef.AreaOfDamage.ByBlockHit.Depth <= 0 ? (float)ammoDef.AreaOfDamage.ByBlockHit.Radius : ammoDef.AreaOfDamage.ByBlockHit.Depth;
            else byBlockHitDepth = 0;
            if (ammoDef.AreaOfDamage.EndOfLife.Enable) endOfLifeDepth = ammoDef.AreaOfDamage.EndOfLife.Depth <= 0 ? (float)ammoDef.AreaOfDamage.EndOfLife.Radius : ammoDef.AreaOfDamage.EndOfLife.Depth;
            else endOfLifeDepth = 0;

            //aoeMaxAbsorb = ammoDef.AreaOfDamage.ByBlockHit.MaxAbsorb > 0? ammoDef.AreaOfDamage.ByBlockHit.MaxAbsorb : 0;
            //detMaxAbsorb = ammoDef.AreaOfDamage.EndOfLife.MaxAbsorb > 0? ammoDef.AreaOfDamage.EndOfLife.MaxAbsorb : 0;

            endOfLifeAoe = ammoDef.AreaOfDamage.EndOfLife.Enable;
        }

        private MyConcurrentPool<MyEntity> Models(AmmoDef ammoDef, WeaponDefinition wDef, out bool primeModel, out bool triggerModel, out string primeModelPath)
        {
            if (ammoDef.Ewar.Type > 0 && IsField) triggerModel = true;
            else triggerModel = false;
            primeModel = ammoDef.AmmoGraphics.ModelName != string.Empty;
            var vanillaModel = !ammoDef.AmmoGraphics.ModelName.StartsWith(BackSlash);
            primeModelPath = primeModel ? vanillaModel ? ammoDef.AmmoGraphics.ModelName : wDef.ModPath + ammoDef.AmmoGraphics.ModelName : string.Empty;
            return primeModel ? new MyConcurrentPool<MyEntity>(64, PrimeEntityClear, 6400, PrimeEntityActivator) : null;
        }

        private void Beams(AmmoDef ammoDef, out bool isBeamWeapon, out bool virtualBeams, out bool rotateRealBeam, out bool convergeBeams, out bool oneHitParticle, out bool offsetEffect)
        {
            isBeamWeapon = ammoDef.Beams.Enable && ammoDef.Trajectory.Guidance == None;
            virtualBeams = ammoDef.Beams.VirtualBeams && IsBeamWeapon;
            rotateRealBeam = ammoDef.Beams.RotateRealBeam && VirtualBeams;
            convergeBeams = !RotateRealBeam && ammoDef.Beams.ConvergeBeams && VirtualBeams;
            oneHitParticle = ammoDef.Beams.OneParticle && IsBeamWeapon && VirtualBeams;
            offsetEffect = ammoDef.AmmoGraphics.Lines.OffsetEffect.MaxOffset > 0;
        }

        private void CollisionShape(AmmoDef ammoDef, out bool collisionIsLine, out double collisionSize, out double tracerLength)
        {
            var isLine = ammoDef.Shape.Shape == LineShape;
            var size = ammoDef.Shape.Diameter;

            if (IsBeamWeapon)
                tracerLength = MaxTrajectory;
            else tracerLength = ammoDef.AmmoGraphics.Lines.Tracer.Length > 0 ? ammoDef.AmmoGraphics.Lines.Tracer.Length : 0.1;

            if (size <= 0)
            {
                if (!isLine) isLine = true;
                size = 1;
            }
            else if (!isLine) size *= 0.5;
            collisionIsLine = isLine;
            collisionSize = size;
        }

        private void DamageScales(AmmoDef ammoDef, out bool damageScaling, out bool fallOffScaling, out bool armorScaling, out bool customDamageScales, out Dictionary<MyDefinitionBase, float> customBlockDef, out bool selfDamage, out bool voxelDamage, out double healthHitModifer, out double voxelHitModifer, out int deformDelay)
        {
            armorScaling = false;
            customDamageScales = false;
            fallOffScaling = false;
            var d = ammoDef.DamageScales;
            customBlockDef = null;
            if (d.Custom.Types != null && d.Custom.Types.Length > 0)
            {
                foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
                    foreach (var customDef in d.Custom.Types)
                        if (customDef.Modifier >= 0 && def.Id.SubtypeId.String == customDef.SubTypeId)
                        {
                            if (customBlockDef == null) customBlockDef = new Dictionary<MyDefinitionBase, float>();
                            customBlockDef.Add(def, customDef.Modifier);
                            customDamageScales = customBlockDef.Count > 0;
                        }
            }

            damageScaling = FallOffMinMultiplier > 0 && !MyUtils.IsZero(FallOffMinMultiplier - 1) || d.MaxIntegrity > 0 || d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0 || d.Grids.Large >= 0 || d.Grids.Small >= 0 || customDamageScales || ArmorCoreActive;

            if (damageScaling)
            {
                armorScaling = d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0;
                fallOffScaling = FallOffMinMultiplier > 0 && !MyUtils.IsZero(FallOffMinMultiplier - 1);
            }
            selfDamage = d.SelfDamage;
            voxelDamage = d.DamageVoxels;
            var healthHitModiferRaw = AmmoModsFound && _modifierMap[HealthHitModStr].HasData() ? _modifierMap[HealthHitModStr].GetAsDouble : d.HealthHitModifier;
            healthHitModifer = healthHitModiferRaw > 0 ? healthHitModiferRaw : 1;
            voxelHitModifer = d.VoxelHitModifier > 0 ? d.VoxelHitModifier : 1;

            deformDelay = d.Deform.DeformDelay <= 0 ? 30 : d.Deform.DeformDelay;
        }

        private void Energy(WeaponSystem.AmmoType ammoPair, WeaponSystem system, WeaponDefinition wDef, out bool energyAmmo, out bool mustCharge, out bool reloadable, out float energyCost, out int energyMagSize, out float chargeSize, out bool burstMode, out bool shotReload, out float requiredPowerPerTick)
        {
            energyAmmo = ammoPair.AmmoDefinitionId.SubtypeId.String == "Energy" || ammoPair.AmmoDefinitionId.SubtypeId.String == string.Empty;
            mustCharge = (energyAmmo || IsHybrid);

            burstMode = wDef.HardPoint.Loading.ShotsInBurst > 0 && (energyAmmo || MagazineDef.Capacity >= wDef.HardPoint.Loading.ShotsInBurst);

            reloadable = !energyAmmo || mustCharge && system.WConst.ReloadTime > 0;

            shotReload = !burstMode && wDef.HardPoint.Loading.ShotsInBurst > 0 && wDef.HardPoint.Loading.DelayAfterBurst > 0;

            if (mustCharge)
            {
                var ewar = ammoPair.AmmoDef.Ewar.Enable;
                energyCost = AmmoModsFound && _modifierMap[EnergyCostStr].HasData() ? _modifierMap[EnergyCostStr].GetAsFloat : ammoPair.AmmoDef.EnergyCost;
                var shotEnergyCost = ewar ? energyCost * ammoPair.AmmoDef.Ewar.Strength : energyCost * BaseDamage;
                var shotsPerTick = system.WConst.RateOfFire / MyEngineConstants.UPDATE_STEPS_PER_MINUTE;
                var energyPerTick = shotEnergyCost * shotsPerTick;
                requiredPowerPerTick = (energyPerTick * wDef.HardPoint.Loading.BarrelsPerShot) * wDef.HardPoint.Loading.TrajectilesPerBarrel;

                var reloadTime = system.WConst.ReloadTime > 0 ? system.WConst.ReloadTime : 1;
                chargeSize = requiredPowerPerTick * reloadTime;
                var chargeCeil = (int)Math.Ceiling(requiredPowerPerTick * reloadTime);

                energyMagSize = ammoPair.AmmoDef.EnergyMagazineSize > 0 ? ammoPair.AmmoDef.EnergyMagazineSize : chargeCeil;
                return;
            }
            energyCost = 0;
            chargeSize = 0;
            energyMagSize = 0;
            requiredPowerPerTick = 0;
        }

        private void Sound(WeaponSystem.AmmoType ammo, WeaponSystem system, Session session, out bool hitSound, out MySoundPair hitSoundPair, out bool ammoTravelSound, out MySoundPair travelSoundPair, out bool shotSound, out MySoundPair shotSoundPair, 
            out bool detSound, out MySoundPair detSoundPair, out float hitSoundDistSqr, out float ammoTravelSoundDistSqr, out float ammoSoundMaxDistSqr, out float shotSoundDistSqr, out float detSoundDistSqr, out string rawShotSoundStr, 
            out bool voxelSound, out MySoundPair voxelSoundPair, out bool floatingSound, out MySoundPair floatingSoundPair, out bool playerSound, out MySoundPair playerSoundPair, out bool shieldSound, out MySoundPair shieldSoundPair)
        {
            var ammoDef = ammo.AmmoDef;
            var weaponShotSound = !string.IsNullOrEmpty(system.Values.HardPoint.Audio.FiringSound);
            var ammoShotSound = !string.IsNullOrEmpty(ammoDef.AmmoAudio.ShotSound);
            var useWeaponShotSound = !ammo.IsShrapnel && weaponShotSound && !ammoShotSound;


            rawShotSoundStr = useWeaponShotSound ? system.Values.HardPoint.Audio.FiringSound : ammoDef.AmmoAudio.ShotSound;

            hitSound = !string.IsNullOrEmpty(ammoDef.AmmoAudio.HitSound);
            hitSoundPair = hitSound ? new MySoundPair(ammoDef.AmmoAudio.HitSound, false) : null;


            ammoTravelSound = !string.IsNullOrEmpty(ammoDef.AmmoAudio.TravelSound);
            travelSoundPair = ammoTravelSound ? new MySoundPair(ammoDef.AmmoAudio.TravelSound, false) : null;
            
            shotSound = !string.IsNullOrEmpty(rawShotSoundStr);
            shotSoundPair = shotSound ? new MySoundPair(rawShotSoundStr, false) : null;

            detSound = !string.IsNullOrEmpty(DetSoundStr) && !ammoDef.AreaOfDamage.EndOfLife.NoSound;
            detSoundPair = detSound ? new MySoundPair(DetSoundStr, false) : null;


            var hitSoundStr = string.Concat(Arc, ammoDef.AmmoAudio.HitSound);
            var travelSoundStr = string.Concat(Arc, ammoDef.AmmoAudio.TravelSound);
            var shotSoundStr = string.Concat(Arc, rawShotSoundStr);
            var detSoundStr = string.Concat(Arc, DetSoundStr);

            hitSoundDistSqr = 0;
            ammoTravelSoundDistSqr = 0;
            ammoSoundMaxDistSqr = 0;
            shotSoundDistSqr = 0;
            detSoundDistSqr = 0;

            foreach (var def in session.SoundDefinitions)
            {
                var id = def.Id.SubtypeId.String;
                if (hitSound && (id == hitSoundStr || id == ammoDef.AmmoAudio.HitSound))
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) hitSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (hitSoundDistSqr > ammoSoundMaxDistSqr) ammoSoundMaxDistSqr = hitSoundDistSqr;
                }
                else if (ammoTravelSound && (id == travelSoundStr || id == ammoDef.AmmoAudio.TravelSound))
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) ammoTravelSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (ammoTravelSoundDistSqr > ammoSoundMaxDistSqr) ammoSoundMaxDistSqr = ammoTravelSoundDistSqr;
                }
                else if (shotSound && (id == shotSoundStr || id == rawShotSoundStr))
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) shotSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (shotSoundDistSqr > ammoSoundMaxDistSqr) ammoSoundMaxDistSqr = shotSoundDistSqr;
                }
                else if (detSound && (id == detSoundStr || id == DetSoundStr))
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) detSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (detSoundDistSqr > ammoSoundMaxDistSqr) ammoSoundMaxDistSqr = detSoundDistSqr;
                }
            }

            voxelSound = !string.IsNullOrEmpty(ammoDef.AmmoAudio.VoxelHitSound);
            voxelSoundPair = voxelSound ? new MySoundPair(ammoDef.AmmoAudio.VoxelHitSound, false) : null;

            playerSound = !string.IsNullOrEmpty(ammoDef.AmmoAudio.PlayerHitSound);
            playerSoundPair = playerSound ? new MySoundPair(ammoDef.AmmoAudio.PlayerHitSound, false) : null;

            floatingSound = !string.IsNullOrEmpty(ammoDef.AmmoAudio.FloatingHitSound);
            floatingSoundPair = floatingSound ? new MySoundPair(ammoDef.AmmoAudio.FloatingHitSound, false) : null;

            shieldSound = !string.IsNullOrEmpty(ammoDef.AmmoAudio.ShieldHitSound);
            shieldSoundPair = shieldSound ? new MySoundPair(ammoDef.AmmoAudio.ShieldHitSound, false) : null;
        }

        private MyEntity PrimeEntityActivator()
        {
            var ent = new MyEntity();
            ent.Init(null, ModelPath, null, null);
            ent.Render.CastShadows = false;
            ent.IsPreview = true;
            ent.Save = false;
            ent.SyncFlag = false;
            ent.NeedsWorldMatrix = false;
            ent.Flags |= EntityFlags.IsNotGamePrunningStructureObject;
            MyEntities.Add(ent, false);
            return ent;
        }

        private static void PrimeEntityClear(MyEntity myEntity)
        {
            myEntity.PositionComp.SetWorldMatrix(ref MatrixD.Identity, null, false, false, false);
            myEntity.InScene = false;
            myEntity.Render.RemoveRenderObjects();
        }

        private void LoadModifiers(Session session, WeaponSystem.AmmoType ammo, out bool modsFound)
        {
            modsFound = false;
            Dictionary<string, string> ammoMods;
            if (session.AmmoValuesMap.TryGetValue(ammo.AmmoDef, out ammoMods) && ammoMods != null)
            {
                foreach (var mod in ammoMods)
                {
                    BaseProcessor processor;
                    if (_modifierMap.TryGetValue(mod.Key, out processor))
                        processor.WriteData(mod.Value);
                }
                modsFound = true;
            }
        }

        private void GetModifiableValues(AmmoDef ammoDef, out float baseDamage, out float health, out float gravityMultiplier, out float maxTrajectory, out bool energyBaseDmg, out bool energyAreaDmg, out bool energyDetDmg, out bool energyShieldDmg, out double shieldModifier, out float fallOffDistance, out float fallOffMinMult, out float mass, out float shieldBypassRaw)
        {
            baseDamage = AmmoModsFound && _modifierMap[BaseDmgStr].HasData() ? _modifierMap[BaseDmgStr].GetAsFloat : ammoDef.BaseDamage;

            if (baseDamage < 0.000001)
                baseDamage = 0.000001f;

            health = AmmoModsFound && _modifierMap[HealthStr].HasData() ? _modifierMap[HealthStr].GetAsFloat : ammoDef.Health;
            gravityMultiplier = AmmoModsFound && _modifierMap[GravityStr].HasData() ? _modifierMap[GravityStr].GetAsFloat : ammoDef.Trajectory.GravityMultiplier;
            maxTrajectory = AmmoModsFound && _modifierMap[MaxTrajStr].HasData() ? _modifierMap[MaxTrajStr].GetAsFloat : ammoDef.Trajectory.MaxTrajectory;

            energyBaseDmg = AmmoModsFound && _modifierMap[EnergyBaseDmgStr].HasData() ? _modifierMap[EnergyBaseDmgStr].GetAsBool : ammoDef.DamageScales.DamageType.Base != DamageTypes.Damage.Kinetic;
            energyAreaDmg = AmmoModsFound && _modifierMap[EnergyAreaDmgStr].HasData() ? _modifierMap[EnergyAreaDmgStr].GetAsBool : ammoDef.DamageScales.DamageType.AreaEffect != DamageTypes.Damage.Kinetic;
            energyDetDmg = AmmoModsFound && _modifierMap[EnergyDetDmgStr].HasData() ? _modifierMap[EnergyDetDmgStr].GetAsBool : ammoDef.DamageScales.DamageType.Detonation != DamageTypes.Damage.Kinetic;
            energyShieldDmg = AmmoModsFound && _modifierMap[EnergyShieldDmgStr].HasData() ? _modifierMap[EnergyShieldDmgStr].GetAsBool : ammoDef.DamageScales.DamageType.Shield != DamageTypes.Damage.Kinetic;

            var givenShieldModifier = AmmoModsFound && _modifierMap[ShieldModStr].HasData() ? _modifierMap[ShieldModStr].GetAsDouble : ammoDef.DamageScales.Shields.Modifier;
            shieldModifier = givenShieldModifier < 0 ? 1 : givenShieldModifier;

            fallOffDistance = AmmoModsFound && _modifierMap[FallOffDistanceStr].HasData() ? _modifierMap[FallOffDistanceStr].GetAsFloat : ammoDef.DamageScales.FallOff.Distance;
            fallOffMinMult = AmmoModsFound && _modifierMap[FallOffMinMultStr].HasData() ? _modifierMap[FallOffMinMultStr].GetAsFloat : ammoDef.DamageScales.FallOff.MinMultipler;

            mass = AmmoModsFound && _modifierMap[MassStr].HasData() ? _modifierMap[MassStr].GetAsFloat : ammoDef.Mass;

            shieldBypassRaw = AmmoModsFound && _modifierMap[ShieldBypassStr].HasData() ? _modifierMap[ShieldBypassStr].GetAsFloat : ammoDef.DamageScales.Shields.BypassModifier;
        }


        internal void GetParticleInfo(WeaponSystem.AmmoType ammo, WeaponDefinition wDef, Session session)
        {
            var list = MyDefinitionManager.Static.GetAllSessionPreloadObjectBuilders();
            var comparer = new Session.HackEqualityComparer();
            for (int i = 0; i < list.Count; i++)
            {
                var tuple = (IStructuralEquatable)list[i];
                if (tuple != null)
                {
                    tuple.GetHashCode(comparer);
                    var hacked = comparer.Def;
                    if (hacked != null)
                    {
                        if (hacked.ParticleEffects != null)
                        {
                            foreach (var particle in hacked.ParticleEffects)
                            {
                                if (particle.Id.SubtypeId.Contains("Spark"))
                                    Log.Line($"test: {particle.Id.SubtypeId} - {ammo.AmmoDef.AmmoGraphics.Particles.Hit.Name}");
                            }
                        }
                    }
                }
            }
        }

        private int mexLogLevel = 0;
        private void GetPeakDps(WeaponSystem.AmmoType ammoDef, WeaponSystem system, WeaponDefinition wDef, out float peakDps, out float effectiveDps, out float dpsWoInaccuracy, out float shotsPerSec, out float realShotsPerSec, out float baseDps, out float areaDps, out float detDps, out float realShotsPerMin)
        {
            var s = system;
            var a = ammoDef.AmmoDef;
            var hasShrapnel = HasFragment;
            var l = wDef.HardPoint.Loading;


            if (mexLogLevel >= 1) Log.Line("-----");
            if (mexLogLevel >= 1) Log.Line($"Name = {s.PartName}"); //a.EnergyMagazineSize
            if (mexLogLevel >= 2) Log.Line($"EnergyMag = {a.EnergyMagazineSize}");

            var baselineRange = a.Trajectory.MaxTrajectory * 0.5f; // 1000; ba

            //Inaccuracy
            var inaccuracyRadius = Math.Tan(system.WConst.DeviateShotAngleRads) * baselineRange;
            var targetRadius = 10;
            var inaccuracyScore = ((Math.PI * targetRadius * targetRadius) / (Math.PI * inaccuracyRadius * inaccuracyRadius));
            inaccuracyScore = inaccuracyScore > 1 ? 1 : inaccuracyScore;
            inaccuracyScore = system.WConst.DeviateShotAngleRads <= 0 ? 1 : inaccuracyScore;


            //EffectiveRange
            var effectiveRangeScore = 1 / (baselineRange / DesiredProjectileSpeed);
            effectiveRangeScore = effectiveRangeScore > 1 ? 1 : effectiveRangeScore;
            effectiveRangeScore = a.Beams.Enable ? 1 : effectiveRangeScore;
            effectiveRangeScore = 1;


            //TrackingScore
            var coverageScore = ((Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation)) * ((Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth)))) / (360 * 90);
            coverageScore = coverageScore > 1 ? 1 : coverageScore;

            var speedEl = (wDef.HardPoint.HardWare.ElevateRate * (180 / Math.PI)) * 60;
            var coverageElevateScore = speedEl / (180d / 5d);
            var speedAz = (wDef.HardPoint.HardWare.RotateRate * (180 / Math.PI)) * 60;
            var coverageRotateScore = speedAz / (180d / 5d);

            var trackingScore = (coverageScore + ((coverageRotateScore + coverageElevateScore) * 0.5d)) * 0.5d;
            //if a sorter weapon use several barrels with only elevation or rotation the score should be uneffected since its designer to work
            if (MyUtils.IsZero(Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation)))
                trackingScore = (coverageScore + ((coverageRotateScore + 1) * 0.5d)) * 0.5d;

            if ((Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth)) == 0)
                trackingScore = (coverageScore + ((coverageElevateScore + 1) * 0.5d)) * 0.5d;

            if (MyUtils.IsZero(Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation) + (Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth))))
                trackingScore = 1.0d;

            trackingScore = trackingScore > 1 ? 1 : trackingScore;
            trackingScore = 1;

            //FinalScore
            var effectiveModifier = ((effectiveRangeScore * inaccuracyScore) * trackingScore);

            // static weapons get a tracking score of 50%
            if (MyUtils.IsZero(Math.Abs(Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation))) || Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth) == 0)
                trackingScore = 0.5f;


            //Logs for effective dps
            if (mexLogLevel >= 2) Log.Line($"newInaccuracyRadius = {inaccuracyRadius}");
            if (mexLogLevel >= 2) Log.Line($"DeviationAngle = { system.WConst.DeviateShotAngleRads}");
            if (mexLogLevel >= 1) Log.Line($"InaccuracyScore = {inaccuracyScore}");
            if (mexLogLevel >= 1) Log.Line($"effectiveRangeScore = {effectiveRangeScore}");
            if (mexLogLevel >= 2) Log.Line($"coverageScore = {coverageScore}");
            if (mexLogLevel >= 2) Log.Line($"ElevateRate = {(wDef.HardPoint.HardWare.ElevateRate * (180 / Math.PI))}");
            if (mexLogLevel >= 2) Log.Line($"coverageElevate = {speedEl}");
            if (mexLogLevel >= 2) Log.Line($"coverageElevateScore = {coverageElevateScore}");
            if (mexLogLevel >= 2) Log.Line($"RotateRate = {(wDef.HardPoint.HardWare.RotateRate * (180 / Math.PI))}");
            if (mexLogLevel >= 2) Log.Line($"coverageRotate = {speedAz}");
            if (mexLogLevel >= 2) Log.Line($"coverageRotateScore = {coverageRotateScore}");

            if (mexLogLevel >= 2) Log.Line($"CoverageScore = {(coverageScore + ((coverageRotateScore + coverageElevateScore) * 0.5d)) * 0.5d}");
            if (mexLogLevel >= 1) Log.Line($"trackingScore = {trackingScore}");
            if (mexLogLevel >= 1) Log.Line($"effectiveModifier = {effectiveModifier}");


            //DPS Calc


            if (!EnergyAmmo && MagazineSize > 0 || IsHybrid)
            {
                realShotsPerSec = GetShotsPerSecond(MagazineSize, wDef.HardPoint.Loading.MagsToLoad, s.WConst.RateOfFire, s.WConst.ReloadTime, s.BarrelsPerShot, l.TrajectilesPerBarrel, l.ShotsInBurst, l.DelayAfterBurst);
            }
            else if (EnergyAmmo && a.EnergyMagazineSize > 0)
            {
                realShotsPerSec = GetShotsPerSecond(a.EnergyMagazineSize, 1, s.WConst.RateOfFire, s.WConst.ReloadTime, s.BarrelsPerShot, l.TrajectilesPerBarrel, l.ShotsInBurst, l.DelayAfterBurst);
            }
            else
            {
                realShotsPerSec = GetShotsPerSecond(1, 1, s.WConst.RateOfFire, 0, s.BarrelsPerShot, l.TrajectilesPerBarrel, s.ShotsPerBurst, l.DelayAfterBurst);
            }
            var shotsPerSecPower = realShotsPerSec; //save for power calc

            shotsPerSec = realShotsPerSec;
            var shotsPerSecPreHeat = shotsPerSec;

            if (s.WConst.HeatPerShot * HeatModifier > 0)
            {


                var heatGenPerSec = (s.WConst.HeatPerShot * HeatModifier * realShotsPerSec) - system.WConst.HeatSinkRate; //heat - cooldown



                if (heatGenPerSec > 0)
                {

                    var safeToOverheat = (l.MaxHeat - (l.MaxHeat * l.Cooldown)) / heatGenPerSec;
                    var cooldownTime = (l.MaxHeat - (l.MaxHeat * l.Cooldown)) / system.WConst.HeatSinkRate;

                    var timeHeatCycle = (safeToOverheat + cooldownTime);


                    realShotsPerSec = (float) ((safeToOverheat / timeHeatCycle) * realShotsPerSec);

                    if ((mexLogLevel >= 1))
                    {
                        Log.Line($"Name = {s.PartName}");
                        Log.Line($"HeatPerShot = {s.WConst.HeatPerShot * HeatModifier}");
                        Log.Line($"HeatGenPerSec = {heatGenPerSec}");

                        Log.Line($"WepCoolDown = {l.Cooldown}");

                        Log.Line($"safeToOverheat = {safeToOverheat}");
                        Log.Line($"cooldownTime = {cooldownTime}");


                        Log.Line($"timeHeatCycle = {timeHeatCycle}s");

                        Log.Line($"realShotsPerSec wHeat = {realShotsPerSec}");
                    }

                }

            }
            var avgArmorModifier = GetAverageArmorModifier(a.DamageScales.Armor);

            realShotsPerMin = (realShotsPerSec * 60);
            //Log.Line($"Current: {a.AmmoRound} ");
            //if(wDef.HardPoint.PartName=="CoilCannon" && a.HardPointUsable) Log.Line($":::::[{wDef.HardPoint.PartName}]:::::");
            baseDps = BaseDamage * realShotsPerSec * avgArmorModifier;
            areaDps = 0; //TODO: Add back in some way
            detDps = (GetDetDmg(a) * realShotsPerSec) * avgArmorModifier;

            if (hasShrapnel)//Add damage from fragments
            {
                var sAmmo = wDef.Ammos[FragmentId];
                var fragments = a.Fragment.Fragments;

                Vector2 FragDmg = new Vector2(0, 0);
                Vector2 patternDmg = new Vector2(0, 0);

                FragDmg = FragDamageLoopCheck(wDef, realShotsPerSec, FragDmg, 0, a, a.Fragment.Fragments);


                //if(wDef.HardPoint.Other.Debug)Log.Line($"Total Fragment Dmg -- {FragDmg}");

                //TODO: fix when fragDmg is split
                baseDps += FragDmg.X;
                //baseDps += (sAmmo.BaseDamage * fragments) * realShotsPerSec;
                detDps += FragDmg.Y;
                //detDps += (GetDetDmg(sAmmo) * fragments) * realShotsPerSec;
            }



            if (a.Pattern.Enable || a.Pattern.Mode != AmmoDef.PatternDef.PatternModes.Never) //make into function
            {
                Vector2 totalPatternDamage = new Vector2();
                //Log.Line($"||:::Ammo [{a.AmmoRound} got {a.Pattern.Patterns.Length} patterns]");
                foreach (var patternName in a.Pattern.Patterns)
                {
                    for (int j = 0; j < wDef.Ammos.Length; j++)
                    {
                        //Log.Line($"Found J= {j}");
                        var patternAmmo = wDef.Ammos[j];
                        if (patternAmmo.AmmoRound.Equals(patternName))
                        {


                            //Log.Line($"Found [{pastI}|{j}] Fragment= {fragmentAmmo.Fragment.AmmoRound}");
                            //Log.Line($"::::{patternAmmo.AmmoRound} got a parent with:{parentFragments} fragments");
                            Vector2 tempDmg = new Vector2();
                            Vector2 tempFragDmg = new Vector2();
                            tempDmg.X += patternAmmo.BaseDamage;
                            tempDmg.Y += GetDetDmg(patternAmmo);
                            //Log.Line($"||:::PatternAmmo [{patternAmmo.AmmoRound}| temp dmg {tempDmg}]");
                            if (patternAmmo.Fragment.Fragments != 0)
                            {
                                tempFragDmg += FragDamageLoopCheck(wDef, 1, tempFragDmg, 0, a, a.Fragment.Fragments);
                            }
                            //Log.Line($"||:::PatternAmmo [{patternAmmo.AmmoRound}| Pattern dmg {tempFragDmg}]");

                            totalPatternDamage += tempFragDmg + tempDmg;

                        }

                    }

                }

                var numPatterns = a.Pattern.Patterns.Length;
                var stepModifier = a.Pattern.PatternSteps;

                totalPatternDamage *= realShotsPerSec * avgArmorModifier; //convert to DPS of combined patterns

                if (numPatterns != a.Pattern.PatternSteps) stepModifier = a.Pattern.PatternSteps == 0 ? 1 : a.Pattern.PatternSteps;

                if (!a.Pattern.SkipParent)
                {
                    numPatterns++;

                    totalPatternDamage.X += baseDps;
                    totalPatternDamage.Y += detDps;

                    totalPatternDamage /= numPatterns; //get average dps of all patterns and base ammo
                    totalPatternDamage *= stepModifier; //Multiply with how many

                    baseDps = totalPatternDamage.X;
                    areaDps = 0; //TODO: Add back in some way
                    detDps = totalPatternDamage.Y;

                }
                else
                {

                    totalPatternDamage /= numPatterns;
                    totalPatternDamage *= stepModifier;

                    baseDps = totalPatternDamage.X;
                    areaDps = 0; //TODO: Add back in some way
                    detDps = totalPatternDamage.Y;

                }


            }



            if (mexLogLevel >= 1) Log.Line($"Got Area damage={ByBlockHitDamage} det={GetDetDmg(a)} @ {realShotsPerSec} areadps={areaDps} basedps={baseDps} detdps={detDps}");


            peakDps = (baseDps + areaDps + detDps);
            effectiveDps = (float)(peakDps * effectiveModifier);
            dpsWoInaccuracy = (float)(effectiveModifier / inaccuracyScore) * peakDps;

            if (mexLogLevel >= 1) Log.Line($"peakDps= {peakDps}");

            if (mexLogLevel >= 1) Log.Line($"Effective DPS(mult) = {effectiveDps}");

            if (false)
            {

                //DebugAll Weapons

                Log.Line($"[========================]");
                Log.Line($":::::[{wDef.HardPoint.PartName}]:::::");
                Log.Line($"AmmoMagazine: {a.AmmoMagazine}");
                Log.Line($"AmmoRound: {a.AmmoRound}");
                Log.Line($"AmmoRound: {a.BaseDamage}");
                Log.Line($"AmmoRound: {a.AreaOfDamage.ByBlockHit.Damage}");
                Log.Line($"AmmoRound: {a.AreaOfDamage.EndOfLife.Damage}");
                Log.Line($"InaccuracyScore: {Math.Round(inaccuracyScore * 100, 2)}% | ShotAngle: {wDef.HardPoint.DeviateShotAngle}  @: { baselineRange}m vs { targetRadius}m Circle");
                Log.Line($"--------------------------");
                Log.Line($"Shots per second(w/Heat): {Math.Round(shotsPerSecPreHeat, 2)} ({Math.Round(realShotsPerSec, 2)})");
                Log.Line($"Peak DPS: {Math.Round(peakDps)}");
                Log.Line($"Effective DPS: {Math.Round(effectiveDps)} | without Inaccuracy: {Math.Round(dpsWoInaccuracy)}");
                Log.Line($"Base Damage DPS: {Math.Round(baseDps)}");
                Log.Line($"Area Damage DPS: {Math.Round(areaDps)}");
                Log.Line($"Explosive Dmg DPS: {Math.Round(detDps)}");
                Log.Line($"[=========== Ammo End =============]");



            }
            if (wDef.HardPoint.Other.Debug && a.HardPointUsable)
            {

                Log.Line($"[========================]");
                Log.Line($":::::[{wDef.HardPoint.PartName}]:::::");
                Log.Line($"AmmoMagazine: {a.AmmoMagazine}");
                Log.Line($"AmmoRound: {a.AmmoRound}");
                Log.Line($"InaccuracyScore: {Math.Round(inaccuracyScore * 100, 2)}% | ShotAngle: {wDef.HardPoint.DeviateShotAngle}  @: { baselineRange}m vs { targetRadius}m Circle");
                Log.Line($"--------------------------");
                Log.Line($"Shots per second(w/Heat): {Math.Round(shotsPerSecPreHeat, 2)} ({Math.Round(realShotsPerSec, 2)})");
                Log.Line($"Peak DPS: {Math.Round(peakDps)}");
                Log.Line($"Effective DPS: {Math.Round(effectiveDps)} | without Inaccuracy: {Math.Round(dpsWoInaccuracy)}");
                Log.Line($"Base Damage DPS: {Math.Round(baseDps)}");
                Log.Line($"Area Damage DPS: {Math.Round(areaDps)}");
                Log.Line($"Explosive Dmg DPS: {Math.Round(detDps)}");
                Log.Line($"[=========== Ammo End =============]");



            }


        }

        private Vector2 FragDamageLoopCheck(WeaponDefinition wDef, float shotsPerSec, Vector2 FragDmg, int pastI, AmmoDef parentAmmo, int parentFragments)
        {

            pastI++; //max fragment depth

            //Log.Line($"Found Ammos= {wDef.Ammos.Length}");
            for (int j = 0; j < wDef.Ammos.Length; j++)
            {
                //Log.Line($"Found J= {j}");
                var fragmentAmmo = wDef.Ammos[j];
                if (fragmentAmmo.AmmoRound.Equals(parentAmmo.Fragment.AmmoRound) && pastI < 10)
                {
                    //Log.Line($"Found [{pastI}|{j}] Fragment= {fragmentAmmo.Fragment.AmmoRound}");
                    //Log.Line($"::::{fragmentAmmo.AmmoRound} got a parent with:{parentFragments} fragments");
                    var tempDmg = GetShrapnelDamage(fragmentAmmo, parentFragments, shotsPerSec, parentFragments);
                    //Log.Line($"Per frag Damage {tempDmg/parentFragments} frags {parentFragments}||Total Frag Damage:{tempDmg}");
                    var fragFrags = 1.0f;
                    if (parentAmmo.Fragment.Fragments > 0) fragFrags = parentAmmo.Fragment.Fragments;
                    if (parentAmmo.Fragment.TimedSpawns.Enable)
                    {
                        //Log.Line($"Experimental DPS Calc| Ammo: {parentAmmo.Fragment.AmmoRound}");
                        //Log.Line($"Found Special Ammo= {parentFragments}");
                        var b = parentAmmo.Fragment.TimedSpawns;

                        float cycleTime = (b.Interval * ((b.GroupSize > 0 ? b.GroupSize : 1) - (b.GroupDelay > 0 ? 1 : 0))) + b.GroupDelay;
                        //Log.Line($"cycleTime: {cycleTime}");
                        tempDmg *= (1.0f / ((cycleTime / 60))) * (b.GroupSize > 0 ? b.GroupSize : 1);

                        //Log.Line($"Ammo/Drone Dps: Base:{tempDmg.X} Expl:{tempDmg.Y} at {(1.0f / ((cycleTime / 60))) * b.GroupSize * parentFragments} shots/s");
                        //Log.Line($"Block Dps: {tempDmg* shotsPerSec} at {(1.0f / ((cycleTime / 60))) * b.GroupSize * parentFragments* shotsPerSec} shots/s");
                        //fragFrags = b.GroupSize;
                        fragFrags = (1.0f / ((cycleTime / 60))) * b.GroupSize;

                    }



                    FragDmg += tempDmg;
                    parentFragments *= fragmentAmmo.Fragment.Fragments;
                    FragDmg = FragDamageLoopCheck(wDef, shotsPerSec, FragDmg, pastI, fragmentAmmo, parentFragments);


                }
                //if(pastI==10)Log.Line($"Ammo Reached Max Fragment Depth of {pastI}");
            }

            return FragDmg;
        }

        private Vector2 GetShrapnelDamage(AmmoDef fAmmo, int frags, float sps, int parentFragments)
        {
            Vector2 fragDmg = new Vector2(0, 0);

            fragDmg.X += (fAmmo.BaseDamage * frags) * sps;
            //fragDmg += 0;
            fragDmg.Y += (GetDetDmg(fAmmo) * frags) * sps;
            float avgArmorModifier = GetAverageArmorModifier(fAmmo.DamageScales.Armor);

            fragDmg *= avgArmorModifier;

            return fragDmg;
        }

        private static float GetAverageArmorModifier(AmmoDef.DamageScaleDef.ArmorDef armor)
        {
            var avgArmorModifier = 0.0f;
            if (armor.Heavy < 0) { avgArmorModifier += 1.0f; }
            else { avgArmorModifier += armor.Heavy; }
            if (armor.Light < 0) { avgArmorModifier += 1.0f; }
            else { avgArmorModifier += armor.Light; }
            if (armor.Armor < 0) { avgArmorModifier += 1.0f; }
            else { avgArmorModifier += armor.Armor; }
            if (armor.NonArmor < 0) { avgArmorModifier += 1.0f; }
            else { avgArmorModifier += armor.NonArmor; }

            avgArmorModifier *= 0.25f;
            return avgArmorModifier;
        }

        private float GetShotsPerSecond(int magCapacity, int magPerReload, int rof, int reloadTime, int barrelsPerShot, int trajectilesPerBarrel, int shotsInBurst, int delayAfterBurst)
        {
            //Log.Line($"magCapacity={magCapacity} rof={rof} reloadTime={reloadTime} barrelsPerShot={barrelsPerShot} trajectilesPerBarrel={trajectilesPerBarrel} shotsInBurst={shotsInBurst} delayAfterBurst={delayAfterBurst}");

            if (true) //WHy is this required ;_;
            {
                if (magPerReload < 1) magPerReload = 1;
                var reloadsPerRoF = rof / ((magCapacity * magPerReload) / (float)barrelsPerShot);
                var burstsPerRoF = shotsInBurst == 0 ? 0 : rof / (float)shotsInBurst;
                var ticksReloading = reloadsPerRoF * reloadTime;

                var ticksDelaying = burstsPerRoF * delayAfterBurst;

                if (mexLogLevel > 0) Log.Line($"burstsPerRof={burstsPerRoF} reloadsPerRof={reloadsPerRoF} ticksReloading={ticksReloading} ticksDelaying={ticksDelaying}");
                float shotsPerSecond = rof / (60f + (ticksReloading / 60) + (ticksDelaying / 60));
            }
            //if (magPerReload < 1) magPerReload = 1;
            //V2///////
            var doRofLog = false;
            if (doRofLog) Log.Line($"------V2-ROF------");
            if (doRofLog) Log.Line($"------------------");

            var totMagCap = magCapacity * magPerReload;

            // How many times will the weapon shoot per magazine
            var shotsPerMagazine = totMagCap == 1 ? 0 : (Math.Ceiling((float)totMagCap / barrelsPerShot) - 1);

            // How many bursts per magazine
            var burstPerMagazine = shotsInBurst == 0 ? 0 : Math.Ceiling(((float)totMagCap / (float)shotsInBurst) - 1); // how many bursts per magazine
                                                                                                                       // Log.Line($"NoReload..{reloadTime} spm {shotsPerMagazine}");

            //Case of no reload time
            if (reloadTime == 0)
            {
                shotsPerMagazine = totMagCap == 1 ? 0 : (Math.Ceiling((float)totMagCap / barrelsPerShot));
                burstPerMagazine = shotsInBurst == 0 ? 0 : Math.Ceiling(((float)totMagCap / (float)shotsInBurst));
                if (doRofLog) Log.Line($"NoReload..{reloadTime} spm {shotsPerMagazine} pbm {burstPerMagazine}");
            }

            //in tick - time spent shooting magazine
            var timeShots = shotsPerMagazine == 0 ? 0 : shotsPerMagazine * ((float)3600 / rof);
            // in tick - time spent on burst
            var timeBurst = burstPerMagazine == 0 ? 0 : burstPerMagazine * ((float)delayAfterBurst);
            // total time per mag
            var timePerCycle = timeShots + timeBurst + reloadTime; //add delayed fire

            if (doRofLog) Log.Line($"shotsPerMagazine={shotsPerMagazine} burstPerMagazine={burstPerMagazine} timeShots={timeShots} timeBurst={timeBurst} timePerCycle={timePerCycle} ");

            //if 0 its a non magazine weapon so a cycle will be base on rof
            timePerCycle = timePerCycle == 0 ? ((float)3600 / rof) : timePerCycle;

            //this part might be shit
            timePerCycle = timePerCycle < ((float)3600 / rof) ? ((float)3600 / rof) : timePerCycle;
            if (doRofLog) Log.Line($"timePerCycleFixed={timePerCycle} ");

            // Convert to seconds
            timePerCycle = (float)timePerCycle / 60f;
            if (doRofLog) Log.Line($"Mag Cycle Time(s)= {timePerCycle} ");

            //Shots per cycle
            var shotsPerSecondV2 = (float)timePerCycle / (totMagCap);
            //Shots per second
            shotsPerSecondV2 = 1.0f / shotsPerSecondV2;
            if (doRofLog) Log.Line($"shotsPerSecond V2 = {shotsPerSecondV2 * trajectilesPerBarrel}");
            //if (doRofLog) Log.Line($"shotsPerSecond V1 = {shotsPerSecond * trajectilesPerBarrel * barrelsPerShot}");

            if (doRofLog) Log.Line($"----------------");




            //----




            return shotsPerSecondV2 * trajectilesPerBarrel;
            //return shotsPerSecond * trajectilesPerBarrel * barrelsPerShot;
        }


        private float GetDetDmg(AmmoDef a)
        {
            var dmgOut = 0.0d;
            var dmgByBlockHit = a.AreaOfDamage.ByBlockHit;
            var dmgEndOfLife = a.AreaOfDamage.EndOfLife;


            if (dmgByBlockHit.Enable)
            {
                if (mexLogLevel >= 1) Log.Line($"ByBlockHit = {dmgByBlockHit.Falloff.ToString()}");

                dmgOut += dmgByBlockHit.Damage * GetFalloffModifier(dmgByBlockHit.Falloff.ToString(), (float)dmgByBlockHit.Radius); ;

            };

            if (dmgEndOfLife.Enable)
            {
                if (mexLogLevel >= 1) Log.Line($"EndOffLife = {dmgEndOfLife.Falloff.ToString()}");
                dmgOut += dmgEndOfLife.Damage * GetFalloffModifier(dmgEndOfLife.Falloff.ToString(), (float)dmgEndOfLife.Radius); ;
            };
            if (mexLogLevel >= 1) Log.Line($"dmgOut = {dmgOut}");
            return (float)dmgOut;
        }

        private static double GetFalloffModifier(string falloffType, float radius)
        {
            var falloffModifier = 1.0d;
            //Sphere
            double blocksHit = Math.Round(((4 / 3) * 3.14f * Math.Pow(radius, 3)) / Math.Pow(2.5f, 3)) / 2;
            //Pyramid
            //double blocksHit = (((0.3333d*radius)*radius)*radius) / Math.Pow(2.5f, 3);
            //Log.Line($"blocksHit = {blocksHit}");


            switch (falloffType)
            {
                case "NoFalloff":
                    falloffModifier = blocksHit * 1.0d;
                    break;
                case "Linear":
                    falloffModifier = blocksHit * 0.55d;
                    break;
                case "Curve":
                    falloffModifier = blocksHit * 0.81d;
                    break;
                case "InvCurve":
                    falloffModifier = blocksHit * 0.39d;
                    break;
                case "Squeeze":
                    falloffModifier = blocksHit * 0.22d;
                    break;
                case "Exponential":
                    falloffModifier = blocksHit * 0.29d;
                    break;
                default:
                    falloffModifier = 1;
                    break;
            }

            return falloffModifier;
        }

    }

    public class ApproachConstants
    {
        public readonly int Index;
        public readonly MySoundPair SoundPair;
        public readonly MyConcurrentPool<MyEntity> ModelPool;
        public readonly ApproachDef Definition;
        public readonly string ModelPath;
        public readonly bool AlternateTravelSound;
        public readonly bool AlternateTravelParticle;
        public readonly bool AlternateModel;
        public readonly bool StartParticle;
        public readonly bool EndAnd;
        public readonly bool StartAnd;
        public readonly double ModFutureStep;

        public ApproachConstants(WeaponSystem.AmmoType ammo, int index, WeaponDefinition wDef)
        {
            var def = ammo.AmmoDef.Trajectory.Approaches[index];
            Index = index;
            Definition = def;
            AlternateTravelSound = !string.IsNullOrEmpty(def.AlternateSound);
            AlternateTravelParticle = !string.IsNullOrEmpty(def.AlternateParticle.Name);
            StartParticle = !string.IsNullOrEmpty(def.StartParticle.Name);
            AlternateModel = !string.IsNullOrEmpty(def.AlternateModel);

            if (AlternateModel)
            {
                ModelPath = wDef.ModPath + def.AlternateModel;
            }

            if (AlternateTravelSound)
            {
                SoundPair = new MySoundPair(def.AlternateSound);
            }

            ModelPool = AlternateModel ? new MyConcurrentPool<MyEntity>(64, AlternateEntityClear, 640, AlternateEntityActivator) : null;

            var stepConst = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            var desiredSpeed = ammo.AmmoDef.Trajectory.DesiredSpeed;
            var accel = ammo.AmmoDef.Trajectory.AccelPerSec;

            var desiredSpeedStep = desiredSpeed * stepConst;
            var maxStepLimit = accel * def.AccelMulti > 0 ? def.AccelMulti : accel;
            var speedStepLimit = def.SpeedCapMulti * desiredSpeedStep;
            var futureStepLimit = maxStepLimit <= speedStepLimit ? maxStepLimit : speedStepLimit;

            ModFutureStep = futureStepLimit;
            GetOperators(def, out StartAnd, out EndAnd);
        }

        private static void GetOperators(ApproachDef def, out bool startAnd, out bool endAnd)
        {
            switch (def.Operators)
            {
                case ApproachDef.ConditionOperators.StartEnd_And:
                    startAnd = true;
                    endAnd = true;
                    return;
                case ApproachDef.ConditionOperators.StartEnd_Or:
                    startAnd = false;
                    endAnd = false;
                    return;
                case ApproachDef.ConditionOperators.StartAnd_EndOr:
                    startAnd = true;
                    endAnd = false;
                    return;
                case ApproachDef.ConditionOperators.StartOr_EndAnd:
                    startAnd = false;
                    endAnd = true;
                    return;
                default:
                    startAnd = true;
                    endAnd = true;
                    break;
            }
        } 

        public void Clean()
        {
            ModelPool?.Clean();
        }

        private MyEntity AlternateEntityActivator()
        {
            var ent = new MyEntity();
            ent.Init(null, ModelPath, null, null);
            ent.Render.CastShadows = false;
            ent.IsPreview = true;
            ent.Save = false;
            ent.SyncFlag = false;
            ent.NeedsWorldMatrix = false;
            ent.Flags |= EntityFlags.IsNotGamePrunningStructureObject;
            MyEntities.Add(ent, false);
            return ent;
        }

        private static void AlternateEntityClear(MyEntity myEntity)
        {
            myEntity.PositionComp.SetWorldMatrix(ref MatrixD.Identity, null, false, false, false);
            myEntity.InScene = false;
            myEntity.Render.RemoveRenderObjects();
        }

    }

    internal class ValueProcessors
    {

        internal abstract class BaseProcessor
        {
            protected object StoredValue;
            protected ValueType Type;
            public bool HasData() => StoredValue != null;
            public ValueType GetType() => Type;
            public float GetAsFloat => (float)StoredValue;
            public double GetAsDouble => (double)StoredValue;
            public int GetAsInt => (int)StoredValue;
            public bool GetAsBool => (bool)StoredValue;
            public abstract bool WriteData(string value);

            public enum ValueType
            {
                Invalid,
                Float,
                Double,
                Int,
                Bool,
                Enum,
                String
            }
        }

        internal class FloatProcessor : BaseProcessor
        {
            public override bool WriteData(string value)
            {
                float floatValue;
                if (float.TryParse(value, out floatValue) && floatValue >= 0)
                {
                    StoredValue = floatValue;
                    Type = ValueType.Float;
                    return true;
                }
                return false;
            }
        }

        internal class NonZeroFloatProcessor : BaseProcessor
        {
            public override bool WriteData(string value)
            {
                float floatValue;
                if (float.TryParse(value, out floatValue) && floatValue > 0)
                {
                    StoredValue = floatValue;
                    Type = ValueType.Float;
                    return true;
                }
                return false;
            }
        }

        internal class DoubleProcessor : BaseProcessor
        {
            public override bool WriteData(string value)
            {
                double doubleValue;
                if (double.TryParse(value, out doubleValue) && doubleValue >= 0)
                {
                    StoredValue = doubleValue;
                    Type = ValueType.Double;
                    return true;
                }
                return false;
            }
        }

        internal class IntProcessor : BaseProcessor
        {
            public override bool WriteData(string value)
            {
                int intValue;
                if (int.TryParse(value, out intValue) && intValue >= 0)
                {
                    StoredValue = intValue;
                    Type = ValueType.Int;
                    return true;
                }
                return false;
            }
        }

        internal class NonZeroIntProcessor : BaseProcessor
        {
            public override bool WriteData(string value)
            {
                int intValue;
                if (int.TryParse(value, out intValue) && intValue > 0)
                {
                    StoredValue = intValue;
                    Type = ValueType.Int;
                    return true;
                }
                return false;
            }
        }

        internal class BoolProcessor : BaseProcessor
        {
            public override bool WriteData(string value)
            {
                bool boolValue;
                if (bool.TryParse(value, out boolValue))
                {
                    StoredValue = boolValue;
                    Type = ValueType.Bool;
                    return true;
                }
                return false;
            }
        }

    }
}
