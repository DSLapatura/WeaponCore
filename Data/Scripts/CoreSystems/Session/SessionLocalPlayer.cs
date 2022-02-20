using System.Collections.Concurrent;
using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using WeaponCore.Data.Scripts.CoreSystems.Ui.Targeting;
using static CoreSystems.Support.Ai;
using static CoreSystems.Support.WeaponDefinition.TargetingDef;
using static CoreSystems.Support.WeaponDefinition.TargetingDef.BlockTypes;
using static CoreSystems.ProtoWeaponState;
namespace CoreSystems
{
    public partial class Session
    {
        internal bool UpdateLocalAiAndCockpit()
        {
            InGridAiBlock = false;
            ActiveControlBlock = ControlledEntity as MyCubeBlock;
            PlayerHandWeapon =  Session.Player?.Character?.EquippedTool as IMyAutomaticRifleGun;
            var cockPit = ControlledEntity as MyCockpit;
            if (cockPit != null && cockPit.EnableShipControl)
                ActiveCockPit = cockPit;
            else ActiveCockPit = null;

            PlayerControllerEntity oldControlId;
            var controlledEntity = ActiveCockPit ?? ActiveControlBlock ?? PlayerHandWeapon?.Owner;
            var topEntity = ActiveControlBlock != null ? controlledEntity?.GetTopMostParent() : controlledEntity;

            if (topEntity != null && EntityToMasterAi.TryGetValue(topEntity, out TrackingAi) && !TrackingAi.MarkedForClose && TrackingAi.Data.Repo != null)
            {
                var camera = Session.CameraController?.Entity as MyCameraBlock;
                if (camera == null || !GroupedCamera(camera))
                    ActiveCameraBlock = null;
                InGridAiBlock = true;

                TrackingAi.PlayerControl.TryGetValue(PlayerId, out oldControlId);

                if (oldControlId.EntityId != controlledEntity.EntityId)
                {
                    SendActiveControlUpdate(TrackingAi, controlledEntity, true);
                    TargetLeadUpdate();
                }
                else if (LeadGroupsDirty || !MyUtils.IsEqual(LastOptimalDps, TrackingAi.Construct.OptimalDps))
                    TargetLeadUpdate();

                TrackingAi.PlayerControl[PlayerId] = new PlayerControllerEntity { ChangeTick = Tick, ControllEntity = controlledEntity, EntityId = controlledEntity.EntityId, Id = PlayerId };
            }
            else
            {
                if (TrackingAi?.Data.Repo != null)
                {
                    TrackingAi.Construct.Focus.ClientIsFocused(TrackingAi);

                    if (TrackingAi.PlayerControl.TryGetValue(PlayerId, out oldControlId))
                    {
                        if (IsServer) TrackingAi.Construct.NetRefreshAi();
                        SendActiveControlUpdate(TrackingAi, oldControlId.ControllEntity, false);
                        foreach (var list in LeadGroups) list.Clear();
                        LeadGroupActive = false;
                        TrackingAi.PlayerControl.Remove(PlayerId);
                    }
                }

                TrackingAi = null;
                ActiveCockPit = null;
                ActiveControlBlock = null;
                ActiveCameraBlock = null;
            }
            return InGridAiBlock;
        }

        private void TargetLeadUpdate()
        {
            LeadGroupActive = false;
            LeadGroupsDirty = false;

            LastOptimalDps = TrackingAi.Construct.OptimalDps;

            foreach (var list in LeadGroups)
                list.Clear();
            
            if (Settings.Enforcement.DisableLeads)
                return;

            foreach (var ai in TrackingAi.Construct.RefreshedAis)
            {
                foreach (var comp in ai.WeaponComps)
                {
                    var collection = comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? comp.Platform.Weapons : comp.Platform.Phantoms;
                    foreach (var w in collection)
                    {
                        if ((!w.Comp.HasTurret && !w.Comp.OverrideLeads || w.Comp.HasTurret && w.Comp.OverrideLeads) && w.Comp.Data.Repo.Values.Set.Overrides.LeadGroup > 0)
                        {
                            LeadGroups[MathHelper.Clamp(w.Comp.Data.Repo.Values.Set.Overrides.LeadGroup - 1, 0, 3)].Add(w);
                            LeadGroupActive = true;
                        }
                    }
                }
            }
        }

