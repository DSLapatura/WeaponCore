using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Projectiles;
using CoreSystems.Support;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using static CoreSystems.Support.Target;
using static CoreSystems.Support.CoreComponent.Start;
using static CoreSystems.Support.CoreComponent.TriggerActions;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType;
using static CoreSystems.ProtoWeaponState;
using VRage.Game.Entity;
using Sandbox.Game.Entities;
using System;
using VRage.Utils;
using SpaceEngineers.Game.ModAPI;

namespace CoreSystems
{
    public partial class Session
    {
        private void AiLoop()
        { //Fully Inlined due to keen's mod profiler

            foreach (var ai in EntityAIs.Values)
            {

                ///
                /// GridAi update section
                ///
                ai.MyProjectiles = 0;
                var activeTurret = false;

                if (ai.MarkedForClose || !ai.AiInit || ai.TopEntity == null || ai.Construct.RootAi == null || ai.TopEntity.MarkedForClose)
                    continue;

                ai.Concealed = ((uint)ai.TopEntity.Flags & 4) > 0;
                if (ai.Concealed)
                    continue;

                if (!ai.ScanInProgress && Tick - ai.TargetsUpdatedTick > 100 && DbTask.IsComplete)
                    ai.RequestDbUpdate();

                if (ai.DeadProjectiles.Count > 0) {
                    for (int i = 0; i < ai.DeadProjectiles.Count; i++) ai.LiveProjectile.Remove(ai.DeadProjectiles[i]);
                    ai.DeadProjectiles.Clear();
                    ai.LiveProjectileTick = Tick;
                }
                var enemyProjectiles = ai.LiveProjectile.Count > 0;
                ai.CheckProjectiles = Tick - ai.NewProjectileTick <= 1;

                if (ai.AiType == Ai.AiTypes.Grid && (ai.UpdatePowerSources || !ai.HadPower && ai.GridEntity.IsPowered || ai.HasPower && !ai.GridEntity.IsPowered || Tick10))
                    ai.UpdateGridPower();

                var enforcement = Settings.Enforcement;
                var advOptimize = enforcement.AdvancedOptimizations;

                if (ai.AiType == Ai.AiTypes.Grid && !ai.HasPower || enforcement.ServerSleepSupport && IsServer && ai.AwakeComps == 0 && ai.WeaponsTracking == 0 && ai.SleepingComps > 0 && !ai.CheckProjectiles && ai.AiSleep && !ai.DbUpdated) 
                    continue;

                var construct = ai.Construct;
                var focus = construct.Focus;

                if (ai.AiType == Ai.AiTypes.Grid && (ai.GridMap.GroupMap.LastControllerTick == Tick || ai.GridMap.LastControllerTick == Tick))
                    Ai.Constructs.UpdatePlayerStates(ai.GridMap.GroupMap);

                if (Tick60 && ai.AiType == Ai.AiTypes.Grid && ai.BlockChangeArea != BoundingBox.Invalid)
                {
                    ai.BlockChangeArea.Min *= ai.GridEntity.GridSize;
                    ai.BlockChangeArea.Max *= ai.GridEntity.GridSize;
                }

                var rootAi = construct.RootAi;
                var rootConstruct = rootAi.Construct;


                if (Tick60 && Tick != rootConstruct.LastEffectUpdateTick && rootConstruct.TotalEffect > rootConstruct.PreviousTotalEffect)
                    rootConstruct.UpdateEffect(Tick);

                if (IsServer) 
                {
                    if (rootConstruct.NewInventoryDetected)
                        rootConstruct.CheckForMissingAmmo();
                    else if (Tick60 && rootConstruct.RecentItems.Count > 0)
                        rootConstruct.CheckEmptyWeapons();
                }


                ///
                /// Control update section
                /// 
                try
                {
                    for (int i = 0; i < ai.ControlComps.Count; i++)
                    {
                        var cComp = ai.ControlComps[i];
                        if (cComp.Status != Started)
                            cComp.HealthCheck();

                        if (ai.DbUpdated || !cComp.UpdatedState)
                        {
                            cComp.DetectStateChanges();
                        }
                        if (cComp.Platform.State != CorePlatform.PlatformState.Ready)
                            continue;

                        if (cComp.IsAsleep || !cComp.IsWorking || cComp.CoreEntity.MarkedForClose || cComp.IsDisabled)
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        var controller = cComp.CoreEntity as IMyTurretControlBlock;

                        var azRaw = (IMyMotorStator)controller.AzimuthRotor;
                        var elRaw = (IMyMotorStator)controller.ElevationRotor;

                        StatorMap az;
                        StatorMap el;
                        if (azRaw == null || elRaw == null || !StatorMaps.TryGetValue(azRaw, out az) || !StatorMaps.TryGetValue(elRaw, out el))
                        {
                            if (Tick - cComp.LastCrashTick > 1200 && azRaw != null && elRaw != null)
                            {
                                cComp.LastCrashTick = Tick;
                                Log.Line($"CTC failed to find stator in StatorMaps - entId:{cComp.CoreEntity.EntityId} - StatorMapsContainsAz:{StatorMaps.ContainsKey(azRaw)} - StatorMapsContainsEl:{StatorMaps.ContainsKey(elRaw)}");
                            }
                            continue;
                        }

                        if (controller.IsUnderControl)
                        {
                            cComp.RotorsMoving = true;
                            continue;
                        }

                        var controlPart = cComp.Platform.Control;

                        if (az == null && el == null)
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        var baseMap = controlPart.BaseMap;
                        if (baseMap == null || baseMap != null && baseMap != az && baseMap != el)
                        {
                            Log.Line($"Setting base: elNull:{el == null} - azNull:{az == null} - azStatorNull:{az?.Stator == null} - elStatorNull{el?.Stator == null}");
                            controlPart.BaseMap = el == null ? az : az == null ? el : az.Stator.TopGrid == el.Stator.CubeGrid ? az : el;
                            if (controlPart.BaseMap == null)
                                continue;
                        }

                        if (!cComp.Data.Repo.Values.Set.Overrides.AiEnabled)
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        var root = controlPart.BaseMap;

                        var topGrid = root.Stator.TopGrid as MyCubeGrid;

                        if (topGrid == null)
                        {
                            controlPart.BaseHasTop = false;
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }
                        if (!controlPart.BaseHasTop)
                        {
                            controlPart.BaseHasTop = true;
                        }

                        List<StatorMap> turretMap;
                        if (!rootConstruct.LocalStatorMaps.TryGetValue(topGrid, out turretMap))
                        {
                            if (Tick - cComp.LastCrashTick > 1200)
                            {
                                cComp.LastCrashTick = Tick;
                                Log.Line($"CTC failed to find stator in LocalStatorMaps - entId:{cComp.CoreEntity.EntityId}");
                            }
                            continue;
                        }

                        if (turretMap.Count == 0)
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        if (Tick120)
                        {
                            for (int j = 0; j < turretMap.Count; j++)
                            {
                                var map = turretMap[j];
                                if (map.Stator.TopGrid == null || map.RotorMap.PrimaryWeapon != null)
                                    continue;

                                for (int k = 0; k < map.TopAi.WeaponComps.Count; k++)
                                {
                                    var wComp = map.TopAi.WeaponComps[k];
                                    if (wComp.IsFunctional)
                                    {
                                        map.RotorMap.PrimaryWeapon = wComp.TrackingWeapon;
                                        map.RotorMap.Scope = wComp.TrackingWeapon.GetScope;
                                        Log.Line($"Set primary weapon and scope");
                                        break;
                                    }

                                }
                            }
                        }
                        var trackingComp = controlPart.TrackingWeapon?.Comp;
                        if (trackingComp == null || !trackingComp.IsFunctional || !trackingComp.IsWorking || trackingComp.IsDisabled)
                        {
                            if (controlPart.NoValidWeapons && !Tick60)
                            {
                                if (cComp.RotorsMoving)
                                    cComp.StopRotors();
                                continue;
                            }

                            if (controlPart.TrackingWeapon != null)
                            {
                                controlPart.TrackingWeapon.MasterComp = null;
                                controlPart.TrackingWeapon.RotorTurretTracking = false;
                                controlPart.TrackingMap = null;
                                controlPart.TrackingWeapon = null;
                                controlPart.TrackingScope = null;
                            }

                            var foundWeapon = false;
                            for (int j = 0; j < turretMap.Count; j++)
                            {
                                if (foundWeapon)
                                    break;

                                var map = turretMap[j];
                                if (map.TopAi.WeaponComps.Count == 0)
                                    continue;

                                for (int k = 0; k < map.TopAi.WeaponComps.Count; k++)
                                {
                                    var wComp = map.TopAi.WeaponComps[k];
                                    if (wComp.IsFunctional && wComp.IsWorking && !wComp.IsDisabled)
                                    {
                                        controlPart.TrackingMap = map;
                                        controlPart.TrackingWeapon = wComp.TrackingWeapon;
                                        controlPart.TrackingScope = wComp.TrackingWeapon.GetScope;
                                        controlPart.TrackingWeapon.MasterComp = cComp;
                                        controlPart.TrackingWeapon.RotorTurretTracking = true;
                                        controlPart.NoValidWeapons = false;

                                        Log.Line("Set tracking weapon");
                                        foundWeapon = true;
                                        break;
                                    }

                                }
                            }

                            if (!foundWeapon)
                            {
                                controlPart.NoValidWeapons = true;

                                if (cComp.RotorsMoving)
                                    cComp.StopRotors();
                                continue;
                            }

                        }

                        var trackingWeapon = controlPart.TrackingWeapon;
                        if (trackingWeapon?.Target == null)
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }
                        var target = trackingWeapon.Target;

                        if (target.TargetState != cComp.OldState)
                        {
                            cComp.OldState = target.TargetState;
                            Log.Line($"New target state: {target.TargetState}");
                        }

                        if (!target.HasTarget || target.TargetState == TargetStates.None)
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();

                            continue;
                        }

                        Vector3D desiredDirection;
                        var targetDistSqr = 0d;

                        if (target.TargetState == TargetStates.IsEntity)
                        {
                            var targetEnt = target.TargetEntity;
                            if (targetEnt == null)
                            {
                                if (cComp.RotorsMoving)
                                    cComp.StopRotors();
                                continue;
                            }

                            targetDistSqr = Vector3D.DistanceSquared(root.Stator.PositionComp.WorldAABB.Center, targetEnt.PositionComp.WorldAABB.Center);

                            if (!ControlSys.TrajectoryEstimation(trackingWeapon, targetEnt, out desiredDirection))
                            {
                                if (cComp.RotorsMoving)
                                    cComp.StopRotors();
                                continue;
                            }
                        }
                        else
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();

                            continue;
                        }

                        cComp.RotorsMoving = true;

                        var epsilon = Tick120 ? 1E-06d : targetDistSqr <= 640000 ? 1E-03d : targetDistSqr <= 3240000 ? 1E-04d : 1E-05d;

                        var currentDirection = controlPart.TrackingScope.Info.Direction;
                        var axis = Vector3D.Cross(desiredDirection, currentDirection);
                        var deviationRads = MathHelper.ToRadians(controller.AngleDeviation);

                        //Root control
                        var up = root.Stator.PositionComp.WorldMatrixRef.Up;
                        var upZero = Vector3D.IsZero(up);
                        var desiredFlat = upZero || Vector3D.IsZero(desiredDirection) ? Vector3D.Zero : desiredDirection - desiredDirection.Dot(up) * up;
                        var currentFlat = upZero || Vector3D.IsZero(currentDirection) ? Vector3D.Zero : currentDirection - currentDirection.Dot(up) * up;
                        var rootAngle = Vector3D.IsZero(desiredFlat) || Vector3D.IsZero(currentFlat) ? 0 : Math.Acos(MathHelper.Clamp(desiredFlat.Dot(currentFlat) / Math.Sqrt(desiredFlat.LengthSquared() * currentFlat.LengthSquared()), -1, 1));

                        var rootOutsideLimits = false;
                        if (MyUtils.IsZero(rootAngle, (float)epsilon))
                        {
                            if (IsServer)
                                root.Stator.TargetVelocityRad = 0;
                        }
                        else
                        {
                            rootAngle *= Math.Sign(Vector3D.Dot(axis, up));
                            var desiredAngle = root.Stator.Angle + rootAngle;
                            rootOutsideLimits = desiredAngle < root.Stator.LowerLimitRad && desiredAngle + MathHelper.TwoPi > root.Stator.UpperLimitRad;

                            if ((desiredAngle < root.Stator.LowerLimitRad && desiredAngle + MathHelper.TwoPi < root.Stator.UpperLimitRad) || (desiredAngle > root.Stator.UpperLimitRad && desiredAngle - MathHelper.TwoPi > root.Stator.LowerLimitRad))
                                rootAngle = -Math.Sign(rootAngle) * (MathHelper.TwoPi - Math.Abs(rootAngle));

                            if (IsServer)
                                root.Stator.TargetVelocityRad = rootOutsideLimits ? 0 : Math.Abs(controller.VelocityMultiplierAzimuthRpm) * (float)rootAngle;
                        }

                        for (int j = 0; j < turretMap.Count; j++)
                        {
                            var map = turretMap[j];
                            if (map.RotorMap.Scope == null)
                                continue;

                            currentDirection = map.RotorMap.Scope.Info.Direction;
                            up = map.Stator.PositionComp.WorldMatrixRef.Up;
                            upZero = Vector3D.IsZero(up);
                            desiredFlat = upZero || Vector3D.IsZero(desiredDirection) ? Vector3D.Zero : desiredDirection - desiredDirection.Dot(up) * up;
                            currentFlat = upZero || Vector3D.IsZero(currentDirection) ? Vector3D.Zero : currentDirection - currentDirection.Dot(up) * up;
                            var subAngle = Vector3D.IsZero(desiredFlat) || Vector3D.IsZero(currentFlat) ? 0 : Math.Acos(MathHelper.Clamp(desiredFlat.Dot(currentFlat) / Math.Sqrt(desiredFlat.LengthSquared() * currentFlat.LengthSquared()), -1, 1));

                            if (MyUtils.IsZero(subAngle, (float)epsilon) || !rootOutsideLimits && Math.Abs(rootAngle) > MathHelper.PiOver2)
                            {
                                if (Tick60) Log.Line($"secondary isZero {MyUtils.IsZero(subAngle, (float)epsilon)} >2pi {Math.Abs(rootAngle) > MathHelper.PiOver2}");
                                if (IsServer)
                                    map.Stator.TargetVelocityRad = 0;
                            }
                            else
                            {
                                subAngle *= Math.Sign(Vector3D.Dot(axis, up));
                                var desiredAngle = map.Stator.Angle + subAngle;
                                var subOutsideLimits = desiredAngle < map.Stator.LowerLimitRad && desiredAngle + MathHelper.TwoPi > map.Stator.UpperLimitRad;

                                if ((desiredAngle < map.Stator.LowerLimitRad && desiredAngle + MathHelper.TwoPi < map.Stator.UpperLimitRad) || (desiredAngle > map.Stator.UpperLimitRad && desiredAngle - MathHelper.TwoPi > map.Stator.LowerLimitRad))
                                    subAngle = -Math.Sign(subAngle) * (MathHelper.TwoPi - Math.Abs(subAngle));

                                if (IsServer)
                                    map.Stator.TargetVelocityRad = subOutsideLimits ? 0 : Math.Abs(controller.VelocityMultiplierElevationRpm) * (float)subAngle;
                            }

                            if (rootAngle * rootAngle + subAngle * subAngle < deviationRads * deviationRads)
                            {
                                map.TopAi.RotorTurretAimed = true;
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    Log.Line($"Caught exception in Control loop: {ex}");
                }

                ///
                /// Upgrade update section
                ///
                for (int i = 0; i < ai.UpgradeComps.Count; i++)
                {
                    var uComp = ai.UpgradeComps[i];
                    if (uComp.Status != Started)
                        uComp.HealthCheck();

                    if (ai.DbUpdated || !uComp.UpdatedState)
                    {
                        uComp.DetectStateChanges();
                    }

                    if (uComp.Platform.State != CorePlatform.PlatformState.Ready || uComp.IsAsleep || !uComp.IsWorking || uComp.CoreEntity.MarkedForClose || uComp.IsDisabled || uComp.LazyUpdate && !ai.DbUpdated && Tick > uComp.NextLazyUpdateStart)
                        continue;

                    for (int j = 0; j < uComp.Platform.Upgrades.Count; j++)
                    {
                        var u = uComp.Platform.Upgrades[j];
                    }
                }
                ///
                /// Support update section
                ///
                for (int i = 0; i < ai.SupportComps.Count; i++)
                {
                    var sComp = ai.SupportComps[i];
                    if (sComp.Status != Started)
                        sComp.HealthCheck();

                    if (ai.DbUpdated || !sComp.UpdatedState)
                    {
                        sComp.DetectStateChanges();
                    }

                    if (sComp.Platform.State != CorePlatform.PlatformState.Ready || sComp.IsAsleep || !sComp.IsWorking || sComp.CoreEntity.MarkedForClose || sComp.IsDisabled || !Tick60)
                        continue;

                    for (int j = 0; j < sComp.Platform.Support.Count; j++)
                    {
                        var s = sComp.Platform.Support[j];
                        if (s.LastBlockRefreshTick < ai.LastBlockChangeTick && s.IsPrime || s.LastBlockRefreshTick < ai.LastBlockChangeTick && !sComp.Structure.CommonBlockRange)
                            s.RefreshBlocks();

                        if (s.ShowAffectedBlocks != sComp.Data.Repo.Values.Set.Overrides.ArmorShowArea)
                            s.ToggleAreaEffectDisplay();

                        if (s.Active)
                            s.Charge();
                    }
                }

                ///
                /// Phantom update section
                ///
                for (int i = 0; i < ai.PhantomComps.Count; i++)
                {
                    var pComp = ai.PhantomComps[i];

                    if (pComp.CloseCondition || pComp.HasCloseConsition && pComp.AllWeaponsOutOfAmmo()) {
                        if (!pComp.CloseCondition) 
                            pComp.ForceClose(pComp.SubtypeName);
                        continue;
                    }
                    if (pComp.Status != Started)
                        pComp.HealthCheck();
                    var burstShots = (pComp.ShootManager.RequestShootBurstId != pComp.Data.Repo.Values.State.ShootSyncStateId);
                    if (pComp.Platform.State != CorePlatform.PlatformState.Ready || pComp.IsDisabled || pComp.IsAsleep || pComp.CoreEntity.MarkedForClose || pComp.LazyUpdate && !ai.DbUpdated && Tick > pComp.NextLazyUpdateStart)
                        continue;

                    if (ai.DbUpdated || !pComp.UpdatedState) {
                        pComp.DetectStateChanges();
                    }

                    ///
                    /// Phantom update section
                    /// 
                    for (int j = 0; j < pComp.Platform.Phantoms.Count; j++)
                    {
                        var p = pComp.Platform.Phantoms[j];
                        if (p.ActiveAmmoDef.AmmoDef.Const.Reloadable && !p.System.DesignatorWeapon && !p.Loading) { 

                            if (IsServer && (p.ProtoWeaponAmmo.CurrentAmmo == 0 || p.CheckInventorySystem))
                                p.ComputeServerStorage();
                            else if (IsClient) {

                                if (p.ClientReloading && p.Reload.EndId > p.ClientEndId && p.Reload.StartId == p.ClientStartId)
                                    p.Reloaded();
                                else
                                    p.ClientReload();
                            }
                        }
                        else if (p.Loading && Tick >= p.ReloadEndTick)
                            p.Reloaded(1);

                        var reloading = p.ActiveAmmoDef.AmmoDef.Const.Reloadable && p.ClientMakeUpShots == 0 && (p.Loading || p.ProtoWeaponAmmo.CurrentAmmo == 0);
                        var canShoot = !p.PartState.Overheated && !reloading && !p.System.DesignatorWeapon;

                        var autoShot =  p.PartState.Action == TriggerOn || p.AiShooting && p.PartState.Action == TriggerOff;

                        var anyShot = autoShot && !burstShots || p.ShootCount > 0;

                        var delayedFire = p.System.DelayCeaseFire && !p.Target.IsAligned && Tick - p.CeaseFireDelayTick <= p.System.CeaseFireDelay;
                        var shoot = (anyShot || p.FinishShots || delayedFire);
                        var shotReady = canShoot && (shoot || p.LockOnFireState);

                        if (shotReady) {
                            p.Shoot();
                        }
                        else {

                            if (p.IsShooting)
                                p.StopShooting();

                            if (p.BarrelSpinning) {

                                var spinDown = !(shotReady && ai.CanShoot && p.System.Values.HardPoint.Loading.SpinFree);
                                p.SpinBarrel(spinDown);
                            }
                        }
                    }
                }

                ///
                /// WeaponComp update section
                ///
                for (int i = 0; i < ai.WeaponComps.Count; i++) {

                    var wComp = ai.WeaponComps[i];

                    if (wComp.Status != Started)
                        wComp.HealthCheck();

                    if (ai.DbUpdated || !wComp.UpdatedState) {

                        wComp.DetectStateChanges();
                    }

                    var wValues = wComp.Data.Repo.Values;

                    var focusTargets = wValues.Set.Overrides.FocusTargets;

                    if (IsServer && wValues.State.PlayerId > 0 && !rootConstruct.ControllingPlayers.ContainsKey(wValues.State.PlayerId))
                        wComp.ResetPlayerControl();
                    else if (IsClient && ai.GridMap.LastControllerTick ==  Tick && wComp.ShootManager.ShootToggled && wComp.Data.Repo.Values.State.PlayerId > 0)
                        wComp.ShootManager.RequestShootSync(PlayerId);

                    if (wComp.Platform.State != CorePlatform.PlatformState.Ready || wComp.IsDisabled || wComp.IsAsleep || !wComp.IsWorking || wComp.CoreEntity.MarkedForClose || wComp.LazyUpdate && !ai.DbUpdated && Tick > wComp.NextLazyUpdateStart)
                        continue;

                    var cMode = wValues.Set.Overrides.Control;
                    var sMode = wValues.Set.Overrides.ShootMode;
                    var shootModeDefault = sMode != Weapon.ShootManager.ShootModes.AiShoot;
                    var shotModeActive = wComp.ShootManager.RequestShootBurstId != wValues.State.ShootSyncStateId;

                    if (HandlesInput) {

                        if (wComp.TypeSpecific == CoreComponent.CompTypeSpecific.Rifle && wValues.State.Control != ControlMode.Ui)
                            wComp.RequestShootUpdate(TriggerClick, PlayerId);

                        var wasTrack = wValues.State.TrackingReticle;

                        var isControllingPlayer = wValues.State.PlayerId == PlayerId;

                        var track = (isControllingPlayer && (cMode != ProtoWeaponOverrides.ControlModes.Auto) && TargetUi.DrawReticle && !InMenu && rootConstruct.ControllingPlayers.ContainsKey(PlayerId) && (!UiInput.CameraBlockView || UiInput.CameraChannelId > 0 && UiInput.CameraChannelId == wComp.Data.Repo.Values.Set.Overrides.CameraChannel));
                        if (isControllingPlayer)
                        {
                            TargetUi.LastTrackTick = Tick;
                            
                            if (cMode == ProtoWeaponOverrides.ControlModes.Manual)
                                TargetUi.LastManualTick = Tick;

                            if (MpActive && wasTrack != track)
                                wComp.Session.SendTrackReticleUpdate(wComp, track);
                            else if (IsServer)
                                wValues.State.TrackingReticle = track;

                            if (sMode == Weapon.ShootManager.ShootModes.MouseControl && (!wComp.ShootManager.ShootToggled && UiInput.MouseButtonLeftNewPressed || wComp.ShootManager.ShootToggled && UiInput.MouseButtonLeftReleased))
                            {
                                wComp.ShootManager.RequestShootSync(PlayerId);
                            }
                        }
                    }

                    wComp.ManualMode = wValues.State.TrackingReticle && cMode == ProtoWeaponOverrides.ControlModes.Manual;

                    Ai.FakeTargets fakeTargets = null;
                    if (wComp.ManualMode || cMode == ProtoWeaponOverrides.ControlModes.Painter)
                        PlayerDummyTargets.TryGetValue(wValues.State.PlayerId, out fakeTargets);

                    wComp.PainterMode = fakeTargets != null && cMode == ProtoWeaponOverrides.ControlModes.Painter && fakeTargets.PaintedTarget.EntityId != 0;
                    wComp.FakeMode = wComp.ManualMode || wComp.PainterMode;
                    
                    wComp.WasControlled = wComp.UserControlled;
                    wComp.UserControlled = wValues.State.Control != ControlMode.None && (cMode != ProtoWeaponOverrides.ControlModes.Auto || wValues.State.Control == ControlMode.Camera || fakeTargets != null && fakeTargets.PaintedTarget.EntityId != 0);
                    
                    if (!PlayerMouseStates.TryGetValue(wValues.State.PlayerId, out wComp.InputState))
                        wComp.InputState = DefaultInputStateData;

                    var compManualMode = wValues.State.Control == ControlMode.Camera || wComp.ManualMode;
                    var canManualShoot = !ai.SuppressMouseShoot && !wComp.InputState.InMenu;

                    if (Tick60) {
                        var add = wComp.TotalEffect - wComp.PreviousTotalEffect;
                        wComp.AddEffect = add > 0 ? add : wComp.AddEffect;
                        wComp.AverageEffect = wComp.DamageAverage.Add((int)add);
                        wComp.PreviousTotalEffect = wComp.TotalEffect;
                    }

                    ///
                    /// Weapon update section
                    ///
                    for (int j = 0; j < wComp.Platform.Weapons.Count; j++) {

                        var w = wComp.Platform.Weapons[j];

                        if (w.PartReadyTick > Tick) {

                            if (w.Target.HasTarget && !IsClient)
                                w.Target.Reset(Tick, States.WeaponNotReady);
                            continue;
                        }
                        if (w.AvCapable && Tick20) {
                            var avWasEnabled = w.PlayTurretAv;
                            double distSqr;
                            var pos = w.Comp.CoreEntity.PositionComp.WorldAABB.Center;
                            Vector3D.DistanceSquared(ref CameraPos, ref pos, out distSqr);
                            w.PlayTurretAv = distSqr < w.System.HardPointAvMaxDistSqr;
                            if (avWasEnabled != w.PlayTurretAv) w.StopBarrelAvTick = Tick;
                        }

                        ///
                        ///Check Reload
                        ///                        

                        var aConst = w.ActiveAmmoDef.AmmoDef.Const;
                        if (aConst.Reloadable && !w.System.DesignatorWeapon && !w.Loading) { // does this need StayCharged?

                            if (IsServer)
                            {
                                if (w.ProtoWeaponAmmo.CurrentAmmo == 0 || w.CheckInventorySystem)
                                    w.ComputeServerStorage();
                            }
                            else if (IsClient) {

                                if (w.ClientReloading && w.Reload.EndId > w.ClientEndId && w.Reload.StartId == w.ClientStartId)
                                    w.Reloaded(5);
                                else 
                                    w.ClientReload();
                            }
                        }
                        else if (w.Loading && (IsServer && Tick >= w.ReloadEndTick || IsClient && !w.Charging && w.Reload.EndId > w.ClientEndId))
                            w.Reloaded(1);

                        if (DedicatedServer && w.Reload.WaitForClient && !w.Loading && (wValues.State.PlayerId <= 0 || Tick - w.LastLoadedTick > 60))
                            SendWeaponReload(w, true);

                        ///
                        /// Update Weapon Hud Info
                        /// 

                        var addWeaponToHud = HandlesInput && (w.HeatPerc >= 0.01 || (w.ShowReload && (w.Loading || w.Reload.WaitForClient)) || (w.System.LockOnFocus && !w.Comp.ModOverride && construct.Data.Repo.FocusData.Locked != FocusData.LockModes.Locked) || (aConst.CanReportTargetStatus && wValues .Set.ReportTarget && !w.Target.HasTarget && wValues.Set.Overrides.Grids && (wComp.DetectOtherSignals && ai.DetectionInfo.OtherInRange || ai.DetectionInfo.PriorityInRange) && ai.DetectionInfo.TargetInRange(w)));

                        if (addWeaponToHud && !Session.Config.MinimalHud && ActiveControlBlock != null && ai.SubGridCache.Contains(ActiveControlBlock.CubeGrid)) {
                            HudUi.TexturesToAdd++;
                            HudUi.WeaponsToDisplay.Add(w);
                        }

                        //if (w.System.PartType != HardwareType.BlockWeapon)
                            //continue;

                        if (w.CriticalReaction && !wComp.CloseCondition && (wValues.Set.Overrides.Armed || wValues.State.CountingDown || wValues.State.CriticalReaction))
                            w.CriticalMonitor();

                        if (w.Target.ClientDirty)
                            w.Target.ClientUpdate(w, w.TargetData);

                        ///
                        /// Check target for expire states
                        /// 
                        var noAmmo = w.NoMagsToLoad && w.ProtoWeaponAmmo.CurrentAmmo == 0 && aConst.Reloadable && !w.System.DesignatorWeapon && Tick - w.LastMagSeenTick > 600;
                        if (w.Target.HasTarget) {

                            
                            if (!IsClient && noAmmo)
                                w.Target.Reset(Tick, States.Expired);
                            else if (!IsClient && w.Target.TargetEntity == null && w.Target.Projectile == null && !wComp.FakeMode || wComp.ManualMode && (fakeTargets == null || Tick - fakeTargets.ManualTarget.LastUpdateTick > 120))
                                w.Target.Reset(Tick, States.Expired, !wComp.ManualMode);
                            else if (!IsClient && w.Target.TargetEntity != null && (wComp.UserControlled && !w.System.SuppressFire || w.Target.TargetEntity.MarkedForClose || Tick60 && (focusTargets && !focus.ValidFocusTarget(w) || Tick60 && !focusTargets && !w.TurretController && (aConst.RequiresTarget || w.RotorTurretTracking) && !w.TargetInRange(w.Target.TargetEntity))))
                                w.Target.Reset(Tick, States.Expired);
                            else if (!IsClient && w.Target.Projectile != null && (!ai.LiveProjectile.Contains(w.Target.Projectile) || w.Target.TargetState == TargetStates.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive)) {
                                w.Target.Reset(Tick, States.Expired);
                                w.FastTargetResetTick = Tick + 6;
                            }
                            else if (w.TurretController) {

                                if (!advOptimize && !Weapon.TrackingTarget(w, w.Target, out w.TargetLock) && !IsClient && w.Target.ExpiredTick != Tick)
                                    w.Target.Reset(Tick, States.LostTracking, !wComp.ManualMode && (w.Target.CurrentState != States.RayCheckFailed && !w.Target.HasTarget));
                            }
                            else {

                                Vector3D targetPos;
                                if (w.TurretAttached) {

                                    if (!w.System.TrackTargets && !IsClient) {

                                        var trackingWeaponIsFake = wComp.TrackingWeapon.Target.TargetState == TargetStates.IsFake;
                                        var thisWeaponIsFake = w.Target.TargetState == TargetStates.IsFake;
                                        if ((wComp.TrackingWeapon.Target.Projectile != w.Target.Projectile || w.Target.TargetState == TargetStates.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive || wComp.TrackingWeapon.Target.TargetEntity != w.Target.TargetEntity || trackingWeaponIsFake != thisWeaponIsFake))
                                            w.Target.Reset(Tick, States.Expired);
                                        else
                                            w.TargetLock = true;
                                    }
                                    else if (!Weapon.TargetAligned(w, w.Target, out targetPos) && !IsClient)
                                        w.Target.Reset(Tick, States.Expired);
                                }
                                else if (w.System.TrackTargets && !Weapon.TargetAligned(w, w.Target, out targetPos) && !IsClient)
                                    w.Target.Reset(Tick, States.Expired);
                            }
                        }

                        w.ProjectilesNear = enemyProjectiles && w.System.TrackProjectile && wValues.Set.Overrides.Projectiles && !w.Target.HasTarget && (w.Target.TargetChanged || QCount == w.ShortLoadId );

                        if (wValues.State.Control == ControlMode.Camera && UiInput.MouseButtonPressed)
                            w.Target.TargetPos = Vector3D.Zero;

                        ///
                        /// Queue for target acquire or set to tracking weapon.
                        /// 
                        

                        var seek = wComp.FakeMode && w.Target.TargetState != TargetStates.IsFake || (aConst.RequiresTarget || w.RotorTurretTracking) & !w.Target.HasTarget && !noAmmo && (wComp.DetectOtherSignals && ai.DetectionInfo.OtherInRange || ai.DetectionInfo.PriorityInRange) && (!wComp.UserControlled && !enforcement.DisableAi || w.PartState.Action == TriggerClick);
                        
                        if (!IsClient && (seek || (aConst.RequiresTarget || w.RotorTurretTracking) && rootConstruct.TargetResetTick == Tick && !wComp.UserControlled && !enforcement.DisableAi) && !w.AcquiringTarget && wValues.State.Control != ControlMode.Camera)
                        {
                            w.AcquiringTarget = true;
                            AcquireTargets.Add(w);
                        }

                        if (w.Target.TargetChanged) // Target changed
                            w.TargetChanged();

                        ///
                        /// Check weapon's turret to see if its time to go home
                        ///

                        if (w.TurretController && !w.IsHome && !w.ReturingHome && !w.Target.HasTarget && Tick - w.Target.ResetTick > 239 && !wComp.UserControlled && w.PartState.Action == TriggerOff)
                            w.ScheduleWeaponHome();

                        ///
                        /// Determine if its time to shoot
                        ///
                        ///
                        w.AiShooting = !wComp.UserControlled && !w.System.SuppressFire && (w.TargetLock || ai.RotorTurretAimed);

                        var reloading = aConst.Reloadable && w.ClientMakeUpShots == 0 && (w.Loading || w.ProtoWeaponAmmo.CurrentAmmo == 0 || w.Reload.WaitForClient);
                        var canShoot = !w.PartState.Overheated && !reloading && !w.System.DesignatorWeapon;
                        var paintedTarget = wComp.PainterMode && w.Target.TargetState == TargetStates.IsFake && w.Target.IsAligned;
                        
                        var autoShot = paintedTarget || w.PartState.Action == TriggerOn || w.AiShooting && w.PartState.Action == TriggerOff;
                        var manualShot = (compManualMode || w.PartState.Action == TriggerClick) && canManualShoot && wComp.InputState.MouseButtonLeft;
                        var normalShot = (manualShot || autoShot);

                        var anyShot = (normalShot && !shotModeActive && !shootModeDefault) || ((w.ShootCount > 0 && w.ShootDelay == 0 || w.ShootDelay != 0 && w.ShootDelay-- == 0) && !wComp.ShootManager.FreezeClientShoot);

                        var delayedFire = w.System.DelayCeaseFire && !w.Target.IsAligned && Tick - w.CeaseFireDelayTick <= w.System.CeaseFireDelay;
                        var shootRequest = (anyShot || w.FinishShots || delayedFire);

                        w.LockOnFireState = shootRequest && (w.System.LockOnFocus && !w.Comp.ModOverride) && construct.Data.Repo.FocusData.HasFocus && focus.FocusInRange(w);
                        var shotReady = canShoot && (shootRequest && (!w.System.LockOnFocus || w.Comp.ModOverride) || w.LockOnFireState);
                        var shoot = shotReady && ai.CanShoot && (!aConst.RequiresTarget || w.Target.HasTarget || wValues.Set.Overrides.Override || compManualMode);

                        if (shoot) {

                            if (MpActive && HandlesInput && !ManualShot)
                                ManualShot = !autoShot && !w.FinishShots && !delayedFire;

                            if (w.System.DelayCeaseFire && (autoShot || manualShot || w.FinishShots))
                                w.CeaseFireDelayTick = Tick;

                            ShootingWeapons.Add(w);
                        }
                        else {

                            if (w.IsShooting || w.PreFired)
                                w.StopShooting();

                            if (w.BarrelSpinning) {
                                var spinDown = !(shotReady && ai.CanShoot && w.System.WConst.SpinFree);
                                w.SpinBarrel(spinDown);
                            }
                        }

                        if (w.TurretController) {
                            w.TurretActive = w.Target.HasTarget;
                            if (advOptimize && w.TurretActive)
                                activeTurret = true;
                        }

                        w.TargetLock = false;

                        if (wComp.Debug && !DedicatedServer)
                            WeaponDebug(w);
                    }
                }
                
                if (ai.AiType == Ai.AiTypes.Grid && Tick60 && ai.BlockChangeArea != BoundingBox.Invalid) {
                    ai.BlockChangeArea = BoundingBox.CreateInvalid();
                    ai.AddedBlockPositions.Clear();
                    ai.RemovedBlockPositions.Clear();
                }
                ai.DbUpdated = false;
                ai.RotorTurretAimed = false;
                if (activeTurret)
                    AimingAi.Add(ai);

                //if (Tick - _vanillaTurretTick < 3)
                //    ai.ResetMyGridTargeting();
            }

            if (DbTask.IsComplete && DbsToUpdate.Count > 0 && !DbUpdating)
                UpdateDbsInQueue();
        }

