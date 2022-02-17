using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.Utils;

namespace CoreSystems.Control
{
    public static class TerminalHelpers
    {
        internal static void AddUiControls<T>(Session session) where T : IMyTerminalBlock
        {
            //AddComboboxNoAction<T>(session, "Shoot Mode", Localization.GetText("TerminalShootModeTitle"), Localization.GetText("TerminalShootModeTooltip"), BlockUi.GetShootModes, BlockUi.RequestShootModes, BlockUi.ListShootModesNoBurst, Istrue);
            AddComboboxNoAction<T>(session, "Shoot Mode", Localization.GetText("TerminalShootModeTitle"), Localization.GetText("TerminalShootModeTooltip"), BlockUi.GetShootModes, BlockUi.RequestShootModes, BlockUi.ListShootModes, IsReady);

            AddSliderRof<T>(session, "Weapon ROF", Localization.GetText("TerminalWeaponROFTitle"), Localization.GetText("TerminalWeaponROFTooltip"), BlockUi.GetRof, BlockUi.RequestSetRof, UiRofSlider);

            AddCheckbox<T>(session, "Overload", Localization.GetText("TerminalOverloadTitle"), Localization.GetText("TerminalOverloadTooltip"), BlockUi.GetOverload, BlockUi.RequestSetOverload, true, UiOverLoad);


            AddWeaponCrticalTimeSliderRange<T>(session, "Detonation", Localization.GetText("TerminalDetonationTitle"), Localization.GetText("TerminalDetonationTooltip"), BlockUi.GetArmedTimer, BlockUi.RequestSetArmedTimer, NotCounting, CanBeArmed, BlockUi.GetMinCriticalTime, BlockUi.GetMaxCriticalTime, true);
            AddButtonNoAction<T>(session, "StartCount", Localization.GetText("TerminalStartCountTitle"), Localization.GetText("TerminalStartCountTooltip"), BlockUi.StartCountDown, NotCounting, CanBeArmed);
            AddButtonNoAction<T>(session, "StopCount", Localization.GetText("TerminalStopCountTitle"), Localization.GetText("TerminalStopCountTooltip"), BlockUi.StopCountDown, IsCounting, CanBeArmed);
            AddCheckboxNoAction<T>(session, "Arm", Localization.GetText("TerminalArmTitle"), Localization.GetText("TerminalArmTooltip"), BlockUi.GetArmed, BlockUi.RequestSetArmed, true, CanBeArmed);
            AddButtonNoAction<T>(session, "Trigger", Localization.GetText("TerminalTriggerTitle"), Localization.GetText("TerminalTriggerTooltip"), BlockUi.TriggerCriticalReaction, IsArmed, CanBeArmed);
        }