        private bool GroupedCamera(MyCameraBlock camera)
        {
            long cameraGroupId;
            if (CameraChannelMappings.TryGetValue(camera, out cameraGroupId))
            {
                ActiveCameraBlock = camera;
                return true;
            }
            ActiveCameraBlock = null;
            return false;
        }

        internal void GunnerAcquire(MyCubeBlock cube)
        {
            GunnerBlackList = true;
            ActiveControlBlock = cube;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, PlayerId);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, PlayerId);
            var controlStringMenu = MyAPIGateway.Input.GetControl(UiInput.MouseButtonMenu).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMenu, PlayerId);
        }

        internal void GunnerRelease(MyCubeBlock cube)
        {
            GunnerBlackList = false;
            ActiveControlBlock = null;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, PlayerId, true);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, PlayerId, true);
            var controlStringMenu = MyAPIGateway.Input.GetControl(UiInput.MouseButtonMenu).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMenu, PlayerId, true);
        }

        internal void EntityControlUpdate()
        {
            var lastControlledEnt = ControlledEntity;
            ControlledEntity = (MyEntity)MyAPIGateway.Session.ControlledObject;

            var entityChanged = lastControlledEnt != null && lastControlledEnt != ControlledEntity;

            if (entityChanged)
            {
                //if (lastControlledEnt is MyCockpit || lastControlledEnt is MyRemoteControl)
                //    PlayerControlAcquired(lastControlledEnt);

                if (ControlledEntity is MyCockpit || ControlledEntity is MyRemoteControl)
                    PlayerControlNotify(ControlledEntity);
            }
        }

        private void FovChanged()
        {
            HudUi.NeedsUpdate = true;
            TargetUi.ResetCache();
        }

        private void ShowClientNotify(ClientNotifyPacket notify)
        {
            MyAPIGateway.Utilities.ShowNotification(notify.Message, notify.Duration > 0 ? notify.Duration : 1000, notify.Color == string.Empty ? "White" : notify.Color);
        }

        internal void ShowLocalNotify(string message, int duration, string color = null)
        {
            MyAPIGateway.Utilities.ShowNotification(message, duration, string.IsNullOrEmpty(color) ? "White" : color);
        }

        private readonly Color _restrictionAreaColor = new Color(128, 0, 128, 96);
        private readonly Color _uninitializedColor = new Color(255, 0, 0, 200);
        private BoundingSphereD _nearbyGridsTestSphere = new BoundingSphereD(Vector3D.Zero, 350);
        private readonly List<MyEntity> _gridsNearCamera = new List<MyEntity>();
        private readonly List<MyCubeBlock> _uninitializedBlocks = new List<MyCubeBlock>();
        private readonly List<Weapon.WeaponComponent> _debugBlocks = new List<Weapon.WeaponComponent>();
        private void DrawDisabledGuns()
        {
            if (Tick600 || Tick60 && QuickDisableGunsCheck)
            {

                QuickDisableGunsCheck = false;
                _nearbyGridsTestSphere.Center = CameraPos;
                _gridsNearCamera.Clear();
                _uninitializedBlocks.Clear();
                _debugBlocks.Clear();

                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref _nearbyGridsTestSphere, _gridsNearCamera);
                for (int i = _gridsNearCamera.Count - 1; i >= 0; i--)
                {
                    var grid = _gridsNearCamera[i] as MyCubeGrid;
                    if (grid?.Physics != null && !grid.MarkedForClose && !grid.IsPreview && !grid.Physics.IsPhantom)
                    {

                        var fatBlocks = grid.GetFatBlocks();
                        for (int j = 0; j < fatBlocks.Count; j++)
                        {

                            var block = fatBlocks[j];
                            if (block.IsFunctional && PartPlatforms.ContainsKey(block.BlockDefinition.Id))
                            {

                                Ai gridAi;
                                CoreComponent comp;
                                if (!EntityAIs.TryGetValue(block.CubeGrid, out gridAi) || !gridAi.CompBase.TryGetValue(block, out comp))
                                    _uninitializedBlocks.Add(block);
                                else {

                                    var wComp = comp as Weapon.WeaponComponent;
                                    if (wComp != null && wComp.Data.Repo.Values.Set.Overrides.Debug)
                                        _debugBlocks.Add(wComp);
                                }
                            }
                        }
                    }

                }
            }
            for (int i = _uninitializedBlocks.Count - 1; i >= 0; i--)
            {

                var badBlock = _uninitializedBlocks[i];

                Ai gridAi;
                CoreComponent comp;
                if (EntityAIs.TryGetValue(badBlock.CubeGrid, out gridAi) && gridAi.CompBase.TryGetValue(badBlock, out comp))
                {
                    _uninitializedBlocks.RemoveAtFast(i);
                    continue;
                }

                if (badBlock.InScene)
                {
                    var lookSphere = new BoundingSphereD(badBlock.PositionComp.WorldAABB.Center, 30f);
                    if (Camera.IsInFrustum(ref lookSphere))
                    {
                        MyOrientedBoundingBoxD blockBox;
                        SUtils.GetBlockOrientedBoundingBox(badBlock, out blockBox);
                        DsDebugDraw.DrawBox(blockBox, _uninitializedColor);
                    }
                }
            }

            for (int i = 0; i < _debugBlocks.Count; i++)
            {

                var comp = _debugBlocks[i];
                if (comp.Cube.InScene)
                {

                    var lookSphere = new BoundingSphereD(comp.Cube.PositionComp.WorldAABB.Center, 100f);

                    if (Camera.IsInFrustum(ref lookSphere))
                    {
                        var collection = comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? comp.Platform.Weapons : comp.Platform.Phantoms;
                        foreach (var w in collection)
                        {

                            if (!w.TurretController && w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance == WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType.Smart)
                                w.SmartLosDebug();
                        }
                    }
                }
            }
        }

        private void UpdatePlacer()
        {
            if (!Placer.Visible) Placer = null;
            if (!MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.HitInfo.HasValue)
            {
                var hit = MyCubeBuilder.Static.HitInfo.Value as IHitInfo;
                var grid = hit.HitEntity as MyCubeGrid;
                Ai ai;
                if (grid != null && EntityToMasterAi.TryGetValue(grid, out ai))
                {
                    if (MyCubeBuilder.Static.CurrentBlockDefinition != null)
                    {
                        var subtypeIdHash = MyCubeBuilder.Static.CurrentBlockDefinition.Id.SubtypeId;
                        PartCounter partCounter;
                        if (ai.PartCounting.TryGetValue(subtypeIdHash, out partCounter))
                        {
                            if (partCounter.Max > 0 && ai.Construct.GetPartCount(subtypeIdHash) >= partCounter.Max)
                            {
                                MyCubeBuilder.Static.NotifyPlacementUnable();
                                MyCubeBuilder.Static.Deactivate();
                                return;
                            }
                        }

                        if (AreaRestrictions.ContainsKey(subtypeIdHash))
                        {
                            MyOrientedBoundingBoxD restrictedBox;
                            MyOrientedBoundingBoxD buildBox = MyCubeBuilder.Static.GetBuildBoundingBox();
                            BoundingSphereD restrictedSphere;
                            if (IsPartAreaRestricted(subtypeIdHash, buildBox, grid, 0, null, out restrictedBox, out restrictedSphere))
                            {
                                DsDebugDraw.DrawBox(buildBox, _uninitializedColor);
                            }

                            if (MyAPIGateway.Session.Config.HudState == 1)
                            {
                                if (restrictedBox.HalfExtent.AbsMax() > 0)
                                {
                                    DsDebugDraw.DrawBox(restrictedBox, _restrictionAreaColor);
                                }
                                if (restrictedSphere.Radius > 0)
                                {
                                    DsDebugDraw.DrawSphere(restrictedSphere, _restrictionAreaColor);
                                }

                                for (int i = 0; i < ai.WeaponComps.Count; i++)
                                {
                                    var comp = ai.WeaponComps[i];

                                    if (comp.IsBlock)
                                    {
                                        MyOrientedBoundingBoxD blockBox;
                                        SUtils.GetBlockOrientedBoundingBox(comp.Cube, out blockBox);

                                        BoundingSphereD s;
                                        MyOrientedBoundingBoxD b;
                                        CalculateRestrictedShapes(comp.SubTypeId, blockBox, out b, out s);

                                        if (s.Radius > 0)
                                        {
                                            DsDebugDraw.DrawSphere(s, _restrictionAreaColor);
                                        }
                                        if (b.HalfExtent.AbsMax() > 0)
                                        {
                                            DsDebugDraw.DrawBox(b, _restrictionAreaColor);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        internal void TargetSelection()
        {
            if (!InGridAiBlock) return;

            if (UiInput.AltPressed && UiInput.ShiftReleased || TargetUi.DrawReticle && UiInput.ClientInputState.MouseButtonRight && PlayerDummyTargets[PlayerId].PaintedTarget.EntityId == 0)
                TrackingAi.Construct.Focus.RequestReleaseActive(TrackingAi);

            if (UiInput.ActionKeyReleased && TrackingAi.Construct.Data.Repo.FocusData.HasFocus)
                TrackingAi.Construct.Focus.RequestAddLock(TrackingAi);

            if (!TrackingAi.IsGrid)
            {
                if (TrackingAi.WeaponComps.Count == 0) return;

                if (UiInput.MouseButtonRightWasPressed && TrackingAi.SmartHandheld && !TrackingAi.WeaponComps[0].Rifle.GunBase.HasIronSightsActive)
                    TrackingAi.Construct.Focus.RequestReleaseActive(TrackingAi);

                if (UiInput.ActionKeyPressed || UiInput.ActionKeyReleased && UiInput.FirstPersonView && TrackingAi.SmartHandheld && TrackingAi.WeaponComps[0].Rifle.GunBase.HasIronSightsActive)
                    TargetUi.SelectTarget(true, UiInput.ActionKeyPressed);

                return;
            }

            if (UiInput.MouseButtonRightNewPressed || UiInput.MouseButtonRightReleased && (TargetUi.DrawReticle || UiInput.FirstPersonView))
                TargetUi.SelectTarget(true, UiInput.MouseButtonRightNewPressed);
            else if (!UiInput.CameraBlockView)
            {
                if (UiInput.CurrentWheel != UiInput.PreviousWheel && !Settings.Enforcement.DisableTargetCycle)
                    TargetUi.SelectNext();
            }
        }

        internal void RemoveGps()
        {
            if (TargetGps != null)
            {
                if (TargetGps.ShowOnHud)
                {
                    MyAPIGateway.Session.GPS.RemoveLocalGps(TargetGps);
                    TargetGps.ShowOnHud = false;
                }

            }
        }

        internal void AddGps(Color color = default(Color))
        {
            if (TargetGps != null)
            {
                if (!TargetGps.ShowOnHud)
                {
                    TargetGps.ShowOnHud = true;
                    MyAPIGateway.Session.GPS.AddLocalGps(TargetGps);
                    if (color != default(Color))
                        MyVisualScriptLogicProvider.SetGPSColor(TargetGps?.Name, color);
                }
            }
        }

        internal void SetGpsInfo(Vector3D pos, string name, double dist = 0, Color color = default(Color))
        {
            if (TargetGps != null)
            {
                var newPos = dist > 0 ? pos + (Camera.WorldMatrix.Up * dist) : pos;
                TargetGps.Coords = newPos;
                TargetGps.Name = name;
                if (color != default(Color))
                    MyVisualScriptLogicProvider.SetGPSColor(TargetGps?.Name, color);
            }
        }

        internal bool CheckTarget(Ai ai)
        {
            if (!ai.Construct.Focus.ClientIsFocused(ai)) return false;
            if (ai != TrackingAi)
            {
                TrackingAi = null;
                return false;
            }

            return ai.Construct.Data.Repo.FocusData.HasFocus;
        }

        internal void SetTarget(MyEntity entity, Ai ai, Dictionary<MyEntity, MyTuple<float, TargetUi.TargetControl>> masterTargets)
        {

            TrackingAi = ai;
            ai.Construct.Focus.RequestAddFocus(entity, ai);

            Ai gridAi;
            TargetArmed = false;
            var grid = entity as MyCubeGrid;
            if (grid != null && EntityToMasterAi.TryGetValue(grid, out gridAi))
            {
                TargetArmed = true;
            }
            else
            {

                MyTuple<float, TargetUi.TargetControl> targetInfo;
                if (!masterTargets.TryGetValue(entity, out targetInfo)) return;
                ConcurrentDictionary<BlockTypes, ConcurrentCachingList<MyCubeBlock>> typeDict;

                var tGrid = entity as MyCubeGrid;
                if (tGrid != null && GridToBlockTypeMap.TryGetValue(tGrid, out typeDict))
                {

                    ConcurrentCachingList<MyCubeBlock> fatList;
                    if (typeDict.TryGetValue(Offense, out fatList))
                        TargetArmed = fatList.Count > 0;
                    else TargetArmed = false;
                }
                else TargetArmed = false;
            }
        }
    }
}