        private void AimAi()
        {
            var aiCount = AimingAi.Count;
            var stride = aiCount < 32 ? 1 : 2;

            MyAPIGateway.Parallel.For(0, aiCount, i =>
            {
                var ai = AimingAi[i];
                for (int j = 0; j < ai.TrackingComps.Count; j++)
                {
                    var wComp = ai.TrackingComps[j];
                    for (int k = 0; k < wComp.Platform.Weapons.Count; k++)
                    {
                        var w = wComp.Platform.Weapons[k];
                        if (!w.TurretActive || !ai.AiInit || ai.MarkedForClose || ai.Concealed || w.Comp.Ai == null || ai.TopEntity == null || ai.Construct.RootAi == null || w.Comp.CoreEntity == null  || wComp.IsDisabled || wComp.IsAsleep || !wComp.IsWorking || ai.TopEntity.MarkedForClose || wComp.CoreEntity.MarkedForClose || w.Comp.Platform.State != CorePlatform.PlatformState.Ready) continue;

                        if (!Weapon.TrackingTarget(w, w.Target, out w.TargetLock) && !IsClient && w.Target.ExpiredTick != Tick)
                            w.Target.Reset(Tick, States.LostTracking, !w.Comp.ManualMode && (w.Target.CurrentState != States.RayCheckFailed && !w.Target.HasTarget));
                    }
                }

            },
                stride);

            AimingAi.Clear();
        }

