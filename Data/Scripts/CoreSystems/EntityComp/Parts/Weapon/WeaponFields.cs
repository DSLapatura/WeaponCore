using System;
using System.Collections.Generic;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

namespace CoreSystems.Platform
{
    public partial class Weapon : Part
    {
        internal int NextMuzzle;
        internal volatile bool Casting;
        private readonly int _numOfMuzzles;
        private readonly int _numModelBarrels;
        private readonly HashSet<string> _muzzlesToFire = new HashSet<string>();
        private readonly HashSet<string> _muzzlesFiring = new HashSet<string>();
        internal readonly Dictionary<int, string> MuzzleIdToName = new Dictionary<int, string>();

        internal readonly WeaponFrameCache WeaponCache;
        internal readonly WeaponSystem System;
        internal readonly Target Target;
        internal readonly Target NewTarget;
        internal readonly PartInfo MuzzlePart;
        internal readonly Dummy[] Dummies;
        internal readonly Dummy Ejector;
        internal readonly Dummy Scope;
        internal readonly Muzzle[] Muzzles;
        internal readonly PartInfo AzimuthPart;
        internal readonly PartInfo ElevationPart;
        internal readonly PartInfo SpinPart;
        internal readonly Dictionary<EventTriggers, ParticleEvent[]> ParticleEvents;
        internal readonly uint[] BeamSlot;
        internal readonly MyParticleEffect[] Effects1;
        internal readonly MyParticleEffect[] Effects2;
        internal readonly MyParticleEffect[] HitEffects;
        internal readonly WeaponComponent Comp;
        internal readonly MyEntity3DSoundEmitter ReloadEmitter;
        internal readonly MyEntity3DSoundEmitter PreFiringEmitter;
        internal readonly MyEntity3DSoundEmitter FiringEmitter;
        internal readonly MyEntity3DSoundEmitter BarrelRotateEmitter;
        internal readonly MyEntity3DSoundEmitter HardPointEmitter;

        internal readonly Dictionary<EventTriggers, PartAnimation[]> AnimationsSet;
        internal readonly Dictionary<string, PartAnimation> AnimationLookup = new Dictionary<string, PartAnimation>();
        internal bool AlternateForward;
        internal readonly bool PrimaryWeaponGroup;
        internal readonly bool AiOnlyWeapon;
        internal readonly bool HasHardPointSound;

        private int _nextVirtual;
        private uint _ticksUntilShoot;
        private uint _spinUpTick;
        private uint _ticksBeforeSpinUp;

        internal bool BurstAvDelay;
        internal bool HeatLoopRunning;
        internal bool PreFired;
        internal bool FinishShots;
        internal bool LockOnFireState;
        internal bool ScheduleAmmoChange;
        internal bool CriticalReaction;
        internal bool FoundTopMostTarget;
        internal uint LastMagSeenTick;
        internal uint GravityTick;
        internal uint ShootTick;
        internal uint LastShootTick;
        internal uint TicksPerShot;
        internal uint PosChangedTick;
        internal uint ElevationTick;
        internal uint AzimuthTick;
        internal uint FastTargetResetTick;
        internal uint LastNanTick;
        internal uint OverHeatCountDown;
        internal uint DelayedTargetResetTick;
        internal float HeatPerc;
        internal int BarrelRate;
        internal int ShotsFired;
        internal int MiddleMuzzleIndex;
        internal int DelayedCycleId = -1;
        internal List<MyEntity> HeatingParts;
        internal Vector3D GravityPoint;
        internal Vector3D MyPivotPos;
        internal Vector3D BarrelOrigin;
        internal Vector3D MyPivotFwd;
        internal Vector3D MyPivotUp;
        internal Vector3D AimOffset;
        internal Vector3D AzimuthInitFwdDir;
        internal MatrixD WeaponConstMatrix;

        internal LineD MyCenterTestLine;
        internal LineD MyBarrelTestLine;
        internal LineD MyPivotTestLine;
        internal LineD MyAimTestLine;
        internal LineD MyShootAlignmentLine;
        internal LineD AzimuthFwdLine;
        internal XorShiftRandomStruct XorRnd;

        internal MyOrientedBoundingBoxD TargetBox;
        internal LineD LimitLine;


