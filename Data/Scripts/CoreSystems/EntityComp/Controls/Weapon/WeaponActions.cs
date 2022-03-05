using System;
using System.Text;
using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.ModAPI;
using VRageMath;
using static CoreSystems.Support.CoreComponent.Trigger;

namespace CoreSystems.Control
{
    public static partial class CustomActions
    {
        #region Call Actions
        internal static void TerminalActionToggleShoot(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var on = comp.Data.Repo.Values.State.Trigger == On;
            comp.ShootManager.RequestShootSync(comp.Session.PlayerId, on ? Weapon.ShootManager.RequestType.Off : Weapon.ShootManager.RequestType.On, Weapon.ShootManager.Signals.On);
        }

        internal static void TerminalActionFriend(IMyTerminalBlock blk)
        {

            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready || comp.Session.DedicatedServer)
                return;

            comp.RequestFriend();
        }

        internal static void TerminalActionEnemy(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready || comp.Session.DedicatedServer)
                return;

            comp.RequestEnemy();
        }

        internal static void TerminalActionPosition(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready || comp.Session.DedicatedServer)
                return;

            comp.RequestPoint();
        }

        internal static void TerminalActionShootOn(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var state = comp.Data.Repo.Values.State;
            var mgr = comp.ShootManager;

            if (mgr.ClientToggleCount > state.ToggleCount || state.Trigger == On)
                return;

            comp.ShootManager.RequestShootSync(comp.Session.PlayerId, Weapon.ShootManager.RequestType.On, Weapon.ShootManager.Signals.On);
        }

        internal static void TerminalActionShootOff(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var state = comp.Data.Repo.Values.State;
            var mgr = comp.ShootManager;

            if (mgr.ClientToggleCount <= state.ToggleCount && state.Trigger != On)
                return;

            comp.ShootManager.RequestShootSync(comp.Session.PlayerId, Weapon.ShootManager.RequestType.Off);
        }

        internal static void TerminalActionKeyShoot(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var mode = comp.Data.Repo.Values.Set.Overrides.ShootMode;
            if (mode == Weapon.ShootManager.ShootModes.KeyToggle || mode == Weapon.ShootManager.ShootModes.KeyFire)
            {
                var keyToggle = mode == Weapon.ShootManager.ShootModes.KeyToggle;
                var signal = keyToggle ? Weapon.ShootManager.Signals.KeyToggle : Weapon.ShootManager.Signals.Once;
                var on = comp.Data.Repo.Values.State.Trigger == On;
                var onOff = on ? Weapon.ShootManager.RequestType.Off : Weapon.ShootManager.RequestType.On;
                comp.ShootManager.RequestShootSync(comp.Session.PlayerId, keyToggle ? onOff : Weapon.ShootManager.RequestType.Once, signal);
            }
        }

        internal static void TerminalRequestReload(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            comp.RequestForceReload();
        }

        internal static void TerminalActionControlMode(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;
            
            var numValue = (int)comp.Data.Repo.Values.Set.Overrides.Control;
            var value = numValue + 1 <= 2 ? numValue + 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "ControlModes", value, comp.Session.PlayerId);
        }

        internal static void TerminalActionMovementMode(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var numValue = (int)comp.Data.Repo.Values.Set.Overrides.MoveMode;
            var value = numValue + 1 <= 3 ? numValue + 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "MovementModes", value, comp.Session.PlayerId);
        }


        internal static void TerminActionCycleSubSystem(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var numValue = (int)comp.Data.Repo.Values.Set.Overrides.SubSystem;
            var value = numValue + 1 <= 7 ? numValue + 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "SubSystems", value, comp.Session.PlayerId);
        }

        internal static void TerminActionCycleShootMode(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready || !BlockUi.ShootModeChangeReady(comp))
                return;

            var numValue = (int)comp.Data.Repo.Values.Set.Overrides.ShootMode;
            var value = numValue + 1 <= 3 ? numValue + 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "ShootMode", value, comp.Session.PlayerId);
        }


        internal static void TerminActionCycleMouseControl(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready || !BlockUi.ShootModeChangeReady(comp))
                return;

            var numValue = (int)comp.Data.Repo.Values.Set.Overrides.ShootMode;
            var value = numValue == 1 ? 0 : 1;

            Weapon.WeaponComponent.RequestSetValue(comp, "ShootMode", value, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleNeutrals(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.Neutrals;
            var newValue = newBool ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "Neutrals", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleProjectiles(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.Projectiles;
            var newValue = newBool ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "Projectiles", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleBiologicals(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.Biologicals;
            var newValue = newBool ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "Biologicals", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleMeteors(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.Meteors;
            var newValue = newBool ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "Meteors", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleGrids(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.Grids;
            var newValue = newBool ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "Grids", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleFriendly(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.Friendly;
            var newValue = newBool ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "Friendly", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleUnowned(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.Unowned;
            var newValue = newBool ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "Unowned", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleFocusTargets(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.FocusTargets;
            var newValue = newBool ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "FocusTargets", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionToggleFocusSubSystem(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.FocusSubSystem;
            var newValue = newBool ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "FocusSubSystem", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMaxSizeIncrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Values.Set.Overrides.MaxSize * 2;
            var newValue = nextValue > 0 && nextValue < 16384 ? nextValue : 16384;

            Weapon.WeaponComponent.RequestSetValue(comp, "MaxSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMaxSizeDecrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Values.Set.Overrides.MaxSize / 2;
            var newValue = nextValue > 0 && nextValue < 16384 ? nextValue : 1;

            Weapon.WeaponComponent.RequestSetValue(comp, "MaxSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMinSizeIncrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Values.Set.Overrides.MinSize == 0 ? 1 : comp.Data.Repo.Values.Set.Overrides.MinSize * 2;
            var newValue = nextValue > 0 && nextValue < 128 ? nextValue : 128;

            Weapon.WeaponComponent.RequestSetValue(comp, "MinSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionMinSizeDecrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var nextValue = comp.Data.Repo.Values.Set.Overrides.MinSize / 2;
            var newValue = nextValue > 0 && nextValue < 128 ? nextValue : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "MinSize", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionCycleAmmo(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Platform.State != CorePlatform.PlatformState.Ready) return;
            for (int i = 0; i < comp.Collection.Count; i++)
            {
                var w = comp.Collection[i];

                if (!w.System.HasAmmoSelection)
                    continue;

                var availAmmo = w.System.AmmoTypes.Length;
                var aId = w.DelayedCycleId >= 0 ? w.DelayedCycleId : w.Reload.AmmoTypeId;
                var currActive = w.System.AmmoTypes[aId];
                var next = (aId + 1) % availAmmo;
                var currDef = w.System.AmmoTypes[next];

                var change = false;

                while (!(currActive.Equals(currDef)))
                {
                    if (currDef.AmmoDef.Const.IsTurretSelectable)
                    {
                        change = true;
                        break;
                    }

                    next = (next + 1) % availAmmo;
                    currDef = w.System.AmmoTypes[next];
                }

                if (change)
                {
                    w.QueueAmmoChange(next);
                }
            }
        }

        internal static void TerminActionCycleDecoy(IMyTerminalBlock blk)
        {
            long valueLong;
            long.TryParse(blk.CustomData, out valueLong);
            var value = valueLong + 1 <= 7 ? valueLong + 1 : 1;
            blk.CustomData = value.ToString();
            blk.RefreshCustomInfo();
        }
        internal static void TerminalActionToggleRepelMode(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent; ;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var newBool = !comp.Data.Repo.Values.Set.Overrides.Repel;
            var newValue = newBool ? 1 : 0;

            Weapon.WeaponComponent.RequestSetValue(comp, "Repel", newValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionCameraChannelIncrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent; ;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var value = comp.Data.Repo.Values.Set.Overrides.CameraChannel;
            var nextValue = MathHelper.Clamp(value + 1, 0, 24);

            Weapon.WeaponComponent.RequestSetValue(comp, "CameraChannel", nextValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionCameraChannelDecrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent; ;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var value = comp.Data.Repo.Values.Set.Overrides.CameraChannel;
            var nextValue = MathHelper.Clamp(value - 1, 0, 24);

            Weapon.WeaponComponent.RequestSetValue(comp, "CameraChannel", nextValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionLeadGroupIncrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent; ;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var value = Convert.ToInt32(comp.Data.Repo.Values.Set.Overrides.LeadGroup);
            var nextValue = MathHelper.Clamp(value + 1, 0, 5);

            Weapon.WeaponComponent.RequestSetValue(comp, "LeadGroup", nextValue, comp.Session.PlayerId);
        }

        internal static void TerminalActionLeadGroupDecrease(IMyTerminalBlock blk)
        {
            var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent; ;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                return;

            var value = Convert.ToInt32(comp.Data.Repo.Values.Set.Overrides.LeadGroup);
            var nextValue = MathHelper.Clamp(value - 1, 0, 5);

            Weapon.WeaponComponent.RequestSetValue(comp, "LeadGroup", nextValue, comp.Session.PlayerId);
        }
        internal static void TerminalActionCameraIncrease(IMyTerminalBlock blk)
        {
            long valueLong;
            long.TryParse(blk.CustomData, out valueLong);
            var value = valueLong + 1 <= 7 ? valueLong + 1 : 1;
            blk.CustomData = value.ToString();
            blk.RefreshCustomInfo();
        }

        internal static void TerminalActionCameraDecrease(IMyTerminalBlock blk)
        {
            long valueLong;
            long.TryParse(blk.CustomData, out valueLong);
            var value = valueLong + 1 <= 7 ? valueLong + 1 : 1;
            blk.CustomData = value.ToString();
            blk.RefreshCustomInfo();
        }
        #endregion

        #region Writters

        internal static void ShootStateWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.State.Trigger == On)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }

        internal static void NeutralWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.Set.Overrides.Neutrals)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }

        internal static void ProjectilesWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.Set.Overrides.Projectiles)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }

        internal static void BiologicalsWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.Set.Overrides.Biologicals)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }

        internal static void MeteorsWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.Set.Overrides.Meteors)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }

        internal static void GridsWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.Set.Overrides.Grids)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }

        internal static void FriendlyWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.Set.Overrides.Friendly)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }

        internal static void UnownedWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.Set.Overrides.Unowned)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }

        internal static void FocusTargetsWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.Set.Overrides.FocusTargets)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }

        internal static void FocusSubSystemWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.Set.Overrides.FocusSubSystem)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }

        internal static void MaxSizeWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            sb.Append(comp.Data.Repo.Values.Set.Overrides.MaxSize);
        }

        internal static void MinSizeWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            sb.Append(comp.Data.Repo.Values.Set.Overrides.MinSize);
        }

        internal static void ControlStateWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            sb.Append(comp.Data.Repo.Values.Set.Overrides.Control);
        }

        internal static void MovementModeWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            sb.Append(comp.Data.Repo.Values.Set.Overrides.MoveMode);
        }

        internal static void SubSystemWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            sb.Append(comp.Data.Repo.Values.Set.Overrides.SubSystem);
        }

        internal static void ShootModeWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var altAiControlName = !comp.HasAim && comp.Data.Repo.Values.Set.Overrides.ShootMode == Weapon.ShootManager.ShootModes.AiShoot ? InActive : comp.Data.Repo.Values.Set.Overrides.ShootMode.ToString();
            sb.Append(altAiControlName);
        }

        private const string InActive = "Inactive";
        internal static void MouseToggleWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            var message = comp.Data.Repo.Values.Set.Overrides.ShootMode == Weapon.ShootManager.ShootModes.MouseControl ? comp.Data.Repo.Values.Set.Overrides.ShootMode.ToString() : InActive; 

            sb.Append(message);
        }

        internal static void DecoyWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            long value;
            if (long.TryParse(blk.CustomData, out value))
            {
                sb.Append(((WeaponDefinition.TargetingDef.BlockTypes)value).ToString());
            }
        }

        internal static void CameraWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            long value;
            if (long.TryParse(blk.CustomData, out value))
            {
                var group = $"Camera Channel {value}";
                sb.Append(group);
            }
        }

        internal static void WeaponCameraChannelWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            sb.Append(comp.Data.Repo.Values.Set.Overrides.CameraChannel);
        }

        internal static void LeadGroupWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            sb.Append(comp.Data.Repo.Values.Set.Overrides.LeadGroup);
        }

        internal static void AmmoSelectionWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready || comp.ConsumableSelectionPartIds.Count == 0) return;
            var w = comp.Collection[comp.ConsumableSelectionPartIds[0]];
            sb.Append(w.AmmoName);
        }

        internal static void RepelWriter(IMyTerminalBlock blk, StringBuilder sb)
        {
            var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            if (comp.Data.Repo.Values.Set.Overrides.Repel)
                sb.Append(Localization.GetText("ActionStateOn"));
            else
                sb.Append(Localization.GetText("ActionStateOff"));
        }
        #endregion
    }
}