        internal static void AddTurretOrTrackingControls<T>(Session session) where T : IMyTerminalBlock
        {
            AddComboboxNoAction<T>(session, "ControlModes", Localization.GetText("TerminalControlModesTitle"), Localization.GetText("TerminalControlModesTooltip"), BlockUi.GetControlMode, BlockUi.RequestControlMode, BlockUi.ListControlModes, TurretOrGuidedAmmo);

            AddComboboxNoAction<T>(session, "PickAmmo", Localization.GetText("TerminalPickAmmoTitle"), Localization.GetText("TerminalPickAmmoTooltip"), BlockUi.GetAmmos, BlockUi.RequestSetAmmo, BlockUi.ListAmmos, AmmoSelection);

            AddComboboxNoAction<T>(session, "PickSubSystem", Localization.GetText("TerminalPickSubSystemTitle"), Localization.GetText("TerminalPickSubSystemTooltip"), BlockUi.GetSubSystem, BlockUi.RequestSubSystem, BlockUi.ListSubSystems, HasTracking);

            AddComboboxNoAction<T>(session, "TrackingMode", Localization.GetText("TerminalTrackingModeTitle"), Localization.GetText("TerminalTrackingModeTooltip"), BlockUi.GetMovementMode, BlockUi.RequestMovementMode, BlockUi.ListMovementModes, HasTracking);

            AddWeaponRangeSliderNoAction<T>(session, "Weapon Range", Localization.GetText("TerminalWeaponRangeTitle"), Localization.GetText("TerminalWeaponRangeTooltip"), BlockUi.GetRange, BlockUi.RequestSetRange, BlockUi.ShowRange, BlockUi.GetMinRange, BlockUi.GetMaxRange, true, false);

            Separator<T>(session, "WC_sep2", HasTracking);

            AddOnOffSwitchNoAction<T>(session, "ReportTarget", Localization.GetText("TerminalReportTargetTitle"), Localization.GetText("TerminalReportTargetTooltip"), BlockUi.GetReportTarget, BlockUi.RequestSetReportTarget, true, UiReportTarget);

            AddOnOffSwitchNoAction<T>(session, "Neutrals", Localization.GetText("TerminalNeutralsTitle"), Localization.GetText("TerminalNeutralsTooltip"), BlockUi.GetNeutrals, BlockUi.RequestSetNeutrals, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, "Unowned", Localization.GetText("TerminalUnownedTitle"), Localization.GetText("TerminalUnownedTooltip"), BlockUi.GetUnowned, BlockUi.RequestSetUnowned, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, "Biologicals", Localization.GetText("TerminalBiologicalsTitle"), Localization.GetText("TerminalBiologicalsTooltip"), BlockUi.GetBiologicals, BlockUi.RequestSetBiologicals, true, TrackBiologicals);

            AddOnOffSwitchNoAction<T>(session,  "Projectiles", Localization.GetText("TerminalProjectilesTitle"), Localization.GetText("TerminalProjectilesTooltip"), BlockUi.GetProjectiles, BlockUi.RequestSetProjectiles, true, TrackProjectiles);

            AddOnOffSwitchNoAction<T>(session, "Meteors", Localization.GetText("TerminalMeteorsTitle"), Localization.GetText("TerminalMeteorsTooltip"), BlockUi.GetMeteors, BlockUi.RequestSetMeteors, true, TrackMeteors);

            AddOnOffSwitchNoAction<T>(session,  "Grids", Localization.GetText("TerminalGridsTitle"), Localization.GetText("TerminalGridsTooltip"), BlockUi.GetGrids, BlockUi.RequestSetGrids, true, TrackGrids);

            AddOnOffSwitchNoAction<T>(session, "FocusFire", Localization.GetText("TerminalFocusFireTitle"), Localization.GetText("TerminalFocusFireTooltip"), BlockUi.GetFocusFire, BlockUi.RequestSetFocusFire, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, "SubSystems", Localization.GetText("TerminalSubSystemsTitle"), Localization.GetText("TerminalSubSystemsTooltip"), BlockUi.GetSubSystems, BlockUi.RequestSetSubSystems, true, HasTracking);

            AddOnOffSwitchNoAction<T>(session, "Repel", Localization.GetText("TerminalRepelTitle"), Localization.GetText("TerminalRepelTooltip"), BlockUi.GetRepel, BlockUi.RequestSetRepel, true, HasTracking);

            Separator<T>(session, "WC_sep3", IsTrue);

            AddWeaponBurstCountSliderRange<T>(session, "Burst Count", Localization.GetText("TerminalBurstShotsTitle"), Localization.GetText("TerminalBurstShotsTooltip"), BlockUi.GetBurstCount, BlockUi.RequestSetBurstCount, CanBurst, BlockUi.GetMinBurstCount, BlockUi.GetMaxBurstCount, true);
            AddWeaponBurstDelaySliderRange<T>(session, "Burst Delay", Localization.GetText("TerminalBurstDelayTitle"), Localization.GetText("TerminalBurstDelayTooltip"), BlockUi.GetBurstDelay, BlockUi.RequestSetBurstDelay, CanDelay, BlockUi.GetMinBurstDelay, BlockUi.GetMaxBurstDelay, true);
            AddWeaponSequenceIdSliderRange<T>(session, "Sequence Id", Localization.GetText("TerminalSequenceIdTitle"), Localization.GetText("TerminalSequenceIdTooltip"), BlockUi.GetSequenceId, BlockUi.RequestSetSequenceId, IsReady, BlockUi.GetMinSequenceId, BlockUi.GetMaxSequenceId, true);
            AddWeaponGroupIdIdSliderRange<T>(session, "Weapon Group Id", Localization.GetText("TerminalWeaponGroupIdTitle"), Localization.GetText("TerminalWeaponGroupIdTooltip"), BlockUi.GetWeaponGroupId, BlockUi.RequestSetWeaponGroupId, IsReady, BlockUi.GetMinWeaponGroupId, BlockUi.GetMaxWeaponGroupId, true);

            Separator<T>(session, "WC_sep4", IsTrue);

            AddLeadGroupSliderRange<T>(session, "Target Group", Localization.GetText("TerminalTargetGroupTitle"), Localization.GetText("TerminalTargetGroupTooltip"), BlockUi.GetLeadGroup, BlockUi.RequestSetLeadGroup, TargetLead, BlockUi.GetMinLeadGroup, BlockUi.GetMaxLeadGroup, true);
            AddWeaponCameraSliderRange<T>(session, "Camera Channel", Localization.GetText("TerminalCameraChannelTitle"), Localization.GetText("TerminalCameraChannelTooltip"), BlockUi.GetWeaponCamera, BlockUi.RequestSetBlockCamera, HasTracking, BlockUi.GetMinCameraChannel, BlockUi.GetMaxCameraChannel, true);

            Separator<T>(session, "WC_sep5", HasTracking);
        }