        internal MathFuncs.Cone AimCone;
        internal Matrix[] BarrelRotationPerShot = new Matrix[10];

        internal string AmmoName = "";
        internal ProtoWeaponPartState PartState;
        internal ProtoWeaponReload Reload;
        internal ProtoWeaponTransferTarget TargetData;
        internal ProtoWeaponAmmo ProtoWeaponAmmo;
        internal WeaponSystem.AmmoType ActiveAmmoDef;
        internal int[] AmmoShufflePattern = {0};
        internal ParallelRayCallBack RayCallBack;
        internal ControlSys.ControlComponent MasterComp;


        internal IHitInfo LastHitInfo;
        internal EventTriggers LastEvent;

        internal float ShotEnergyCost;
        internal float LastHeat;
        internal uint ReloadEndTick = uint.MaxValue;
        internal uint CeaseFireDelayTick = uint.MaxValue / 2;
        internal uint LastTargetTick;
        internal uint LastTrackedTick;
        internal uint LastMuzzleCheck;
        internal uint LastSmartLosCheck;
        internal uint LastLoadedTick;
        internal uint OffDelay;
        internal uint AnimationDelayTick;
        internal uint TrackingDelayTick;
        internal uint LastHeatUpdateTick;
        internal uint LastInventoryTick;
        internal uint StopBarrelAvTick;
        internal int LiveDrones;
        internal int ProposedAmmoId = -1;
        internal int ShootCount;
        internal int FireCounter;
        internal int RateOfFire;
        internal int BarrelSpinRate;
        internal int EnergyPriority;
        internal int LastBlockCount;
        internal int ClientStartId;
        internal int ClientEndId;
        internal int ClientMakeUpShots;
        internal int ClientLastShotId;
        internal int ProjectileCounter;

        internal float HeatPShot;
        internal float HsRate;
        internal float CurrentAmmoVolume;
        internal double Azimuth;
        internal double Elevation;
        internal double AimingTolerance;
        internal double RotationSpeed;
        internal double ElevationSpeed;
        internal double MaxAzToleranceRadians;
        internal double MinAzToleranceRadians;
        internal double MaxElToleranceRadians;
        internal double MinElToleranceRadians;
        internal double MaxAzimuthRadians;
        internal double MinAzimuthRadians;
        internal double MaxElevationRadians;
        internal double MinElevationRadians;
        internal double MaxTargetDistance;
        internal double MaxTargetDistanceSqr;
        internal double MinTargetDistance;
        internal double MinTargetDistanceSqr;
        internal double MinTargetDistanceBufferSqr;
        internal double MuzzleDistToBarrelCenter;
        internal double ScopeDistToCheckPos;
        internal bool TurretActive;
        internal bool TargetLock;
        internal bool ClientReloading;
        internal bool ServerQueuedAmmo;
        internal bool Rotating;
        internal bool TurretAttached;
        internal bool TurretController;
        internal bool AiShooting;
        internal bool IsShooting;
        internal bool PlayTurretAv;
        internal bool AvCapable;
        internal bool NoMagsToLoad;
        internal bool CurrentlyDegrading;
        internal bool FixedOffset;
        internal bool ProjectilesNear;
        internal bool AcquiringTarget;
        internal bool BarrelSpinning;
        internal bool ReturingHome;
        internal bool IsHome = true;
        internal bool CanUseEnergyAmmo;
        internal bool CanUseHybridAmmo;
        internal bool CanUseChargeAmmo;
        internal bool CanUseBeams;
        internal bool PauseShoot;
        internal bool ShowReload;
        internal bool ParentIsSubpart;
        internal bool CheckInventorySystem = true;
        internal bool PlayingHardPointSound;
        internal bool VanillaTracking;
        internal bool RotorTurretTracking;

        internal bool ShotReady
        {
            get
            {
                var reloading = ActiveAmmoDef.AmmoDef.Const.Reloadable && ClientMakeUpShots == 0 && (Loading || ProtoWeaponAmmo.CurrentAmmo == 0 || Reload.WaitForClient);
                var overHeat = PartState.Overheated && OverHeatCountDown == 0;
                var canShoot = !overHeat && !reloading && !System.DesignatorWeapon;
                var shotReady = canShoot;
                return shotReady;
            }
        }

