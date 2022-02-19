using System.Collections.Generic;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;

namespace CoreSystems.Platform
{
    public partial class ControlSys
    {
        public class ControlComponent : CoreComponent
        {
            internal readonly ControlCompData Data;
            internal readonly ControlStructure Structure;

            internal bool RotorsDirty;
            internal bool RotorsMoving;
            internal Target.TargetStates OldState = Target.TargetStates.WasFake;
            internal uint LastCrashTick;


            internal ControlComponent(Session session, MyEntity coreEntity, MyDefinitionId id)
            {
                //Bellow order is important
                Data = new ControlCompData(this);
                Init(session, coreEntity, true, Data, id);
                Structure = (ControlStructure)Platform.Structure;
            }

            internal void DetectStateChanges()
            {
                if (Platform.State != CorePlatform.PlatformState.Ready)
                    return;

                if (Session.Tick - Ai.LastDetectEvent > 59)
                {
                    Ai.LastDetectEvent = Session.Tick;
                    Ai.SleepingComps = 0;
                    Ai.AwakeComps = 0;
                    Ai.DetectOtherSignals = false;
                }

                UpdatedState = true;


                DetectOtherSignals = false;
                if (DetectOtherSignals)
                    Ai.DetectOtherSignals = true;

                var wasAsleep = IsAsleep;
                IsAsleep = false;
                IsDisabled = false;

                if (!Ai.Session.IsServer)
                    return;

                var otherRangeSqr = Ai.DetectionInfo.OtherRangeSqr;
                var priorityRangeSqr = Ai.DetectionInfo.PriorityRangeSqr;
                var somethingInRange = DetectOtherSignals ? otherRangeSqr <= MaxDetectDistanceSqr && otherRangeSqr >= MinDetectDistanceSqr || priorityRangeSqr <= MaxDetectDistanceSqr && priorityRangeSqr >= MinDetectDistanceSqr : priorityRangeSqr <= MaxDetectDistanceSqr && priorityRangeSqr >= MinDetectDistanceSqr;

                if (Ai.Session.Settings.Enforcement.ServerSleepSupport && !somethingInRange && PartTracking == 0 && Ai.Construct.RootAi.Construct.ControllingPlayers.Count <= 0 && Session.TerminalMon.Comp != this && Data.Repo.Values.State.TerminalAction == TriggerActions.TriggerOff)
                {

                    IsAsleep = true;
                    Ai.SleepingComps++;
                }
                else if (wasAsleep)
                {

                    Ai.AwakeComps++;
                }
                else
                    Ai.AwakeComps++;
            }


            internal void ResetPlayerControl()
            {
                Data.Repo.Values.State.PlayerId = -1;
                Data.Repo.Values.State.Mode = ProtoControlState.ControlMode.None;
                Data.Repo.Values.Set.Overrides.Control = ProtoWeaponOverrides.ControlModes.Auto;

                var tAction = Data.Repo.Values.State.TerminalAction;
                if (tAction == TriggerActions.TriggerClick)
                    Data.Repo.Values.State.TerminalActionSetter(this, TriggerActions.TriggerOff, Session.MpActive);
                if (Session.MpActive)
                    Session.SendComp(this);
            }

            internal void StopRotors()
            {
                RotorsMoving = false;
                var baseMap = Platform.Control.BaseMap;
                var baseRotor = baseMap?.Stator;
                if (baseRotor == null) return;

                baseRotor.TargetVelocityRad = 0;

                var rootConstruct = Ai.Construct.RootAi.Construct;
                var mapList = rootConstruct.LocalStatorMaps[baseRotor.TopGrid as MyCubeGrid];

                foreach (var statorMap in mapList)
                    if (statorMap?.Stator != null)
                        statorMap.Stator.TargetVelocityRad = 0;

            }


            internal static void RequestSetValue(ControlComponent comp, string setting, int value, long playerId)
            {
                if (comp.Session.IsServer)
                {
                    SetValue(comp, setting, value, playerId);
                }
                else if (comp.Session.IsClient)
                {
                    comp.Session.SendOverRidesClientComp(comp, setting, value);
                }
            }

