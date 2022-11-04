using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Projectiles;
using CoreSystems.Support;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using static CoreSystems.Support.Target;
using static CoreSystems.Support.CoreComponent.Start;
using static CoreSystems.Support.CoreComponent.Trigger;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType;
using static CoreSystems.ProtoWeaponState;
using Sandbox.Game.Entities;
using System;
using System.Diagnostics;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using static VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GameDefinition;
using VRage.Input;
using SpaceEngineers.Game.Weapons.Guns;
using VRage.Game.Entity;

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

                if (ai.AiType == Ai.AiTypes.Grid && !ai.HasPower || enforcement.ServerSleepSupport && IsServer && ai.AwakeComps == 0 && ai.WeaponsTracking == 0 && ai.SleepingComps > 0 && !ai.CheckProjectiles && ai.AiSleep && !ai.DbUpdated) 
                    continue;


                if (ai.AiType != Ai.AiTypes.Phantom && (ai.TopEntityMap.GroupMap.LastControllerTick == Tick || ai.TopEntityMap.LastControllerTick == Tick))
                    Ai.Constructs.UpdatePlayerStates(ai.TopEntityMap.GroupMap);

                if (Tick60 && ai.AiType == Ai.AiTypes.Grid && ai.BlockChangeArea != BoundingBox.Invalid) {
                    ai.BlockChangeArea.Min *= ai.GridEntity.GridSize;
                    ai.BlockChangeArea.Max *= ai.GridEntity.GridSize;
                }

                var construct = ai.Construct;
                var rootAi = construct.RootAi;
                var rootConstruct = rootAi.Construct;
                var focus = rootConstruct.Focus;

                if (rootConstruct.DirtyWeaponGroups)
                {
                    Ai.Constructs.RebuildWeaponGroups(rootAi.TopEntityMap.GroupMap);
                }

                if (Tick60 && Tick != rootConstruct.LastEffectUpdateTick && rootConstruct.TotalEffect > rootConstruct.PreviousTotalEffect)
                    rootConstruct.UpdateEffect(Tick);

                if (IsServer) {
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

                    switch (pComp.Data.Repo.Values.State.Trigger)
                    {
                        case Once:
                            pComp.ShootManager.RequestShootSync(PlayerId, Weapon.ShootManager.RequestType.Once, Weapon.ShootManager.Signals.Once);
                            break;
                        case On:
                            pComp.ShootManager.RequestShootSync(PlayerId, Weapon.ShootManager.RequestType.On, Weapon.ShootManager.Signals.On);
                            break;
                    }

                    var pValues = pComp.Data.Repo.Values;
                    var overrides = pValues.Set.Overrides;
                    var cMode = overrides.Control;
                    var sMode = overrides.ShootMode;

                    var onConfrimed = pValues.State.Trigger == On && !pComp.ShootManager.FreezeClientShoot && !pComp.ShootManager.WaitingShootResponse && (sMode != Weapon.ShootManager.ShootModes.AiShoot || pComp.ShootManager.Signal == Weapon.ShootManager.Signals.Manual);
                    var noShootDelay = pComp.ShootManager.ShootDelay == 0 || pComp.ShootManager.ShootDelay != 0 && pComp.ShootManager.ShootDelay-- == 0;

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
                        var overHeat = p.PartState.Overheated && p.OverHeatCountDown == 0;
                        var canShoot = !overHeat && !reloading && !p.System.DesignatorWeapon;

                        var autoShot =  pComp.Data.Repo.Values.State.Trigger == On || p.AiShooting && pComp.Data.Repo.Values.State.Trigger == Off;
                        var anyShot = !pComp.ShootManager.FreezeClientShoot && (p.ShootCount > 0 || onConfrimed) && noShootDelay || autoShot && sMode == Weapon.ShootManager.ShootModes.AiShoot;

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

                        if (cComp.Platform.State != CorePlatform.PlatformState.Ready || cComp.IsDisabled || cComp.IsAsleep || !cComp.IsWorking || cComp.CoreEntity.MarkedForClose || cComp.LazyUpdate && !ai.DbUpdated && Tick > cComp.NextLazyUpdateStart) {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        var az = (IMyMotorStator)cComp.Controller.AzimuthRotor;
                        var el = (IMyMotorStator)cComp.Controller.ElevationRotor;

                        var cValues = cComp.Data.Repo.Values;

                        if (MpActive && IsClient)
                        {
                            MyEntity rotorEnt;
                            if (az == null && cValues.Other.Rotor1 > 0 && MyEntities.TryGetEntityById(cValues.Other.Rotor1, out rotorEnt))
                                az = (IMyMotorStator)rotorEnt;

                            if (el == null && cValues.Other.Rotor2 > 0 && MyEntities.TryGetEntityById(cValues.Other.Rotor2, out rotorEnt))
                                el = (IMyMotorStator)rotorEnt;
                        }
                        
                        if (az == null || el == null)
                            continue;

                        if (MpActive && IsServer)
                        {
                            if (az.EntityId != cValues.Other.Rotor1 || el.EntityId != cValues.Other.Rotor2)
                            {
                                cValues.Other.Rotor1 = az.EntityId;
                                cComp.Controller.AzimuthRotor = az;
                                cValues.Other.Rotor2 = el.EntityId;
                                cComp.Controller.ElevationRotor = el;
                                SendComp(cComp);
                            }
                            else if (Tick1800)
                            {
                                cComp.Controller.AzimuthRotor = az;
                                cComp.Controller.ElevationRotor = el;
                            }

                        }

                        var controlPart = cComp.Platform.Control;
                        controlPart.IsAimed = false;

                        controlPart.BaseMap = az.TopGrid == el.CubeGrid ? az : el;
                        controlPart.OtherMap = controlPart.BaseMap == az ? el : az;
                        var topGrid = controlPart.BaseMap.TopGrid as MyCubeGrid;
                        var otherGrid = controlPart.OtherMap.TopGrid as MyCubeGrid;
                        Ai topAi;
                        if (controlPart.BaseMap == null || controlPart.OtherMap == null  || topGrid == null || otherGrid == null || !EntityAIs.TryGetValue(otherGrid, out topAi)) {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        var trackWeapon = topAi.RootFixedWeaponComp?.PrimaryWeapon;
                        controlPart.TrackingWeapon = trackWeapon;
                        if (trackWeapon == null)
                            continue;

                        if (trackWeapon.Comp.Ai.MaxTargetingRange > ai.MaxTargetingRange)
                            cComp.ReCalculateMaxTargetingRange(trackWeapon.Comp.Ai.MaxTargetingRange);

                        topAi.RotorCommandTick = Tick;
                        trackWeapon.MasterComp = cComp;
                        trackWeapon.RotorTurretTracking = true;
                        
                        var isUnderControl = cComp.Controller.IsUnderControl;
                        var cPlayerId = cValues.State.PlayerId;
                        Ai.PlayerController pControl;
                        pControl.ControlEntity = null;
                        var playerControl = rootConstruct.ControllingPlayers.TryGetValue(cPlayerId, out pControl);
                        var activePlayer = PlayerId == cPlayerId && playerControl;

                        var hasControl = activePlayer && pControl.ControlEntity == cComp.CoreEntity;
                        topAi.RotorManualControlId = hasControl ? PlayerId : topAi.RotorManualControlId != -2 ? -1 : -2;
                        var cMode = cValues.Set.Overrides.Control;
                        if (HandlesInput && (cPlayerId == PlayerId || !controlPart.Comp.HasAim && ai.RotorManualControlId == PlayerId))
                        {
                            var overrides = cValues.Set.Overrides;

                            var playerAim = activePlayer && cMode != ProtoWeaponOverrides.ControlModes.Auto || pControl.ControlEntity is IMyTurretControlBlock;
                            var track = !InMenu && (playerAim && !UiInput.CameraBlockView || pControl.ControlEntity is IMyTurretControlBlock || UiInput.CameraChannelId > 0 && UiInput.CameraChannelId == overrides.CameraChannel);

                            if (cValues.State.TrackingReticle != track)
                                TrackReticleUpdateCtc(controlPart.Comp, track);
                        }

                        if (isUnderControl)
                        {
                            cComp.RotorsMoving = true;
                            continue;
                        }

                        if (!cComp.Data.Repo.Values.Set.Overrides.AiEnabled || topAi.RootFixedWeaponComp.PrimaryWeapon.Comp.CoreEntity.MarkedForClose) {
                            if (cComp.RotorsMoving)
                                cComp.StopRotors();
                            continue;
                        }

                        var validTarget = controlPart.TrackingWeapon.Target.TargetState == TargetStates.IsEntity || controlPart.TrackingWeapon.Target.TargetState == TargetStates.IsFake;

                        var noTarget = false;
                        var desiredDirection = Vector3D.Zero;
                        
                        if (!validTarget)
                            noTarget = true;
                        else if (!ControlSys.TrajectoryEstimation(topAi, controlPart, out desiredDirection))
                        {
                            noTarget = true;
                        }

                        if (noTarget) {
                            
                            topAi.RotorTargetPosition = Vector3D.MaxValue;

                            if (IsServer && trackWeapon.Target.HasTarget)
                                trackWeapon.Target.Reset(Tick, States.ServerReset);

                            if (cComp.RotorsMoving)
                                cComp.StopRotors();

                            continue;
                        }

                        if (!cComp.TrackTarget(topAi, cComp.Platform.Control.BaseMap,  cComp.Platform.Control.OtherMap, true, ref desiredDirection))
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

                    if (ai.DbUpdated || !wComp.UpdatedState) 
                        wComp.DetectStateChanges();

                    if (wComp.Platform.State != CorePlatform.PlatformState.Ready || wComp.IsDisabled || wComp.IsAsleep || !wComp.IsWorking || wComp.CoreEntity.MarkedForClose || wComp.LazyUpdate && !ai.DbUpdated && Tick > wComp.NextLazyUpdateStart)
                        continue;

                    wComp.OnCustomTurret = ai.RootFixedWeaponComp?.PrimaryWeapon?.MasterComp != null;


                    var wValues = wComp.Data.Repo.Values;
                    var overrides = wValues.Set.Overrides;
                    var cMode = overrides.Control;

                    var sMode = overrides.ShootMode;
                    var focusTargets = wComp.OnCustomTurret ? ai.RootFixedWeaponComp.PrimaryWeapon.MasterComp.Data.Repo.Values.Set.Overrides.FocusTargets : overrides.FocusTargets;
                    var grids = wComp.OnCustomTurret ? ai.RootFixedWeaponComp.PrimaryWeapon.MasterComp.Data.Repo.Values.Set.Overrides.Grids : overrides.Grids;
                    var overRide = wComp.OnCustomTurret ? ai.RootFixedWeaponComp.PrimaryWeapon.MasterComp.Data.Repo.Values.Set.Overrides.Override : overrides.Override;
                    var projectiles = wComp.OnCustomTurret ? ai.RootFixedWeaponComp.PrimaryWeapon.MasterComp.Data.Repo.Values.Set.Overrides.Projectiles : overrides.Projectiles;


                    if (IsServer && wComp.OnCustomTurret)
                    {
                        var cValues = ai.RootFixedWeaponComp.PrimaryWeapon.MasterComp.Data.Repo.Values;
                        if (cValues.Set.Overrides.Control != overrides.Control)
                            BlockUi.RequestControlMode(wComp.TerminalBlock, (long)cValues.Set.Overrides.Control);

                        if (cValues.State.PlayerId != wValues.State.PlayerId)
                            wComp.TakeOwnerShip(cValues.State.PlayerId);
                    }

                    if (HandlesInput) {

                        if (IsClient && ai.TopEntityMap.LastControllerTick == Tick && wComp.ShootManager.Signal == Weapon.ShootManager.Signals.Manual && (wComp.ShootManager.ClientToggleCount > wValues.State.ToggleCount || wValues.State.Trigger == On) && wValues.State.PlayerId > 0) 
                            wComp.ShootManager.RequestShootSync(PlayerId, Weapon.ShootManager.RequestType.Off);

                        var isControllingPlayer = wValues.State.PlayerId == PlayerId || !wComp.HasAim && ai.RotorManualControlId == PlayerId;
                        if (isControllingPlayer) {

                            Ai.PlayerController pControl;
                            pControl.ControlEntity = null;
                            var playerControl = rootConstruct.ControllingPlayers.TryGetValue(wValues.State.PlayerId, out pControl);
                            
                            var activePlayer = PlayerId == wValues.State.PlayerId && playerControl;
                            var cManual = pControl.ControlEntity is IMyTurretControlBlock;
                            var customWeapon = cManual && wComp.OnCustomTurret && ai.RootFixedWeaponComp.PrimaryWeapon.MasterComp.Cube == pControl.ControlEntity;
                            var manualThisWeapon = pControl.ControlEntity == wComp.Cube && wComp.HasAim || pControl.ControlEntity is IMyAutomaticRifleGun;
                            var controllingWeapon = customWeapon || manualThisWeapon;
                            var validManualModes = (sMode == Weapon.ShootManager.ShootModes.MouseControl || cMode == ProtoWeaponOverrides.ControlModes.Manual);
                            var manual = (controllingWeapon || pControl.ShareControl && validManualModes && ((wComp.HasAim || wComp.OnCustomTurret) || !IdToCompMap.ContainsKey(pControl.EntityId)));
                            var playerAim = activePlayer && manual;
                            var track = !InMenu && (playerAim && (!UiInput.CameraBlockView || cManual || manualThisWeapon) || UiInput.CameraChannelId > 0 && UiInput.CameraChannelId == overrides.CameraChannel);
                            if (!activePlayer && wComp.ShootManager.Signal == Weapon.ShootManager.Signals.MouseControl)
                                wComp.ShootManager.RequestShootSync(PlayerId, Weapon.ShootManager.RequestType.Off);
                            
                            if (cMode == ProtoWeaponOverrides.ControlModes.Manual)
                                TargetUi.LastManualTick = Tick;

                            if (wValues.State.TrackingReticle != track)
                                TrackReticleUpdate(wComp, track);

                            var active = wComp.ShootManager.ClientToggleCount > wValues.State.ToggleCount || wValues.State.Trigger == On;
                            var turnOn = !active && UiInput.ClientInputState.MouseButtonLeft && playerControl && !InMenu;
                            var turnOff = active && (!UiInput.ClientInputState.MouseButtonLeft || InMenu) && Tick5;

                            if (sMode == Weapon.ShootManager.ShootModes.AiShoot)
                            {
                                if (playerAim)
                                {
                                    if (turnOn || turnOff)
                                    {
                                        wComp.ShootManager.RequestShootSync(PlayerId, turnOn ? Weapon.ShootManager.RequestType.On : Weapon.ShootManager.RequestType.Off, turnOn ? Weapon.ShootManager.Signals.Manual : Weapon.ShootManager.Signals.None);
                                    }
                                }
                                else if (wComp.ShootManager.Signal == Weapon.ShootManager.Signals.Manual && active)
                                {
                                    wComp.ShootManager.RequestShootSync(PlayerId, Weapon.ShootManager.RequestType.Off);
                                }
                            }
                            else if (sMode == Weapon.ShootManager.ShootModes.MouseControl && (turnOn && playerAim || turnOff))
                            {
                                wComp.ShootManager.RequestShootSync(PlayerId, turnOn ? Weapon.ShootManager.RequestType.On : Weapon.ShootManager.RequestType.Off, Weapon.ShootManager.Signals.MouseControl);
                            }
                        }
                    }

                    Ai.FakeTargets fakeTargets = null;
                    if (cMode == ProtoWeaponOverrides.ControlModes.Manual || cMode == ProtoWeaponOverrides.ControlModes.Painter)
                        PlayerDummyTargets.TryGetValue(wValues.State.PlayerId, out fakeTargets);

                    wComp.PainterMode = fakeTargets != null && cMode == ProtoWeaponOverrides.ControlModes.Painter && fakeTargets.PaintedTarget.EntityId != 0;
                    wComp.UserControlled = cMode != ProtoWeaponOverrides.ControlModes.Auto || wValues.State.Control == ControlMode.Camera || fakeTargets != null && fakeTargets.PaintedTarget.EntityId != 0;
                    wComp.FakeMode = wComp.ManualMode || wComp.PainterMode;

                    var onConfrimed = wValues.State.Trigger == On && !wComp.ShootManager.FreezeClientShoot && !wComp.ShootManager.WaitingShootResponse && (sMode != Weapon.ShootManager.ShootModes.AiShoot || wComp.ShootManager.Signal == Weapon.ShootManager.Signals.Manual);
                    var noShootDelay = wComp.ShootManager.ShootDelay == 0 || wComp.ShootManager.ShootDelay != 0 && wComp.ShootManager.ShootDelay-- == 0;
                    var sequenceReady = overrides.WeaponGroupId == 0 || wComp.SequenceReady(rootConstruct);

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

                        if (ai.AiType == Ai.AiTypes.Player && DedicatedServer)
                            SendHandDebugInfo(w);

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
                        var addWeaponToHud = HandlesInput && (w.HeatPerc >= 0.01 || (w.ShowReload && (w.Loading || w.Reload.WaitForClient)) || (aConst.CanReportTargetStatus && wValues.Set.ReportTarget && !w.Target.HasTarget && grids && (wComp.DetectOtherSignals && ai.DetectionInfo.OtherInRange || ai.DetectionInfo.PriorityInRange) && ai.DetectionInfo.TargetInRange(w)));

                        if (addWeaponToHud && !Session.Config.MinimalHud && !enforcement.DisableHudReload && (ActiveControlBlock != null && ai.SubGridCache.Contains(ActiveControlBlock.CubeGrid) || PlayerHandWeapon != null && IdToCompMap.ContainsKey(((IMyGunBaseUser)PlayerHandWeapon).OwnerId))) {
                            HudUi.TexturesToAdd++;
                            HudUi.WeaponsToDisplay.Add(w);
                        }

                        if (w.CriticalReaction && !wComp.CloseCondition && (overrides.Armed || wValues.State.CountingDown || wValues.State.CriticalReaction))
                            w.CriticalMonitor();

                        if (w.Target.ClientDirty)
                            w.Target.ClientUpdate(w, w.TargetData);

                        ///
                        /// Check target for expire states
                        /// 
                        var noAmmo = w.NoMagsToLoad && w.ProtoWeaponAmmo.CurrentAmmo == 0 && aConst.Reloadable && !w.System.DesignatorWeapon && Tick - w.LastMagSeenTick > 600;

                        if (!IsClient) {

                            if (w.Target.HasTarget) 
                            {
                                if (noAmmo)
                                    w.Target.Reset(Tick, States.Expired);
                                else if (w.Target.TargetEntity == null && w.Target.Projectile == null && !wComp.FakeMode || wComp.ManualMode && (fakeTargets == null || Tick - fakeTargets.ManualTarget.LastUpdateTick > 120))
                                    w.Target.Reset(Tick, States.Expired, !wComp.ManualMode);
                                else if (w.Target.TargetEntity != null && (wComp.UserControlled && !w.System.SuppressFire || w.Target.TargetEntity.MarkedForClose || Tick60 && (focusTargets && !focus.ValidFocusTarget(w) || Tick60 && !focusTargets && !w.TurretController && (aConst.RequiresTarget || w.RotorTurretTracking) && !w.TargetInRange(w.Target.TargetEntity))))
                                    w.Target.Reset(Tick, States.Expired);
                                else if (w.Target.Projectile != null && (!ai.LiveProjectile.Contains(w.Target.Projectile) || w.Target.TargetState == TargetStates.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive)) 
                                {
                                    w.Target.Reset(Tick, States.Expired);
                                    w.FastTargetResetTick = Tick + 6;
                                }
                                else if (!w.TurretController)
                                {
                                    Vector3D targetPos;
                                    if (w.TurretAttached) 
                                    {
                                        if (!w.System.TrackTargets) 
                                        {
                                            var trackingWeaponIsFake = wComp.PrimaryWeapon.Target.TargetState == TargetStates.IsFake;
                                            var thisWeaponIsFake = w.Target.TargetState == TargetStates.IsFake;
                                            if ((wComp.PrimaryWeapon.Target.Projectile != w.Target.Projectile || w.Target.TargetState == TargetStates.IsProjectile && w.Target.Projectile.State != Projectile.ProjectileState.Alive || wComp.PrimaryWeapon.Target.TargetEntity != w.Target.TargetEntity || trackingWeaponIsFake != thisWeaponIsFake))
                                                w.Target.Reset(Tick, States.Expired);
                                            else
                                                w.TargetLock = true;
                                        }
                                        else if (!Weapon.TargetAligned(w, w.Target, out targetPos))
                                            w.Target.Reset(Tick, States.Expired);
                                    }
                                    else if (w.System.TrackTargets && !Weapon.TargetAligned(w, w.Target, out targetPos))
                                        w.Target.Reset(Tick, States.Expired);
                                }
                            }
                        }
                        else if(w.Target.TargetEntity != null && w.Target.TargetEntity.MarkedForClose || w.DelayedTargetResetTick == Tick && w.TargetData.EntityId == 0 && w.Target.HasTarget)
                            w.Target.Reset(w.System.Session.Tick, States.ServerReset);

                        w.ProjectilesNear = enemyProjectiles && (w.System.TrackProjectile || wComp.OnCustomTurret) && projectiles && w.Target.TargetState != TargetStates.IsProjectile && (w.Target.TargetChanged || QCount == w.ShortLoadId);

                        if (wValues.State.Control == ControlMode.Camera && UiInput.MouseButtonPressed)
                            w.Target.TargetPos = Vector3D.Zero;

                        ///
                        /// Queue for target acquire or set to tracking weapon.
                        /// 
                        var seek = wComp.FakeMode && w.Target.TargetState != TargetStates.IsFake || (aConst.RequiresTarget || w.RotorTurretTracking) & !w.Target.HasTarget && !noAmmo && (wComp.DetectOtherSignals && ai.DetectionInfo.OtherInRange || ai.DetectionInfo.PriorityInRange) && (!wComp.UserControlled && !enforcement.DisableAi || wValues.State.Trigger == On);

                        if (!IsClient && (seek || (aConst.RequiresTarget || w.RotorTurretTracking) && (rootConstruct.TargetResetTick == Tick || w.ProjectilesNear) && !wComp.UserControlled && !enforcement.DisableAi) && !w.AcquiringTarget && wValues.State.Control != ControlMode.Camera)
                        {
                            w.AcquiringTarget = true;
                            AcquireTargets.Add(w);
                        }

                        if (w.Target.TargetChanged) // Target changed
                            w.TargetChanged();

                        ///
                        /// Determine if its time to shoot
                        ///
                        ///
                        w.AiShooting = !wComp.UserControlled && !w.System.SuppressFire && (w.TargetLock || ai.RotorTurretAimed && Vector3D.DistanceSquared(wComp.CoreEntity.PositionComp.WorldAABB.Center, ai.RotorTargetPosition) <= wComp.MaxDetectDistanceSqr);

                        var reloading = aConst.Reloadable && w.ClientMakeUpShots == 0 && (w.Loading || w.ProtoWeaponAmmo.CurrentAmmo == 0 || w.Reload.WaitForClient);
                        var overHeat = w.PartState.Overheated && (w.OverHeatCountDown == 0 || w.OverHeatCountDown != 0 && w.OverHeatCountDown-- == 0);

                        var canShoot = !overHeat && !reloading && !w.System.DesignatorWeapon && sequenceReady;
                        var paintedTarget = wComp.PainterMode && w.Target.TargetState == TargetStates.IsFake && (w.Target.IsAligned || wComp.OnCustomTurret && wComp.Ai.RootFixedWeaponComp.PrimaryWeapon.MasterComp.Platform.Control.IsAimed);
                        var autoShot = paintedTarget || w.AiShooting && wValues.State.Trigger == Off;
                        var anyShot = !wComp.ShootManager.FreezeClientShoot && (w.ShootCount > 0 || onConfrimed) && noShootDelay || autoShot && sMode == Weapon.ShootManager.ShootModes.AiShoot;

                        var delayedFire = w.System.DelayCeaseFire && !w.Target.IsAligned && Tick - w.CeaseFireDelayTick <= w.System.CeaseFireDelay;
                        var finish = w.FinishShots || delayedFire;
                        var shootRequest = (anyShot || finish);

                        w.LockOnFireState = shootRequest && (w.System.LockOnFocus && !w.Comp.ModOverride) && construct.Data.Repo.FocusData.HasFocus && focus.FocusInRange(w);
                        var shotReady = canShoot && (shootRequest && (!w.System.LockOnFocus || w.Comp.ModOverride) || w.LockOnFireState);
                        var shoot = shotReady && ai.CanShoot && (!aConst.RequiresTarget || w.Target.HasTarget || finish || overRide || (wComp.ShootManager.Signal == Weapon.ShootManager.Signals.Manual || wComp.ShootManager.Signal == Weapon.ShootManager.Signals.MouseControl));

                        if (shoot) {
 
                            if (w.System.DelayCeaseFire && (autoShot || w.FinishShots))
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
                            if (w.TurretActive)
                                activeTurret = true;
                        }

                        ///
                        /// Check weapon's turret to see if its time to go home
                        ///
                        if (w.TurretController && !w.IsHome && !w.ReturingHome && !w.Target.HasTarget && !shootRequest && Tick - w.Target.ResetTick > 239 && !wComp.UserControlled && wValues.State.Trigger == Off)
                            w.ScheduleWeaponHome();

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
                if (ai.RotorCommandTick > 0 && Tick - ai.RotorCommandTick > 1)
                    ai.ResetControlRotorState();


                if (activeTurret)
                    AimingAi.Add(ai);

                if (Tick - VanillaTurretTick < 3)
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

                        if (!Weapon.TrackingTarget(w, w.Target, out w.TargetLock) && !IsClient && w.Target.ExpiredTick != Tick && w.Target.HasTarget)
                            w.Target.Reset(Tick, States.LostTracking);
                    }
                }

            }, stride);

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

                var seekProjectile = w.ProjectilesNear || (w.System.TrackProjectile || w.Comp.OnCustomTurret) && overrides.Projectiles && w.BaseComp.Ai.CheckProjectiles;
                var checkTime = w.Target.TargetChanged || acquire || seekProjectile || w.FastTargetResetTick == Tick;
                var ai = w.BaseComp.Ai;

                if (checkTime || ai.Construct.RootAi.Construct.TargetResetTick == Tick && w.Target.HasTarget) {

                    if (seekProjectile || comp.Data.Repo.Values.State.TrackingReticle || (comp.DetectOtherSignals && ai.DetectionInfo.OtherInRange || ai.DetectionInfo.PriorityInRange) && ai.DetectionInfo.ValidSignalExists(w))
                    {
                        if (comp.PrimaryWeapon != null && comp.PrimaryWeapon.System.DesignatorWeapon && comp.PrimaryWeapon != w && comp.PrimaryWeapon.Target.HasTarget) {

                            var topMost = comp.PrimaryWeapon.Target.TargetEntity?.GetTopMostParent();
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
                var quickSkip = invalidWeapon || w.Comp.IsBlock && smartTimer && !w.SmartLos() || w.PauseShoot || w.LiveDrones >= w.System.MaxActiveProjectiles || (w.ProtoWeaponAmmo.CurrentAmmo == 0 && w.ClientMakeUpShots == 0) && w.ActiveAmmoDef.AmmoDef.Const.Reloadable;
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