        private void CheckAcquire()
        {
            for (int i = AcquireTargets.Count - 1; i >= 0; i--)
            {
                var w = AcquireTargets[i];
                var comp = w.Comp;
                var overrides = w.MasterComp?.Data?.Repo?.Values?.Set?.Overrides ?? comp.Data.Repo.Values.Set.Overrides;
                if (w.BaseComp.IsAsleep || w.BaseComp.Ai == null || comp.Ai.TopEntity.MarkedForClose || comp.Ai.IsGrid && !comp.Ai.HasPower || comp.Ai.Concealed || comp.CoreEntity.MarkedForClose || !comp.Ai.DbReady || !comp.IsWorking || w.NoMagsToLoad && w.ProtoWeaponAmmo.CurrentAmmo == 0 && Tick - w.LastMagSeenTick > 600) {

                    w.AcquiringTarget = false;
                    AcquireTargets.RemoveAtFast(i);
                    continue;
                }

                if (!w.Acquire.Monitoring && IsServer && w.System.HasRequiresTarget)
                    AcqManager.Monitor(w.Acquire);

                var acquire = (w.Acquire.IsSleeping && AsleepCount == w.Acquire.SlotId || !w.Acquire.IsSleeping && AwakeCount == w.Acquire.SlotId);

                var seekProjectile = w.ProjectilesNear || w.System.TrackProjectile && overrides.Projectiles && w.BaseComp.Ai.CheckProjectiles;
                var checkTime = w.Target.TargetChanged || acquire || seekProjectile || w.FastTargetResetTick == Tick;
                var ai = w.BaseComp.Ai;

                if (checkTime || ai.Construct.RootAi.Construct.TargetResetTick == Tick && w.Target.HasTarget) {

                    if (seekProjectile || comp.Data.Repo.Values.State.TrackingReticle || (comp.DetectOtherSignals && ai.DetectionInfo.OtherInRange || ai.DetectionInfo.PriorityInRange) && ai.DetectionInfo.ValidSignalExists(w))
                    {
                        if (comp.TrackingWeapon != null && comp.TrackingWeapon.System.DesignatorWeapon && comp.TrackingWeapon != w && comp.TrackingWeapon.Target.HasTarget) {

                            var topMost = comp.TrackingWeapon.Target.TargetEntity?.GetTopMostParent();
                            Ai.AcquireTarget(w, false, topMost, overrides);
                        }
                        else
                        {
                            Ai.AcquireTarget(w, ai.Construct.RootAi.Construct.TargetResetTick == Tick, null, overrides);
                        }
                    }

                    if (w.Target.HasTarget || !(comp.DetectOtherSignals && ai.DetectionInfo.OtherInRange || ai.DetectionInfo.PriorityInRange)) {

                        w.AcquiringTarget = false;
                        AcquireTargets.RemoveAtFast(i);
                        if (w.Target.HasTarget && MpActive) {
                            w.Target.PushTargetToClient(w);
                        }
                    }
                }
            }
        }