        internal static void AddTurretControlBlockControls<T>(Session session) where T : IMyTerminalBlock
        {
            AddOnOffSwitchNoAction<T>(session, "WCAiEnabled", Localization.GetText("TerminalAiEnabledTitle"), Localization.GetText("TerminalAiEnabledTooltip"), BlockUi.GetAiEnabledControl, BlockUi.RequestSetAiEnabledControl, true, IsReady);

            Separator<T>(session, "WC_sep2", IsTrue);

            AddWeaponCTCRangeSliderNoAction<T>(session, "Weapon Range", Localization.GetText("TerminalWeaponRangeTitle"), Localization.GetText("TerminalWeaponRangeTooltip"), BlockUi.GetRangeControl, BlockUi.RequestSetRangeControl, IsReady, BlockUi.GetMinRangeControl, BlockUi.GetMaxRangeControl, false, false);

            AddOnOffSwitchNoAction<T>(session, "ReportTarget", Localization.GetText("TerminalReportTargetTitle"), Localization.GetText("TerminalReportTargetTooltip"), BlockUi.GetReportTargetControl, BlockUi.RequestSetReportTargetControl, true, IsReady);

            AddOnOffSwitchNoAction<T>(session, "Neutrals", Localization.GetText("TerminalNeutralsTitle"), Localization.GetText("TerminalNeutralsTooltip"), BlockUi.GetNeutralsControl, BlockUi.RequestSetNeutralsControl, true, IsReady);

            AddOnOffSwitchNoAction<T>(session, "Unowned", Localization.GetText("TerminalUnownedTitle"), Localization.GetText("TerminalUnownedTooltip"), BlockUi.GetUnownedControl, BlockUi.RequestSetUnownedControl, true, IsReady);

            AddOnOffSwitchNoAction<T>(session, "Biologicals", Localization.GetText("TerminalBiologicalsTitle"), Localization.GetText("TerminalBiologicalsTooltip"), BlockUi.GetBiologicalsControl, BlockUi.RequestSetBiologicalsControl, true, IsReady);

            AddOnOffSwitchNoAction<T>(session, "Projectiles", Localization.GetText("TerminalProjectilesTitle"), Localization.GetText("TerminalProjectilesTooltip"), BlockUi.GetProjectilesControl, BlockUi.RequestSetProjectilesControl, true, IsReady);

            AddOnOffSwitchNoAction<T>(session, "Meteors", Localization.GetText("TerminalMeteorsTitle"), Localization.GetText("TerminalMeteorsTooltip"), BlockUi.GetMeteorsControl, BlockUi.RequestSetMeteorsControl, true, IsReady);

            AddOnOffSwitchNoAction<T>(session, "Grids", Localization.GetText("TerminalGridsTitle"), Localization.GetText("TerminalGridsTooltip"), BlockUi.GetGridsControl, BlockUi.RequestSetGridsControl, true, IsReady);

            AddOnOffSwitchNoAction<T>(session, "FocusFire", Localization.GetText("TerminalFocusFireTitle"), Localization.GetText("TerminalFocusFireTooltip"), BlockUi.GetFocusFireControl, BlockUi.RequestSetFocusFireControl, true, IsReady);

            AddOnOffSwitchNoAction<T>(session, "SubSystems", Localization.GetText("TerminalSubSystemsTitle"), Localization.GetText("TerminalSubSystemsTooltip"), BlockUi.GetSubSystemsControl, BlockUi.RequestSetSubSystemsControl, true, IsReady);

            AddOnOffSwitchNoAction<T>(session, "Repel", Localization.GetText("TerminalRepelTitle"), Localization.GetText("TerminalRepelTooltip"), BlockUi.GetRepelControl, BlockUi.RequestSetRepelControl, true, IsReady);

            Separator<T>(session, "WC_sep3", IsTrue);

            AddComboboxNoAction<T>(session, "PickSubSystem", Localization.GetText("TerminalPickSubSystemTitle"), Localization.GetText("TerminalPickSubSystemTooltip"), BlockUi.GetSubSystemControl, BlockUi.RequestSubSystemControl, BlockUi.ListSubSystems, IsReady);

            AddComboboxNoAction<T>(session, "TrackingMode", Localization.GetText("TerminalTrackingModeTitle"), Localization.GetText("TerminalTrackingModeTooltip"), BlockUi.GetMovementModeControl, BlockUi.RequestMovementModeControl, BlockUi.ListMovementModes, IsReady);

            //AddComboboxNoAction<T>(session, "ControlModes", Localization.GetText("TerminalControlModesTitle"), Localization.GetText("TerminalControlModesTooltip"), BlockUi.GetControlModeControl, BlockUi.RequestControlModeControl, BlockUi.ListControlModes, IsTrue);

            //AddWeaponCameraSliderRange<T>(session, "Camera Channel", Localization.GetText("TerminalCameraChannelTitle"), Localization.GetText("TerminalCameraChannelTooltip"), BlockUi.GetWeaponCamera, BlockUi.RequestSetBlockCamera, HasTracking, BlockUi.GetMinCameraChannel, BlockUi.GetMaxCameraChannel, true);

            //AddLeadGroupSliderRange<T>(session, "Target Group", Localization.GetText("TerminalTargetGroupTitle"), Localization.GetText("TerminalTargetGroupTooltip"), BlockUi.GetLeadGroup, BlockUi.RequestSetLeadGroup, TargetLead, BlockUi.GetMinLeadGroup, BlockUi.GetMaxLeadGroup, true);

            Separator<T>(session, "WC_sep4", IsTrue);
        }