        internal bool LoadingWait => ReloadEndTick < uint.MaxValue - 1;
        internal Dummy GetScope => Scope ?? Dummies[MiddleMuzzleIndex];

        internal struct AmmoLoad
        {
            internal enum ChangeType
            {
                Add,
                Remove
            }

            internal MyPhysicalInventoryItem Item;
            internal int Amount;
            internal int OldId;
            internal ChangeType Change;
        }

        internal Weapon(MyEntity entity, WeaponSystem system, int partId, WeaponComponent comp, RecursiveSubparts parts, MyEntity elevationPart, MyEntity azimuthPart, MyEntity spinPart, string azimuthPartName, string elevationPartName)
        {
            Comp = comp;
            System = system;
            Init(comp, system, partId);

            if (System.Session.MpActive && System.HasProjectileSync)
                System.Session.WeaponProSyncs[UniqueId] = new Dictionary<long, ProtoWeaponProSync>();

            MyStringHash subtype;
            if (comp.Session.VanillaIds.TryGetValue(comp.Id, out subtype)) {
                if (subtype.String.Contains("Gatling"))
                    _numModelBarrels = 6;
                else
                    _numModelBarrels = System.Muzzles.Length;
            }
            else
                _numModelBarrels = System.Muzzles.Length;

            bool hitParticle = false;
            foreach (var ammoType in System.AmmoTypes)
            {
                var c = ammoType.AmmoDef.Const;
                if (c.EnergyAmmo) CanUseEnergyAmmo = true;
                if (c.IsHybrid) CanUseHybridAmmo = true;
                if (c.MustCharge) CanUseChargeAmmo = true;
                if (c.IsBeamWeapon) CanUseBeams = true;
                if (c.HitParticle) hitParticle = true;
            }

            comp.HasEnergyWeapon = comp.HasEnergyWeapon || CanUseEnergyAmmo || CanUseHybridAmmo;

            AvCapable = System.HasBarrelShootAv && !Comp.Session.DedicatedServer || hitParticle;

            if (AvCapable && system.FiringSound == WeaponSystem.FiringSoundState.WhenDone)
            {
                FiringEmitter = System.Session.Emitters.Count > 0 ? System.Session.Emitters.Pop() : new MyEntity3DSoundEmitter(null);
                FiringEmitter.CanPlayLoopSounds = true;
                FiringEmitter.Entity = Comp.CoreEntity;
            }

            if (AvCapable && system.PreFireSound)
            {
                PreFiringEmitter = System.Session.Emitters.Count > 0 ? System.Session.Emitters.Pop() : new MyEntity3DSoundEmitter(null);
                PreFiringEmitter.CanPlayLoopSounds = true;

                PreFiringEmitter.Entity = Comp.CoreEntity;
            }

            if (AvCapable && system.WeaponReloadSound)
            {
                ReloadEmitter = System.Session.Emitters.Count > 0 ? System.Session.Emitters.Pop() : new MyEntity3DSoundEmitter(null);
                ReloadEmitter.CanPlayLoopSounds = true;

                ReloadEmitter.Entity = Comp.CoreEntity;
            }

            if (AvCapable && system.BarrelRotateSound)
            {
                BarrelRotateEmitter = System.Session.Emitters.Count > 0 ? System.Session.Emitters.Pop() : new MyEntity3DSoundEmitter(null);
                BarrelRotateEmitter.CanPlayLoopSounds = true;

                BarrelRotateEmitter.Entity = Comp.CoreEntity;
            }

            if (AvCapable)
            {
                if (System.BarrelEffect1)
                    Effects1 = new MyParticleEffect[System.Values.Assignments.Muzzles.Length];
                if (System.BarrelEffect2)
                    Effects2 = new MyParticleEffect[System.Values.Assignments.Muzzles.Length];
                if (hitParticle && CanUseBeams)
                    HitEffects = new MyParticleEffect[System.Values.Assignments.Muzzles.Length];
            }

            if (System.TurretMovement != WeaponSystem.TurretType.Fixed)
                Comp.HasAim = true;

            PrimaryWeaponGroup = PartId % 2 == 0;
            TurretAttached = System.Values.HardPoint.Ai.TurretAttached;
            TurretController = System.Values.HardPoint.Ai.TurretController;

            AimOffset = System.Values.HardPoint.HardWare.Offset;
            FixedOffset = System.Values.HardPoint.HardWare.FixedOffset;

            HsRate = system.WConst.HeatSinkRate / 3;
            EnergyPriority = System.Values.HardPoint.Other.EnergyPriority;
            var toleranceInRadians = System.WConst.AimingToleranceRads;
            AimCone.ConeAngle = toleranceInRadians;
            AimingTolerance = Math.Cos(toleranceInRadians);

            if (Comp.Platform.Structure.PrimaryPart == partId)
                comp.TrackingWeapon = this;

            if (TurretAttached && !System.TrackTargets)
                Target = comp.TrackingWeapon.Target;
            else Target = new Target(this, true);

            _numOfMuzzles = System.Muzzles.Length;
            BeamSlot = new uint[_numOfMuzzles];
            Muzzles = new Muzzle[_numOfMuzzles];
            Dummies = new Dummy[_numOfMuzzles];
            WeaponCache = new WeaponFrameCache(_numOfMuzzles);
            NewTarget = new Target(this);
            RayCallBack = new ParallelRayCallBack(this);
            Acquire = new PartAcquire(this);
            AzimuthPart = new PartInfo {Entity = azimuthPart, IsBlock = azimuthPart == Comp.CoreEntity, ParentIsBlock = azimuthPart?.Parent == Comp.CoreEntity};
            ElevationPart = new PartInfo {Entity = elevationPart, IsBlock = elevationPart == Comp.CoreEntity, ParentIsBlock = elevationPart?.Parent == Comp.CoreEntity };
            SpinPart = new PartInfo {Entity = spinPart, IsBlock = spinPart == Comp.CoreEntity, ParentIsBlock = spinPart?.Parent == Comp.CoreEntity };
            MuzzlePart = new PartInfo { Entity = entity,  IsBlock = entity == Comp.CoreEntity, ParentIsBlock = entity?.Parent == Comp.CoreEntity };
            MiddleMuzzleIndex = Muzzles.Length > 1 ? Muzzles.Length / 2 - 1 : 0;

            AnimationsSet = comp.Session.CreateWeaponAnimationSet(system, parts);

            try
            {
                foreach (var set in AnimationsSet)
                {
                    foreach (var pa in set.Value)
                    {
                        var modifiesCore = pa.Part == azimuthPart || pa.Part == elevationPart || pa.Part == spinPart ||
                                           pa.Part == entity;
                        if (modifiesCore && (pa.HasMovement || pa.MovesPivotPos))
                        {
                            Comp.AnimationsModifyCoreParts = true;
                            if (!System.Session.DedicatedServer && System.Session.PerformanceWarning.Add(Comp.SubTypeId))
                                Log.Line($"{Comp.SubtypeName} - {System.PartName} - Animation modifies core subparts, performance impact");
                        }

                        comp.AllAnimations.Add(pa);
                        AnimationLookup.Add(pa.AnimationId, pa);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in {comp.SubtypeName} AnimationsSet: {ex}", null, true); }



            ParticleEvents = comp.Session.CreateWeaponParticleEvents(system, parts);

            var burstDelay = System.Values.HardPoint.Loading.DelayAfterBurst;
            ShowReload = Comp.Session.HandlesInput && (System.WConst.ReloadTime >= 60 || System.Values.HardPoint.Loading.ShotsInBurst > 0 && burstDelay >= 60);

            ParentIsSubpart = azimuthPart.Parent is MyEntitySubpart;
            AzimuthInitFwdDir = azimuthPart.PositionComp.LocalMatrixRef.Forward;
            
            FuckMyLife();
            
            AiOnlyWeapon = Comp.TypeSpecific != CoreComponent.CompTypeSpecific.VanillaTurret || (Comp.TypeSpecific == CoreComponent.CompTypeSpecific.VanillaTurret && (azimuthPartName != "MissileTurretBase1" && elevationPartName != "MissileTurretBarrels" && azimuthPartName != "InteriorTurretBase1" && elevationPartName != "InteriorTurretBase2" && azimuthPartName != "GatlingTurretBase1" && elevationPartName != "GatlingTurretBase2"));

            VanillaTracking = TurretAttached && !AiOnlyWeapon;

            CriticalReaction = Comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom && System.Values.HardPoint.HardWare.CriticalReaction.Enable;

            string ejectorMatch;
            MyEntity ejectorPart;
            if (System.HasEjector && Comp.Platform.Parts.FindFirstDummyByName(System.Values.Assignments.Ejector, System.AltEjectorName, out ejectorPart, out ejectorMatch))
                Ejector = new Dummy(ejectorPart,this, System.Values.Assignments.Ejector);

            string scopeMatch;
            MyEntity scopePart;
            if (System.HasScope && Comp.Platform.Parts.FindFirstDummyByName(System.Values.Assignments.Scope, System.AltScopeName, out scopePart, out scopeMatch))
                Scope = new Dummy(scopePart, this, scopeMatch);

            comp.Platform.SetupWeaponUi(this);

            if (!comp.Debug && System.Values.HardPoint.Other.Debug)
                comp.Debug = true;

            var hasHardPointSound = false;
            if (TurretController)
            {
                if (System.Values.HardPoint.Ai.PrimaryTracking && comp.TrackingWeapon == null)
                    comp.TrackingWeapon = this;

                if (AvCapable && System.HardPointRotationSound && (comp.TrackingWeapon == this || !System.Values.HardPoint.Ai.PrimaryTracking))
                {
                    hasHardPointSound = true;
                    HardPointEmitter = System.Session.Emitters.Count > 0 ? System.Session.Emitters.Pop() : new MyEntity3DSoundEmitter(null);
                    HardPointEmitter.CanPlayLoopSounds = true;

                    HardPointEmitter.Entity = Comp.CoreEntity;
                }
            }

            HasHardPointSound = hasHardPointSound;

            if (System.HasAntiSmart)
                System.Session.AntiSmartActive = true;
        }

        private void FuckMyLife()
        {
            var azPartMatrix = AzimuthPart.Entity.PositionComp.LocalMatrixRef;
            
            var fwdX = Math.Abs(azPartMatrix.Forward.X);
            var fwdY = Math.Abs(azPartMatrix.Forward.Y);
            var fwdZ = Math.Abs(azPartMatrix.Forward.Z);

            var fwdXAngle = !MyUtils.IsEqual(fwdX, 1f) && !MyUtils.IsZero(fwdX);
            var fwdYAngle = !MyUtils.IsEqual(fwdY, 1f) && !MyUtils.IsZero(fwdY);
            var fwdZAngle = !MyUtils.IsEqual(fwdZ, 1f) && !MyUtils.IsZero(fwdZ);

            var fwdAngled = fwdXAngle || fwdYAngle || fwdZAngle; 

            var upX = Math.Abs(azPartMatrix.Up.X);
            var upY = Math.Abs(azPartMatrix.Up.Y);
            var upZ = Math.Abs(azPartMatrix.Up.Z);

            var upXAngle = !MyUtils.IsEqual(upX, 1f) && !MyUtils.IsZero(upX);
            var upYAngle = !MyUtils.IsEqual(upY, 1f) && !MyUtils.IsZero(upY);
            var upZAngle = !MyUtils.IsEqual(upZ, 1f) && !MyUtils.IsZero(upZ);
           
            var upAngled = upXAngle || upYAngle || upZAngle;

            var leftX = Math.Abs(azPartMatrix.Up.X);
            var leftY = Math.Abs(azPartMatrix.Up.Y);
            var leftZ = Math.Abs(azPartMatrix.Up.Z);

            var leftXAngle = !MyUtils.IsEqual(leftX, 1f) && !MyUtils.IsZero(leftX);
            var leftYAngle = !MyUtils.IsEqual(leftY, 1f) && !MyUtils.IsZero(leftY);
            var leftZAngle = !MyUtils.IsEqual(leftZ, 1f) && !MyUtils.IsZero(leftZ);
            
            var leftAngled = leftXAngle || leftYAngle || leftZAngle;

            if (fwdAngled || upAngled || leftAngled)
                AlternateForward = true;
        }
    }
}