        private void ShootWeapons()
        {
            for (int i = ShootingWeapons.Count - 1; i >= 0; i--) {
                
                var w = ShootingWeapons[i];
                var invalidWeapon = w.Comp.CoreEntity.MarkedForClose || w.Comp.Ai == null || w.Comp.Ai.Concealed || w.Comp.Ai.TopEntity.MarkedForClose || w.Comp.Platform.State != CorePlatform.PlatformState.Ready;
                var smartTimer = w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance == Smart && (QCount == w.ShortLoadId && (w.Target.HasTarget || w.LockOnFireState) && Tick - w.LastSmartLosCheck > 240 || Tick - w.LastSmartLosCheck > 1200);
                var quickSkip = invalidWeapon || w.Comp.IsBlock && smartTimer && !w.SmartLos() || w.PauseShoot || (w.ProtoWeaponAmmo.CurrentAmmo == 0 && w.ClientMakeUpShots == 0) && w.ActiveAmmoDef.AmmoDef.Const.Reloadable;
                if (quickSkip) continue;

                w.Shoot();
            }
            ShootingWeapons.Clear();
        }

        private void GroupUpdates()
        {
            for (int i = 0; i < GridGroupUpdates.Count; i++)
                GridGroupUpdates[i].UpdateAis();

            GridGroupUpdates.Clear();
        }
    }
}
