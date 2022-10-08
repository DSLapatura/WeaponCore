using System;
using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace CoreSystems
{
    internal static partial class BlockUi
    {
        internal static bool GetAiEnabledControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
            {
                return false;
            }
            return comp.Data.Repo.Values.Set.Overrides.AiEnabled;
        }

        internal static void RequestSetAiEnabledControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "AiEnabled", value, comp.Session.PlayerId);
        }

        internal static float GetRangeControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 100;
            return comp.Data.Repo.Values.Set.Range;
        }

        internal static void RequestSetRangeControl(IMyTerminalBlock block, float newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            if (!MyUtils.IsEqual(newValue, comp.Data.Repo.Values.Set.Range))
            {

                if (comp.Session.IsServer)
                {
                    comp.Data.Repo.Values.Set.Range = newValue;
                    //ControlSys.ControlComponent.SetRange(comp);
                    if (comp.Session.MpActive)
                        comp.Session.SendComp(comp);
                }
                else
                    comp.Session.SendSetCompFloatRequest(comp, newValue, PacketType.RequestSetRange);
            }

        }

        internal static long CtcGetShootModes(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 0;
            return (int)comp.Data.Repo.Values.Set.Overrides.ShootMode;
        }

        internal static void CtcRequestShootModes(IMyTerminalBlock block, long newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            ControlSys.ControlComponent.RequestSetValue(comp, "ShootMode", (int)newValue, comp.Session.PlayerId);
        }

        internal static void CtcListShootModes(List<MyTerminalControlComboBoxItem> shootModeList)
        {
            foreach (var sub in CtcShootModeList)
            {
                shootModeList.Add(sub);
            }
        }

        private static readonly List<MyTerminalControlComboBoxItem> CtcShootModeList = new List<MyTerminalControlComboBoxItem>
        {
            new MyTerminalControlComboBoxItem { Key = 0, Value = MyStringId.GetOrCompute($"{(Weapon.ShootManager.ShootModes)0}") },
            new MyTerminalControlComboBoxItem { Key = 1, Value = MyStringId.GetOrCompute($"{(Weapon.ShootManager.ShootModes)1}") },
            new MyTerminalControlComboBoxItem { Key = 2, Value = MyStringId.GetOrCompute($"{(Weapon.ShootManager.ShootModes)2}") },
            new MyTerminalControlComboBoxItem { Key = 3, Value = MyStringId.GetOrCompute($"{(Weapon.ShootManager.ShootModes)3}") },
        };

        internal static float GetMinRangeControl(IMyTerminalBlock block)
        {
            return 0;
        }

        internal static float GetMaxRangeControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready || comp.Platform.Control?.TrackingWeapon?.Comp?.Ai == null)
                return 0;

            return (float) comp.Platform.Control.TrackingWeapon.Comp.Ai.MaxTargetingRange;
        }

        internal static void RequestSetReportTargetControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            if (comp.Session.IsServer)
            {
                comp.Data.Repo.Values.Set.ReportTarget = newValue;
                if (comp.Session.MpActive)
                    comp.Session.SendComp(comp);
            }
            else
                comp.Session.SendSetCompBoolRequest(comp, newValue, PacketType.RequestSetReportTarget);
        }

        internal static bool GetReportTargetControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.ReportTarget;
        }

        internal static bool GetNeutralsControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Neutrals;
        }

        internal static void RequestSetNeutralsControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "Neutrals", value, comp.Session.PlayerId);
        }

        internal static bool GetUnownedControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Unowned;
        }

        internal static void RequestSetUnownedControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "Unowned", value, comp.Session.PlayerId);
        }

        internal static bool GetFocusFireControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.FocusTargets;
        }

        internal static void RequestSetFocusFireControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "FocusTargets", value, comp.Session.PlayerId);
        }

        internal static bool GetSubSystemsControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.FocusSubSystem;
        }

        internal static void RequestSetSubSystemsControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            var value = newValue ? 1 : 0;

            ControlSys.ControlComponent.RequestSetValue(comp, "FocusSubSystem", value, comp.Session.PlayerId);
        }

        internal static bool GetBiologicalsControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Biologicals;
        }

        internal static void RequestSetBiologicalsControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "Biologicals", value, comp.Session.PlayerId);
        }

        internal static bool GetProjectilesControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Projectiles;
        }

        internal static void RequestSetProjectilesControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "Projectiles", value, comp.Session.PlayerId);
        }

        internal static bool GetMeteorsControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Meteors;
        }

        internal static void RequestSetMeteorsControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "Meteors", value, comp.Session.PlayerId);
        }

        internal static bool GetGridsControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Grids;
        }

        internal static void RequestSetGridsControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "Grids", value, comp.Session.PlayerId);
        }

        internal static long GetSubSystemControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 0;
            return (int)comp.Data.Repo.Values.Set.Overrides.SubSystem;
        }

        internal static void RequestSubSystemControl(IMyTerminalBlock block, long newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            ControlSys.ControlComponent.RequestSetValue(comp, "SubSystems", (int) newValue, comp.Session.PlayerId);
        }

        internal static long GetMovementModeControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 0;
            return (int)comp.Data.Repo.Values.Set.Overrides.MoveMode;
        }

        internal static void RequestMovementModeControl(IMyTerminalBlock block, long newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            ControlSys.ControlComponent.RequestSetValue(comp, "MovementModes", (int)newValue, comp.Session.PlayerId);
        }

        internal static long GetControlModeControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return 0;
            return (int)comp.Data.Repo.Values.Set.Overrides.Control;
        }

        internal static void RequestControlModeControl(IMyTerminalBlock block, long newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            ControlSys.ControlComponent.RequestSetValue(comp, "ControlModes", (int)newValue, comp.Session.PlayerId);
        }

        internal static bool GetRepelControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.Repel;
        }

        internal static void RequestSetRepelControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "Repel", value, comp.Session.PlayerId);
        }

        internal static bool GetSmallGridControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.SmallGrid;
        }

        internal static void RequestSetSmallGridControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "SmallGrid", value, comp.Session.PlayerId);
        }

        internal static bool GetLargeGridControl(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return false;
            return comp.Data.Repo.Values.Set.Overrides.LargeGrid;
        }

        internal static void RequestSetLargeGridControl(IMyTerminalBlock block, bool newValue)
        {
            var comp = block?.Components?.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var value = newValue ? 1 : 0;
            ControlSys.ControlComponent.RequestSetValue(comp, "LargeGrid", value, comp.Session.PlayerId);
        }

    }
}
