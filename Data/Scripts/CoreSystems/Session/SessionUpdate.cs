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

                        var anyShot = autoShot;

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
                            cComp.DetectStateChanges();

                        if (cComp.Platform.State != CorePlatform.PlatformState.Ready || cComp.IsDisabled || cComp.IsAsleep || !cComp.IsWorking || cComp.CoreEntity.MarkedForClose || cComp.LazyUpdate && !ai.DbUpdated && Tick > cComp.NextLazyUpdateStart)
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        var az = (IMyMotorStator)cComp.Controller.AzimuthRotor;
                        var el = (IMyMotorStator)cComp.Controller.ElevationRotor;

                        if (az == null || el == null)
                            continue;

                        var controlPart = cComp.Platform.Control;

                        controlPart.BaseMap = az.TopGrid == el.CubeGrid ? az : el;
                        controlPart.OtherMap = controlPart.BaseMap == az ? el : az;
                        var topGrid = controlPart.BaseMap.TopGrid as MyCubeGrid;
                        var otherGrid = controlPart.OtherMap.TopGrid as MyCubeGrid;

                        Ai topAi;
                        if (controlPart.BaseMap == null || controlPart.OtherMap == null || (topGrid == null || !EntityAIs.TryGetValue(topGrid, out topAi)) && (otherGrid == null || !EntityAIs.TryGetValue(otherGrid, out topAi)))
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        if (cComp.Controller.IsUnderControl)
                        {
                            Ai.PlayerController pControl;
                            if (HandlesInput && rootConstruct.ControllingPlayers.TryGetValue(PlayerId, out pControl) && pControl.ControllBlock == cComp.CoreEntity && PlayerMouseStates.TryGetValue(cComp.Data.Repo.Values.State.PlayerId, out cComp.InputState) && cComp.InputState.MouseButtonLeft)
                            {
                                for (int j = 0; j < topAi.WeaponComps.Count; j++)
                                {
                                    var w = topAi.WeaponComps[j];
                                    w.ShootManager.RequestShootSync(PlayerId);
                                }
                            }
                            cComp.RotorsMoving = true;
                            continue;
                        }

                        if (!cComp.Data.Repo.Values.Set.Overrides.AiEnabled || topAi.RootWeaponComp?.TrackingWeapon == null || topAi.RootWeaponComp.TrackingWeapon.Comp.CoreEntity.MarkedForClose)
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        controlPart.TrackingWeapon = topAi.RootWeaponComp.TrackingWeapon;
                        controlPart.TrackingWeapon.MasterComp = cComp;
                        controlPart.TrackingWeapon.RotorTurretTracking = true;

                        var desiredDirection = Vector3D.Zero;
                        var noTarget = false;

                        if (controlPart.TrackingWeapon.Target.TargetState != TargetStates.IsEntity)
                            noTarget = true;
                        else if (!ControlSys.TrajectoryEstimation(controlPart.TrackingWeapon, out desiredDirection, false))
                            noTarget = true;

                        if (noTarget)
                        {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        if (!cComp.TrackTarget(topAi, ref desiredDirection))
                            continue;
                    }

                }
                catch (Exception ex)
                {
                    Log.Line($"Caught exception in Control loop: {ex}");
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

                        var anyShot = (normalShot && !shootModeDefault) || ((w.ShootCount > 0 && w.ShootDelay == 0 || w.ShootDelay != 0 && w.ShootDelay-- == 0) && !wComp.ShootManager.FreezeClientShoot);

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
                ai.ClosestWeaponCompSqr = double.MaxValue;
                if (activeTurret)
                    AimingAi.Add(ai);

                if (Tick - _vanillaTurretTick < 3)
                    ai.ResetMyGridTargeting();
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
                var overrides = w.MasterComp?.Data.Repo.Values.Set.Overrides ?? comp.Data.Repo.Values.Set.Overrides;
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