        internal static void AddDecoyControls<T>(Session session) where T : IMyTerminalBlock
        {
            Separator<T>(session, "WC_decoySep1", IsTrue);
            AddComboboxDecoyNoAction<T>(session, "PickSubSystem", Localization.GetText("TerminalDecoyPickSubSystemTitle"), Localization.GetText("TerminalDecoyPickSubSystemTooltip"), BlockUi.GetDecoySubSystem, BlockUi.RequestDecoySubSystem, BlockUi.ListDecoySubSystems, IsDecoy);
        }

        internal static void AddCameraControls<T>(Session session) where T : IMyTerminalBlock
        {
            Separator<T>(session,  "WC_cameraSep1", IsTrue);
            AddBlockCameraSliderRange<T>(session, "WC_PickCameraChannel", Localization.GetText("TerminalCameraCameraChannelTitle"), Localization.GetText("TerminalCameraCameraChannelTooltip"), BlockUi.GetBlockCamera, BlockUi.RequestBlockCamera, BlockUi.ShowCamera, BlockUi.GetMinCameraChannel, BlockUi.GetMaxCameraChannel, true);
        }

        internal static void CreateGenericControls<T>(Session session) where T : IMyTerminalBlock
        {
            AddOnOffSwitchNoAction<T>(session,  "Debug", Localization.GetText("TerminalDebugTitle"), Localization.GetText("TerminalDebugTooltip"), BlockUi.GetDebug, BlockUi.RequestDebug, true, GuidedAmmoNoTurret);

            Separator<T>(session, "WC_sep4", IsTrue);
            AddOnOffSwitchNoAction<T>(session,  "Shoot", Localization.GetText("TerminalShootTitle"), Localization.GetText("TerminalShootTooltip"), BlockUi.GetShoot, BlockUi.RequestSetShoot, true, IsNotBomb);
            AddOnOffSwitchNoAction<T>(session, "Override", Localization.GetText("TerminalOverrideTitle"), Localization.GetText("TerminalOverrideTooltip"), BlockUi.GetOverride, BlockUi.RequestOverride, true, OverrideTarget);
        }