            internal static void SetValue(ControlComponent comp, string setting, int v, long playerId)
            {
                var o = comp.Data.Repo.Values.Set.Overrides;
                var enabled = v > 0;
                var clearTargets = false;

                switch (setting)
                {
                    case "MaxSize":
                        o.MaxSize = v;
                        break;
                    case "MinSize":
                        o.MinSize = v;
                        break;
                    case "SubSystems":
                        o.SubSystem = (WeaponDefinition.TargetingDef.BlockTypes)v;
                        break;
                    case "MovementModes":
                        o.MoveMode = (ProtoWeaponOverrides.MoveModes)v;
                        clearTargets = true;
                        break;
                    case "ControlModes":
                        o.Control = (ProtoWeaponOverrides.ControlModes)v;
                        clearTargets = true;
                        break;
                    case "FocusSubSystem":
                        o.FocusSubSystem = enabled;
                        break;
                    case "FocusTargets":
                        o.FocusTargets = enabled;
                        clearTargets = true;
                        break;
                    case "Unowned":
                        o.Unowned = enabled;
                        break;
                    case "Friendly":
                        o.Friendly = enabled;
                        clearTargets = true;
                        break;
                    case "Meteors":
                        o.Meteors = enabled;
                        break;
                    case "Grids":
                        o.Grids = enabled;
                        break;
                    case "Biologicals":
                        o.Biologicals = enabled;
                        break;
                    case "Projectiles":
                        o.Projectiles = enabled;
                        clearTargets = true;
                        break;
                    case "Neutrals":
                        o.Neutrals = enabled;
                        clearTargets = true;
                        break;
                    case "AiEnabled":
                        o.AiEnabled = enabled;
                        break;
                }

                ResetCompState(comp, playerId, clearTargets);

                if (comp.Session.MpActive)
                    comp.Session.SendComp(comp);
            }

            internal static void ResetCompState(ControlComponent comp, long playerId, bool resetTarget, Dictionary<string, int> settings = null)
            {
                var o = comp.Data.Repo.Values.Set.Overrides;
                var userControl = o.Control != ProtoWeaponOverrides.ControlModes.Auto;

                if (userControl)
                {
                    comp.Data.Repo.Values.State.PlayerId = playerId;
                    comp.Data.Repo.Values.State.Mode = ProtoControlState.ControlMode.Ui;
                    if (settings != null) settings["ControlModes"] = (int)o.Control;
                    comp.Data.Repo.Values.State.TerminalActionSetter(comp, TriggerActions.TriggerOff);
                }
                else
                {
                    comp.Data.Repo.Values.State.PlayerId = -1;
                    comp.Data.Repo.Values.State.Mode = ProtoControlState.ControlMode.None;
                }

                if (resetTarget)
                    ClearParts(comp);
            }

            internal static void SetRange(ControlComponent comp)
            {
                var w = comp.Platform.Control.TrackingWeapon;
                if (w == null) return;
                w.UpdateWeaponRange();
            }

            internal static void SetRof(ControlComponent comp)
            {
                for (int i = 0; i < comp.Platform.Support.Count; i++)
                {
                    var w = comp.Platform.Support[i];

                    //if (w.ActiveAmmoDef.AmmoDef.Const.MustCharge) continue;

                    //w.UpdateRof();
                }

                //SetDps(comp);
            }

            private static void ClearParts(ControlComponent comp)
            {
                for (int i = 0; i < comp.Platform.Upgrades.Count; i++)
                {
                    var part = comp.Platform.Upgrades[i];
                }
            }

            internal void TookControl(long playerId)
            {
                if (Session.IsServer)
                {

                    if (Data.Repo != null)
                    {
                        Data.Repo.Values.State.PlayerId = playerId;
                        Data.Repo.Values.State.Mode = ProtoControlState.ControlMode.Camera;

                        if (Session.MpActive)
                            Session.SendComp(this);
                    }
                    else
                        Log.Line($"OnPlayerController enter Repo null");

                }

                if (Session.HandlesInput)
                    Session.GunnerAcquire(Cube);
            }

            internal void ReleaseControl(long playerId)
            {
                if (Session.IsServer)
                {

                    if (Data.Repo != null)
                    {

                        Data.Repo.Values.State.PlayerId = -1;
                        Data.Repo.Values.State.Mode = ProtoControlState.ControlMode.Camera;

                        if (Session.MpActive)
                            Session.SendComp(this);
                    }
                    else
                        Log.Line($"OnPlayerController exit Repo null");
                }

                if (Session.HandlesInput)
                {
                    Session.GunnerRelease(Cube);
                }
            }
        }
    }
}
