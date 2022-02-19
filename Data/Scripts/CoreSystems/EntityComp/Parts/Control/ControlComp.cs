using System;
using System.Collections.Generic;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace CoreSystems.Platform
{
    public partial class ControlSys
    {
        public class ControlComponent : CoreComponent
        {
            internal readonly ControlCompData Data;
            internal readonly ControlStructure Structure;
            internal readonly IMyTurretControlBlock Controller;
            internal bool RotorsDirty;
            internal bool RotorsMoving;
            internal Target.TargetStates OldState = Target.TargetStates.WasFake;
            internal uint LastCrashTick;


            internal ControlComponent(Session session, MyEntity coreEntity, MyDefinitionId id)
            {
                Controller = (IMyTurretControlBlock)coreEntity;
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
                var mapList = rootConstruct.LocalStatorMaps[(MyCubeGrid)baseRotor.TopGrid];

                foreach (var statorMap in mapList)
                    if (statorMap.Stator != null)
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

            internal void RefreshRotorMaps(List<StatorMap> statorMap)
            {
                for (int j = 0; j < statorMap.Count; j++)
                {
                    var map = statorMap[j];
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

            internal void StatorMapCrashReport(IMyMotorStator azRaw, IMyMotorStator elRaw)
            {
                LastCrashTick = Session.Tick;
                Log.Line($"CTC failed to find stator in StatorMaps - entId:{CoreEntity.EntityId} - StatorMapsContainsAz:{Session.StatorMaps.ContainsKey(azRaw)} - StatorMapsContainsEl:{Session.StatorMaps.ContainsKey(elRaw)}");
            }

            internal bool GetTrackingComp(List<StatorMap> statorMaps)
            {
                var controlPart = Platform.Control;

                var trackingComp = controlPart.TrackingWeapon?.Comp;
                if (trackingComp == null || !trackingComp.IsFunctional || !trackingComp.IsWorking || trackingComp.IsDisabled)
                {
                    if (controlPart.NoValidWeapons && !Session.Tick60)
                    {
                        if (RotorsMoving)
                            StopRotors();
                        return false;
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
                    for (int j = 0; j < statorMaps.Count; j++)
                    {
                        if (foundWeapon)
                            break;

                        var map = statorMaps[j];
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
                                controlPart.TrackingWeapon.MasterComp = this;
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

                        if (RotorsMoving)
                            StopRotors();
                        return false;
                    }

                }

                var trackingWeapon = controlPart.TrackingWeapon;
                if (trackingWeapon?.Target == null)
                {
                    if (RotorsMoving)
                        StopRotors();
                    return false;
                }

                return true;
            }

            internal bool TrackTarget(List<StatorMap> statorMaps, ref Vector3D desiredDirection)
            {
                var trackingWeapon = Platform.Control.TrackingWeapon;
                var root = Platform.Control.BaseMap;
                RotorsMoving = true;
                var targetDistSqr = Vector3D.DistanceSquared(root.Stator.PositionComp.WorldAABB.Center, trackingWeapon.Target.TargetEntity.PositionComp.WorldAABB.Center);

                var epsilon = Session.Tick120 ? 1E-06d : targetDistSqr <= 640000 ? 1E-03d : targetDistSqr <= 3240000 ? 1E-04d : 1E-05d;

                var currentDirection = Platform.Control.TrackingScope.Info.Direction;
                var axis = Vector3D.Cross(desiredDirection, currentDirection);
                var deviationRads = MathHelper.ToRadians(Controller.AngleDeviation);

                //Root control
                var up = root.Stator.PositionComp.WorldMatrixRef.Up;
                var upZero = Vector3D.IsZero(up);
                var desiredFlat = upZero || Vector3D.IsZero(desiredDirection) ? Vector3D.Zero : desiredDirection - desiredDirection.Dot(up) * up;
                var currentFlat = upZero || Vector3D.IsZero(currentDirection) ? Vector3D.Zero : currentDirection - currentDirection.Dot(up) * up;
                var rootAngle = Vector3D.IsZero(desiredFlat) || Vector3D.IsZero(currentFlat) ? 0 : Math.Acos(MathHelper.Clamp(desiredFlat.Dot(currentFlat) / Math.Sqrt(desiredFlat.LengthSquared() * currentFlat.LengthSquared()), -1, 1));

                var rootOutsideLimits = false;
                if (MyUtils.IsZero((float) rootAngle, (float)epsilon))
                {
                    if (Session.IsServer)
                        root.Stator.TargetVelocityRad = 0;
                }
                else
                {
                    rootAngle *= Math.Sign(Vector3D.Dot(axis, up));

                    var desiredAngle = root.Stator.Angle + rootAngle;
                    rootOutsideLimits = desiredAngle < root.Stator.LowerLimitRad && desiredAngle + MathHelper.TwoPi > root.Stator.UpperLimitRad;

                    if ((desiredAngle < root.Stator.LowerLimitRad && desiredAngle + MathHelper.TwoPi < root.Stator.UpperLimitRad) || (desiredAngle > root.Stator.UpperLimitRad && desiredAngle - MathHelper.TwoPi > root.Stator.LowerLimitRad))
                        rootAngle = -Math.Sign(rootAngle) * (MathHelper.TwoPi - Math.Abs(rootAngle));

                    if (Session.IsServer)
                        root.Stator.TargetVelocityRad = rootOutsideLimits ? 0 : Math.Abs(Controller.VelocityMultiplierAzimuthRpm) * (float)rootAngle;
                }

                for (int j = 0; j < statorMaps.Count; j++)
                {
                    var map = statorMaps[j];
                    if (map.RotorMap.Scope == null)
                        continue;

                    currentDirection = map.RotorMap.Scope.Info.Direction;
                    up = map.Stator.PositionComp.WorldMatrixRef.Up;
                    upZero = Vector3D.IsZero(up);
                    desiredFlat = upZero || Vector3D.IsZero(desiredDirection) ? Vector3D.Zero : desiredDirection - desiredDirection.Dot(up) * up;
                    currentFlat = upZero || Vector3D.IsZero(currentDirection) ? Vector3D.Zero : currentDirection - currentDirection.Dot(up) * up;
                    var subAngle = Vector3D.IsZero(desiredFlat) || Vector3D.IsZero(currentFlat) ? 0 : Math.Acos(MathHelper.Clamp(desiredFlat.Dot(currentFlat) / Math.Sqrt(desiredFlat.LengthSquared() * currentFlat.LengthSquared()), -1, 1));

                    if (MyUtils.IsZero((float) subAngle, (float)epsilon) || !rootOutsideLimits && Math.Abs(rootAngle) > MathHelper.PiOver2)
                    {
                        //if (Tick60) Log.Line($"secondary isZero {MyUtils.IsZero(subAngle, (float)epsilon)} >2pi {Math.Abs(rootAngle) > MathHelper.PiOver2}");
                        if (Session.IsServer)
                            map.Stator.TargetVelocityRad = 0;
                    }
                    else
                    {
                        subAngle *= Math.Sign(Vector3D.Dot(axis, up));
                        var desiredAngle = map.Stator.Angle + subAngle;
                        var subOutsideLimits = desiredAngle < map.Stator.LowerLimitRad && desiredAngle + MathHelper.TwoPi > map.Stator.UpperLimitRad;

                        if ((desiredAngle < map.Stator.LowerLimitRad && desiredAngle + MathHelper.TwoPi < map.Stator.UpperLimitRad) || (desiredAngle > map.Stator.UpperLimitRad && desiredAngle - MathHelper.TwoPi > map.Stator.LowerLimitRad))
                            subAngle = -Math.Sign(subAngle) * (MathHelper.TwoPi - Math.Abs(subAngle));

                        if (Session.IsServer)
                            map.Stator.TargetVelocityRad = subOutsideLimits ? 0 : Math.Abs(Controller.VelocityMultiplierElevationRpm) * (float)subAngle;
                    }

                    if (rootAngle * rootAngle + subAngle * subAngle < deviationRads * deviationRads)
                    {
                        map.TopAi.RotorTurretAimed = true;
                    }
                }

                return true;
            }
        }
    }
}