        internal static void CreateGenericArmor<T>(Session session) where T : IMyTerminalBlock
        {
            AddOnOffSwitchNoAction<T>(session, "Show Enhanced Area", "Area Influence", "Show On/Off", BlockUi.GetShowArea, BlockUi.RequestSetShowArea, true, SupportIsReady);
        }

        internal static bool IsTrue(IMyTerminalBlock block)
        {
            return true;
        }

        internal static bool IsCamera(IMyTerminalBlock block)
        {
            return block is IMyCameraBlock;
        }


        internal static bool ShootBurstWeapon(IMyTerminalBlock block)
        {
            var comp = block.Components.Get<CoreComponent>();

            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Type == CoreComponent.CompType.Weapon;
        }


        internal static bool WeaponIsReady(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Type == CoreComponent.CompType.Weapon;
        }

        internal static bool WeaponIsReadyAndSorter(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Type == CoreComponent.CompType.Weapon && comp.TypeSpecific == CoreComponent.CompTypeSpecific.SorterWeapon;
        }

        internal static bool SupportIsReady(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.TypeSpecific == CoreComponent.CompTypeSpecific.Support;
        }

        internal static bool UiRofSlider(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasRofSlider;
        }

        internal static bool UiOverLoad(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.CanOverload;
        }

        internal static bool UiReportTarget(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasRequireTarget;
        }

        internal static bool TrackMeteors(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.IsBlock && (comp.HasTurret || comp.TrackingWeapon.System.HasGuidedAmmo) && comp.TrackingWeapon.System.TrackMeteors;
        }

        internal static bool TrackGrids(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return block is IMyTurretControlBlock || comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.IsBlock && (comp.HasTurret || comp.TrackingWeapon.System.HasGuidedAmmo) && comp.TrackingWeapon.System.TrackGrids;
        }

        internal static bool TrackProjectiles(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.IsBlock && (comp.HasTurret || comp.TrackingWeapon.System.HasGuidedAmmo) && comp.TrackingWeapon.System.TrackProjectile;
        }

        internal static bool TrackBiologicals(IMyTerminalBlock block)
        {

            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.IsBlock && (comp.HasTurret || comp.TrackingWeapon.System.HasGuidedAmmo) && comp.TrackingWeapon.System.TrackCharacters;
        }

        internal static bool AmmoSelection(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.ConsumableSelectionPartIds.Count > 0 && comp.Type == CoreComponent.CompType.Weapon;
        }

        internal static bool HasTracking(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && (comp.HasTracking || comp.HasGuidance);
        }

        internal static bool CanBurst(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && !comp.HasDisabledBurst;
        }

        internal static bool NoBurst(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasDisabledBurst;
        }

        internal static bool CanDelay(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready;
        }

        internal static bool CanBeArmed(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasArming;
        }

        internal static bool IsCounting(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Data.Repo.Values.State.CountingDown;
        }

        internal static bool NotCounting(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && !comp.Data.Repo.Values.State.CountingDown;
        }

        internal static bool IsArmed(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Data.Repo.Values.Set.Overrides.Armed;
        }

        internal static bool IsReady(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready;
        }


        internal static bool IsDecoy(IMyTerminalBlock block)
        {
            return block is IMyDecoy;
        }

        internal static bool IsNotBomb(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && !comp.IsBomb;
        }
        internal static bool HasSupport(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Type == CoreComponent.CompType.Support;
        }

        internal static bool HasTurret(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasTurret && comp.Type == CoreComponent.CompType.Weapon; 
        }

        internal static bool NoTurret(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && !comp.HasTurret && comp.Type == CoreComponent.CompType.Weapon;
        }

