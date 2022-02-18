using System;
using System.Linq;
using CoreSystems.Support;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static Sandbox.Definitions.MyDefinitionManager;

namespace CoreSystems
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation, int.MaxValue - 1)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            try
            {
                if (!SuppressWc)
                    BeforeStartInit();
            }
            catch (Exception ex) { Log.Line($"Exception in BeforeStart: {ex}", null, true); }
        }

            public override MyObjectBuilder_SessionComponent GetObjectBuilder()
            {
                ResetVisualAreas();
                return base.GetObjectBuilder();
            }

        public override void UpdatingStopped()
        {
            try
            {
                ResetVisualAreas();
                if (!SuppressWc)
                    Paused();

            }
            catch (Exception ex) { Log.Line($"Exception in UpdatingStopped: {ex}", null, true); }
        }


        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (SuppressWc)
                    return;

                if (!DelayedHandWeaponsSpawn.IsEmpty)
                    InitDelayedHandWeapons();

                if (DeformProtection.Count > 0 && Tick - LastDeform > 0)
                    DeformProtection.Clear();
                
                Timings();

                if (IsClient) {

                    if (Tick10 && DeferredPlayerLock.Count > 0)
                        DeferredPlayerLocks();

                    if (Tick - ClientDestroyBlockTick == 30) {
                        _slimHealthClient.Clear();
                        _destroyedSlimsClient.Clear();
                    }

                    if (ClientSideErrorPkt.Count > 0)
                        ReproccessClientErrorPackets();

                    if (ClientPacketsToClean.Count > 0)
                        CleanClientPackets();
                }

                if (IsServer) {
                    if (Tick60) AcqManager.Observer();
                    if (Tick600) AcqManager.ReorderSleep();
                }

                if (!DedicatedServer && TerminalMon.Active)
                    TerminalMon.Monitor();

                MyCubeBlock cube;
                if (Tick60 && UiInput.ControlKeyPressed && UiInput.CtrlPressed && GetAimedAtBlock(out cube) && cube.BlockDefinition != null && CoreSystemsDefs.ContainsKey(cube.BlockDefinition.Id.SubtypeName))
                    ProblemRep.GenerateReport(cube);

                if (!IsClient && !InventoryUpdate && PartToPullConsumable.Count > 0 && ITask.IsComplete)
                    StartAmmoTask();

                if (!CompsToStart.IsEmpty)
                    StartComps();

                if (GridGroupUpdates.Count > 0)
                    GroupUpdates();

                if ((Tick120 || ReInitTick > 0 && Tick - ReInitTick < 10) && (CompsDelayedReInit.Count > 0 || CompsDelayedInit.Count > 0))
                {
                    InitDelayedComps();
                }

                if (Tick10 && !DelayedAiClean.IsEmpty) {
                    InitDelayedComps();
                    DelayedAiCleanup();
                }

                if (CompReAdds.Count > 0) {
                    InitDelayedComps();
                    if (!DelayedAiClean.IsEmpty) DelayedAiCleanup();
                    ChangeReAdds();
                }

                if (Tick3600 && MpActive) 
                    NetReport();

                if (Tick180) 
                    ProfilePerformance();

                if (Tick90 && HandlesInput && Settings.ClientConfig.ClientOptimizations)
                    ClientMonitor();

                FutureEvents.Tick(Tick);

                if (HomingWeapons.Count > 0)
                    UpdateHomingWeapons();

                if (MpActive) {
                    if (PacketsToClient.Count > 0 || PrunedPacketsToClient.Count > 0)
                        ProccessServerPacketsForClients();
                    if (PacketsToServer.Count > 0)
                        ProccessClientPacketsForServer();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionBeforeSim: {ex}", null, true); }
        }

        public override void Simulate()
        {
            try
            {
                if (SuppressWc)
                    return;

                if (!DedicatedServer) {
                    EntityControlUpdate();
                    CameraMatrix = Session.Camera.WorldMatrix;
                    CameraPos = CameraMatrix.Translation;
                    PlayerPos = Session.Player?.Character?.WorldAABB.Center ?? Vector3D.Zero;

                    if (Tick120 && DisplayAffectedArmor.Count > 0)
                        ColorAreas();
                }

                if (GameLoaded) {

                    DsUtil.Start("ai");

                    if (AimingAi.Count > 0) 
                        AimAi();

                    AiLoop();
                    DsUtil.Complete("ai", true);


                    DsUtil.Start("charge");
                    if (ChargingParts.Count > 0) UpdateChargeWeapons();
                    DsUtil.Complete("charge", true);

                    DsUtil.Start("acquire");
                    if (AcquireTargets.Count > 0) CheckAcquire();
                    DsUtil.Complete("acquire", true);

                    DsUtil.Start("shoot");
                    if (ShootingWeapons.Count > 0) ShootWeapons();
                    DsUtil.Complete("shoot", true);



                }

                if (!DedicatedServer && !InMenu) {
                    UpdateLocalAiAndCockpit();
                    if ((UiInput.PlayerCamera && (ActiveCockPit != null || TrackingAi != null && TrackingAi.SmartHandheld) || ActiveControlBlock is MyRemoteControl && !UiInput.PlayerCamera || UiInput.CameraBlockView) && PlayerDummyTargets.ContainsKey(PlayerId))
                        TargetSelection();
                }

                DsUtil.Start("ps");
                Projectiles.SpawnAndMove();
                DsUtil.Complete("ps", true);

                DsUtil.Start("pi");
                Projectiles.Intersect();
                DsUtil.Complete("pi", true);

                DsUtil.Start("pd");
                Projectiles.Damage();
                DsUtil.Complete("pd", true);

                DsUtil.Start("pa");
                Projectiles.AvUpdate();
                DsUtil.Complete("pa", true);

                DsUtil.Start("av");
                if (!DedicatedServer) Av.End();
                DsUtil.Complete("av", true);

                if (MpActive)  {
                    
                    DsUtil.Start("network1");

                    if (WeaponProSyncs.Count > 0)
                        SendProjectileSyncs();

                    if (PacketsToClient.Count > 0 || PrunedPacketsToClient.Count > 0) 
                        ProccessServerPacketsForClients();
                    if (PacketsToServer.Count > 0) 
                        ProccessClientPacketsForServer();
                    if (EwarNetDataDirty)
                        SendEwaredBlocks();
                    DsUtil.Complete("network1", true);
                }

            }
            catch (Exception ex) { Log.Line($"Exception in SessionSim: {ex}", null, true); }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (SuppressWc)
                    return;

                if (Placer != null) UpdatePlacer();

                if (AnimationsToProcess.Count > 0 || ThreadedAnimations.Count > 0) ProcessAnimations();

                if (GridTask.IsComplete)
                    CheckDirtyGridInfos();

                if (!DirtyPowerGrids.IsEmpty)
                    UpdateGridPowerState();

                if (WaterApiLoaded && (Tick3600 || WaterMap.IsEmpty))
                    UpdateWaters();

                if (HandlesInput && Tick60)
                    UpdatePlayerPainters();

                if (DebugLos && Tick1800) {
                    var averageMisses = RayMissAmounts > 0 ? RayMissAmounts / Rays : 0; 
                    Log.Line($"RayMissAverage: {averageMisses} - tick:{Tick}");
                }
            }
            catch (Exception ex) { Log.Line($"Exception in SessionAfterSim: {ex}", null, true); }
        }

        public override void Draw()
        {
            try
            {

                if (SuppressWc || DedicatedServer || _lastDrawTick == Tick || _paused) return;
                

                if (DebugLos)
                    LosDebuging();
                
                _lastDrawTick = Tick;
                DsUtil.Start("draw");
                CameraMatrix = Session.Camera.WorldMatrix;
                CameraPos = CameraMatrix.Translation;
                CameraFrustrum.Matrix = (Camera.ViewMatrix * Camera.ProjectionMatrix);
                var newFov = Camera.FovWithZoom;
                
                if (!MyUtils.IsEqual(newFov, CurrentFovWithZoom))
                    FovChanged();

                CurrentFovWithZoom = newFov;
                AspectRatio = Camera.ViewportSize.X / Camera.ViewportSize.Y;
                AspectRatioInv = Camera.ViewportSize.Y / Camera.ViewportSize.X;

                ScaleFov = Math.Tan(CurrentFovWithZoom * 0.5);

                if (!Session.Config.MinimalHud && InGridAiBlock) {

                    if (HudUi.TexturesToAdd > 0 || HudUi.KeepBackground) 
                        HudUi.DrawTextures();

                    if ((UiInput.PlayerCamera || UiInput.FirstPersonView || UiInput.CameraBlockView) && !InMenu && !MyAPIGateway.Gui.IsCursorVisible && PlayerDummyTargets.ContainsKey(PlayerId))
                        TargetUi.DrawTargetUi();

                    if (HudUi.AgingTextures)
                        HudUi.DrawText();
                }

                Av.Run();
                DrawDisabledGuns();
                DsUtil.Complete("draw", true);
            }
            catch (Exception ex) { Log.Line($"Exception in SessionDraw: {ex}", null, true); }
        }

        public override void HandleInput()  
        {
            if (HandlesInput && !SuppressWc) {

                if (ControlRequest != ControlQuery.None)
                    UpdateControlKeys();

                UiInput.UpdateInputState();
                if (MpActive)  {

                    if (UiInput.InputChanged && ActiveControlBlock != null)
                    {
                        SendMouseUpdate(TrackingAi, ActiveControlBlock);
                    }

                    Ai.FakeTargets fakeTargets;
                    if (TrackingAi != null && PlayerDummyTargets.TryGetValue(PlayerId, out fakeTargets)) {

                        if (fakeTargets.ManualTarget.LastUpdateTick == Tick && Tick - TargetUi.LastTrackTick <= 1 && Tick - TargetUi.LastManualTick <= 1)
                            SendAimTargetUpdate(TrackingAi, fakeTargets.ManualTarget);

                        if (fakeTargets.PaintedTarget.LastUpdateTick == Tick)
                            SendPaintedTargetUpdate(TrackingAi, fakeTargets.PaintedTarget);
                    }

                    if (PacketsToServer.Count > 0)
                        ProccessClientPacketsForServer();
                }

                if (Tick60 && SoundsToClean.Count > 0)
                    CleanSounds();
            }
        }

        public override void LoadData()
        {
            try
            {
                ModChecker();

                if (SuppressWc)
                    return;

                IsServer = MyAPIGateway.Multiplayer.IsServer;
                DedicatedServer = MyAPIGateway.Utilities.IsDedicated;
                AllDefinitions = Static.GetAllDefinitions();

                SoundDefinitions = Static.GetSoundDefinitions();
                MyEntities.OnEntityCreate += OnEntityCreate;
                MyEntities.OnCloseAll += OnCloseAll;

                MyAPIGateway.Gui.GuiControlCreated += MenuOpened;
                MyAPIGateway.Gui.GuiControlRemoved += MenuClosed;

                MyAPIGateway.Utilities.RegisterMessageHandler(7771, Handler);
                MyAPIGateway.Utilities.SendModMessage(7772, null);

                TriggerEntityModel = ModContext.ModPath + "\\Models\\Environment\\JumpNullField.mwm";
                TriggerEntityPool = new MyConcurrentPool<MyEntity>(0, TriggerEntityClear, 10000, TriggerEntityActivator);
                ReallyStupidKeenShit();
            }
            catch (Exception ex) { Log.Line($"Exception in LoadData: {ex}", null, true); }
        }

        protected override void UnloadData()
        {
            try
            {
                if (SuppressWc)
                    return;

                if (!PTask.IsComplete)
                    PTask.Wait();

                if (!CTask.IsComplete)
                    CTask.Wait();

                if (!ITask.IsComplete)
                    ITask.Wait();

                if (IsServer || DedicatedServer)
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(ServerPacketId, ProccessServerPacket);
                else
                {
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(ClientPacketId, ClientReceivedPacket);
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(StringPacketId, StringReceived);
                }

                if (HandlesInput)
                    MyAPIGateway.Utilities.MessageEntered -= ChatMessageSet;

                MyAPIGateway.Utilities.UnregisterMessageHandler(7771, Handler);

                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlHandler;

                MyEntities.OnEntityCreate -= OnEntityCreate;
                MyEntities.OnCloseAll -= OnCloseAll;


                MyAPIGateway.Gui.GuiControlCreated -= MenuOpened;
                MyAPIGateway.Gui.GuiControlRemoved -= MenuClosed;

                MyVisualScriptLogicProvider.PlayerDisconnected -= PlayerDisconnected;
                MyVisualScriptLogicProvider.PlayerRespawnRequest -= PlayerConnected;
                ApiServer.Unload();

                PurgeAll();

                Log.Line("Logging stopped.");
                Log.Close();
            }
            catch (Exception ex) { Log.Line($"Exception in UnloadData: {ex}", null, true); }
        }
    }
}