        internal static bool TargetLead(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.Type == CoreComponent.CompType.Weapon && !comp.IsBomb && (!comp.HasTurret && !comp.OverrideLeads || comp.HasTurret && comp.OverrideLeads);
        }

        internal static bool GuidedAmmo(IMyTerminalBlock block, bool checkFixed = false)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.TrackingWeapon.System.HasGuidedAmmo && (!checkFixed || comp.TrackingWeapon.System.TurretMovement != WeaponSystem.TurretType.Fixed);
        }
        internal static bool TurretOrGuidedAmmo(IMyTerminalBlock block)
        {
            return HasTurret(block) || GuidedAmmo(block, true);
        }

        internal static bool TurretOrGuidedAmmoAny(IMyTerminalBlock block)
        {
            return HasTurret(block) || GuidedAmmo(block, false);
        }
        internal static bool GuidedAmmoNoTurret(IMyTerminalBlock block)
        {
            return GuidedAmmo(block) && NoTurret(block);
        }

        internal static bool OverrideTarget(IMyTerminalBlock block)
        {
            var comp = block?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
            return comp != null && comp.Platform.State == CorePlatform.PlatformState.Ready && comp.HasRequireTarget;
        }

        internal static void SliderWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append(BlockUi.GetRange(block).ToString("N2"));
        }

        internal static void SliderCTCWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append(BlockUi.GetMaxRangeControl(block).ToString("N2"));
        }

        internal static void SliderWriterRof(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append(BlockUi.GetRof(block).ToString("N2"));
        }

        internal static void EmptyStringBuilder(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append("");
        }

        internal static bool NotWcBlock(IMyTerminalBlock block)
        {
            return !block.Components.Has<CoreComponent>(); 
        }

        internal static bool NotWcOrIsTurret(IMyTerminalBlock block)
        {
            CoreComponent comp;
            return !block.Components.TryGet(out comp) || comp is ControlSys.ControlComponent || comp.HasTurret;
        }

        internal static void SliderBlockCameraWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {
            long value = -1;
            string message;
            if (string.IsNullOrEmpty(block.CustomData) || long.TryParse(block.CustomData, out value))
            {
                var group = value >= 0 ? value : 0;
                message = value == 0 ? "Disabled" : group.ToString();
            }
            else message = "Invalid CustomData";

            builder.Append(message);
        }

        internal static void SliderWeaponCameraWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {

            var value = (long)Math.Round(BlockUi.GetWeaponCamera(block), 0);
            var message = value > 0 ? value.ToString() : "Disabled";

            builder.Append(message);
        }

        internal static void SliderWeaponBurstCountWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {

            var value = (long)Math.Round(BlockUi.GetBurstCount(block), 0);
            var message = value > 0 ? value.ToString() : "Disabled";

            builder.Append(message);
        }

        internal static void SliderWeaponBurstDelayWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {

            var value = (long)Math.Round(BlockUi.GetBurstDelay(block), 0);
            var message = value > 0 ? value.ToString() : "Disabled";

            builder.Append(message);
        }

        internal static void SliderWeaponSequenceIdWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {

            var value = (long)Math.Round(BlockUi.GetSequenceId(block), 0);
            var message = value > 0 ? value.ToString() : "Disabled";

            builder.Append(message);
        }

        internal static void SliderWeaponGroupIdWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {

            var value = (long)Math.Round(BlockUi.GetWeaponGroupId(block), 0);
            var message = value > 0 ? value.ToString() : "Disabled";

            builder.Append(message);
        }

        internal static void SliderCriticalTimerWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {

            var value = BlockUi.GetArmedTimeRemaining(block);

            if (value >= 59.95)
                builder.Append("00:01:00");
            else if (value < 0.33)
                builder.Append("00:00:00");
            else
            {
                builder.Append("00:")
                    .Append(value.ToString("00:00"));
            }
        }

        internal static void SliderLeadGroupWriterRange(IMyTerminalBlock block, StringBuilder builder)
        {

            var value = (long)Math.Round(BlockUi.GetLeadGroup(block), 0);
            var message = value > 0 ? value.ToString() : "Disabled";

            builder.Append(message);
        }

        #region terminal control methods
        internal static IMyTerminalControlSlider AddBlockCameraSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = IsCamera;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderBlockCameraWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            CreateCustomActions<T>.CreateSliderActionSet(session, c, title, 0, 1, .1f, visibleGetter, group);
            return c;
        }


        internal static IMyTerminalControlSlider AddWeaponCameraSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = IsReady;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWeaponCameraWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddWeaponBurstCountSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = IsReady;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWeaponBurstCountWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddWeaponBurstDelaySliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = IsReady;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWeaponBurstDelayWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddWeaponSequenceIdSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = IsTrue;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWeaponSequenceIdWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddWeaponGroupIdIdSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = IsReady;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWeaponGroupIdWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }


        internal static IMyTerminalControlSlider AddWeaponCrticalTimeSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> enableGetter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = enableGetter;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderCriticalTimerWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddLeadGroupSliderRange<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = IsReady;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderLeadGroupWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);
            return c;
        }

        internal static IMyTerminalControlOnOffSwitch AddWeaponOnOff<T>(Session session, string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, int, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, bool> visibleGetter) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>($"WC_Enable");

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute(onText);
            c.OffText = MyStringId.GetOrCompute(offText);
            c.Enabled = IsReady;
            c.Visible = visibleGetter;
            c.Getter = IsReady;
            c.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            CreateCustomActions<T>.CreateOnOffActionSet(session, c, name, visibleGetter);

            return c;
        }

        internal static IMyTerminalControlSeparator Separator<T>(Session session, string name, Func<IMyTerminalBlock,bool> visibleGettter) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(name);

            c.Enabled = IsTrue;
            c.Visible = visibleGettter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddWeaponRangeSliderNoAction<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false, bool addAction = true) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = IsTrue;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddWeaponCTCRangeSliderNoAction<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null, bool group = false, bool addAction = true) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = IsTrue;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderCTCWriterRange;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddSliderRof<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visibleGetter, Func<IMyTerminalBlock, float> minGetter = null, Func<IMyTerminalBlock, float> maxGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = IsReady;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            c.Writer = SliderWriterRof;

            if (minGetter != null)
                c.SetLimits(minGetter, maxGetter);

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            CreateCustomActions<T>.CreateSliderActionSet(session, c, name, 0, 1, .1f, visibleGetter, false);
            return c;
        }

        internal static IMyTerminalControlCheckbox AddCheckbox<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, bool allowGroup, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = visibleGetter;
            c.Enabled = IsReady;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            CreateCustomActions<T>.CreateOnOffActionSet(session, c, name, visibleGetter, allowGroup);

            return c;
        }

        internal static IMyTerminalControlCheckbox AddCheckboxNoAction<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, bool allowGroup, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = visibleGetter;
            c.Enabled = IsReady;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlOnOffSwitch AddOnOffSwitchNoAction<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, bool allowGroup, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>("WC_" + name);
            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute(Localization.GetText("TerminalSwitchOn"));
            c.OffText = MyStringId.GetOrCompute(Localization.GetText("TerminalSwitchOff"));
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = visibleGetter;
            c.Enabled = IsReady;
            
            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlCombobox AddComboboxNoAction<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter, Action<List<MyTerminalControlComboBoxItem>> fillAction, Func<IMyTerminalBlock,  bool> visibleGetter = null) where T : IMyTerminalBlock {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.ComboBoxContent = fillAction;
            c.Getter = getter;
            c.Setter = setter;

            c.Visible = visibleGetter;
            c.Enabled = IsReady;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        internal static IMyTerminalControlCombobox AddComboboxDecoyNoAction<T>(Session session, string name, string title, string tooltip, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter, Action<List<MyTerminalControlComboBoxItem>> fillAction, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.ComboBoxContent = fillAction;
            c.Getter = getter;
            c.Setter = setter;

            c.Visible = visibleGetter;
            c.Enabled = IsDecoy;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }


        internal static IMyTerminalControlButton AddButtonNoAction<T>(Session session, string name, string title, string tooltip, Action<IMyTerminalBlock> action, Func<IMyTerminalBlock, bool> enableGetter, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Action = action;
            c.Visible = visibleGetter;
            c.Enabled = enableGetter;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            session.CustomControls.Add(c);

            return c;
        }

        #endregion
    }
}
