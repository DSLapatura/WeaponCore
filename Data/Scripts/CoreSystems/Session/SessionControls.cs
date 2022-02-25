using System;
using System.Collections.Generic;
using System.Text;
using CoreSystems.Control;
using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Utils;
using static CoreSystems.Support.CoreComponent.Trigger;
using static CoreSystems.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
namespace CoreSystems
{
    public partial class Session
    {
        #region UI Config
        public static void CreateDecoyTerminalUi<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomDecoyActions<T>(session);
            TerminalHelpers.AddDecoyControls<T>(session);
        }

        public static void CreateCameraTerminalUi<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomCameraActions<T>(session);
            TerminalHelpers.AddCameraControls<T>(session);
        }

        public static void EarlyInitControls(Session session)
        {
            Type controlObject;
            while (session.ControlQueue.TryDequeue(out controlObject))
            {
                if (controlObject == typeof(IMyConveyorSorter))
                {
                    CreateTerminalUi<IMyConveyorSorter>(session);
                }
                else if (controlObject == typeof(IMyLargeTurretBase))
                {
                    CreateTerminalUi<IMyLargeTurretBase>(session);
                }
                else if (controlObject == typeof(IMySmallMissileLauncherReload))
                {
                    CreateTerminalUi<IMySmallMissileLauncherReload>(session);
                }
                else if (controlObject == typeof(IMySmallMissileLauncher))
                {
                    CreateTerminalUi<IMySmallMissileLauncher>(session);
                }
                else if (controlObject == typeof(IMySmallGatlingGun))
                {
                    CreateTerminalUi<IMySmallGatlingGun>(session);
                }
                else if (controlObject == typeof(IMyTurretControlBlock))
                {
                    CreateTerminalUi<IMyTurretControlBlock>(session);
                }
            }
            session.ControlQueue.Clear();
            session.EarlyInitOver = true;
        }

        public static bool ControlsAlreadyExist<T>(Session session)
        {
            if (typeof(T) == typeof(IMyConveyorSorter) && session.ControlTypeActivated.Contains(typeof(IMyConveyorSorter)))
                return true;

            if (typeof(T) == typeof(IMyLargeTurretBase) && session.ControlTypeActivated.Contains(typeof(IMyLargeTurretBase)))
                return true;

            if (typeof(T) == typeof(IMySmallMissileLauncherReload) && session.ControlTypeActivated.Contains(typeof(IMySmallMissileLauncherReload)))
                return true;

            if (typeof(T) == typeof(IMySmallMissileLauncher) && session.ControlTypeActivated.Contains(typeof(IMySmallMissileLauncher)))
                return true;

            if (typeof(T) == typeof(IMySmallGatlingGun) && session.ControlTypeActivated.Contains(typeof(IMySmallGatlingGun)))
                return true;

            if (typeof(T) == typeof(IMyTurretControlBlock) && session.ControlTypeActivated.Contains(typeof(IMyTurretControlBlock)))
                return true;

            session.ControlTypeActivated.Add(typeof(T));
            return false;
        }

        public static void CreateTerminalUi<T>(Session session) where T : IMyTerminalBlock
        {
            try
            {
                if (ControlsAlreadyExist<T>(session))
                {
                    return;
                }
                AlterActions<T>(session);
                AlterControls<T>(session);

                if (typeof(T) == typeof(IMyTurretControlBlock))
                {
                    CreateTurretControllerActions<T>(session);
                    TerminalHelpers.AddTurretControlBlockControls<T>(session);
                    return;
                }

                TerminalHelpers.CreateGenericControls<T>(session);
                TerminalHelpers.AddUiControls<T>(session);

                if (typeof(T) == typeof(IMyLargeTurretBase) || typeof(T) == typeof(IMySmallMissileLauncher) || typeof(T) == typeof(IMySmallGatlingGun) || typeof(T) == typeof(IMySmallMissileLauncherReload))
                {
                    session.BaseControlsActions = true;
                    CreateCustomActionSet<T>(session);
                }
                else if (typeof(T) == typeof(IMyConveyorSorter))
                {
                    CreateDefaultActions<T>(session);
                }

                TerminalHelpers.AddTurretOrTrackingControls<T>(session);
            }
            catch (Exception ex) { Log.Line($"Exception in CreateControlUi: {ex}"); }
        }

        internal static void CreateDefaultActions<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomActionSet<T>(session);
        }

        internal static void CreateCustomDecoyActions<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomActions<T>.CreateDecoy(session);
        }

        internal static void CreateCustomCameraActions<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomActions<T>.CreateCamera(session);
        }
        internal static void CreateCustomActionSet<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomActions<T>.CreateShootToggle(session);
            CreateCustomActions<T>.CreateShootOn(session);
            CreateCustomActions<T>.CreateShootOff(session);
            CreateCustomActions<T>.CreateKeyShoot(session);
            CreateCustomActions<T>.CreateShootMode(session);
            CreateCustomActions<T>.CreateMouseToggle(session);
            CreateCustomActions<T>.CreateSubSystems(session);
            CreateCustomActions<T>.CreateControlModes(session);
            CreateCustomActions<T>.CreateMovementState(session);
            CreateCustomActions<T>.CreateCycleAmmo(session);
            CreateCustomActions<T>.CreateFocusTargets(session);
            CreateCustomActions<T>.CreateFocusSubSystem(session);
            CreateCustomActions<T>.CreateGrids(session);
            CreateCustomActions<T>.CreateNeutrals(session);
            CreateCustomActions<T>.CreateFriendly(session);
            CreateCustomActions<T>.CreateUnowned(session);
            CreateCustomActions<T>.CreateProjectiles(session);
            CreateCustomActions<T>.CreateBiologicals(session);
            CreateCustomActions<T>.CreateMeteors(session);
            CreateCustomActions<T>.CreateWeaponCameraChannels(session);
            CreateCustomActions<T>.CreateLeadGroups(session);
            CreateCustomActions<T>.CreateRepelMode(session);
            CreateCustomActions<T>.CreateMaxSize(session);
            CreateCustomActions<T>.CreateMinSize(session);
            CreateCustomActions<T>.CreateSelectFriend(session);
            CreateCustomActions<T>.CreateSelectEnemy(session);
            CreateCustomActions<T>.CreateSelectPosition(session);
        }

        internal static void CreateTurretControllerActions<T>(Session session) where T : IMyTerminalBlock
        {
            CreateCustomActions<T>.CreateAiEnabledControl(session);
            CreateCustomActions<T>.CreateNeutralsControl(session);
            CreateCustomActions<T>.CreateFriendlyControl(session);
            CreateCustomActions<T>.CreateUnownedControl(session);

            CreateCustomActions<T>.CreateMovementStateControl(session);
            //CreateCustomActions<T>.CreateControlModesControl(session);
            CreateCustomActions<T>.CreateSubSystemsControl(session);
            CreateCustomActions<T>.CreateProjectilesControl(session);
            CreateCustomActions<T>.CreateBiologicalsControl(session);
            CreateCustomActions<T>.CreateMeteorsControl(session);
            CreateCustomActions<T>.CreateGridsControl(session);
            CreateCustomActions<T>.CreateFocusTargetsControl(session);
            CreateCustomActions<T>.CreateFocusSubSystemControl(session);
            CreateCustomActions<T>.CreateMaxSizeControl(session);
            CreateCustomActions<T>.CreateMinSizeControl(session);
            CreateCustomActions<T>.CreateRepelModeControl(session);
        }

        internal static void CreateCustomActionSetArmorEnhancer<T>(Session session) where T: IMyTerminalBlock
        {
            CreateCustomActions<T>.CreateArmorShowArea(session);
        }

        private void CustomControlHandler(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            LastTerminal = block;

            var cube = (MyCubeBlock)block;
            Ai ai;
            if (EntityAIs.TryGetValue(cube.CubeGrid, out ai))
            {
                ai.LastTerminal = block;
                CoreComponent comp;
                if (ai.CompBase.TryGetValue(cube, out comp) && comp.Platform.State == CorePlatform.PlatformState.Ready)
                {
                    TerminalMon.HandleInputUpdate(comp);
                    for (int i = controls.Count - 1; i >= 0; i--)
                    {
                        var control = controls[i];
                        if (control.Id.Equals("Range"))
                        {
                            controls.RemoveAt(i);
                        }
                        else if (control.Id.Equals("UseConveyor"))
                        {
                            controls.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private const string ShootModeStr = "Shoot";

        private const string ShootOnceModeStr = "ShootOnce";

        internal static void AlterActions<T>(Session session)
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);
            for (int i = 0; i < actions.Count; i++) {

                var a = actions[i];

                if (a.Id.Equals(ShootOnceModeStr) || !a.Id.Contains(ShootModeStr) && !a.Id.Contains("OnOff") && !a.Id.Contains("WC_") && !a.Id.Contains("Control"))
                {
                    a.Enabled = TerminalHelpers.NotWcBlock;
                    session.AlteredActions.Add(a);
                }
                else if (a.Id.Equals("Control")) {

                    a.Enabled = TerminalHelpers.NotWcOrIsTurret;
                    session.AlteredActions.Add(a);
                }
                /*
                else if (a.Id.Equals("ShootOnce")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            if (comp == null)
                                oldAction(blk);
                            return;
                        }

                        a.Name.Clear();
                        a.Name.Append(ShootModeStr);
                        comp.ShootManager.RequestShootSync(comp.Session.PlayerId);
                    };
                    a.Name.Clear();
                    a.Name.Append(ShootModeStr);
                    session.AlteredActions.Add(a);
                }
                */
                else if (a.Id.Equals("Shoot")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            if (comp == null)
                                oldAction(blk);
                            return;
                        }

                        var on = comp.Data.Repo.Values.State.Trigger == On;
                        comp.ShootManager.RequestShootSync(comp.Session.PlayerId, on ? Weapon.ShootManager.RequestType.On : Weapon.ShootManager.RequestType.Off, Weapon.ShootManager.Signals.On);
                    };

                    var oldWriter = a.Writer;
                    a.Writer = (blk, sb) => {

                        var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            oldWriter(blk, sb);
                            return;
                        }
                        if (comp.Data.Repo.Values.State.Trigger == On)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                    session.AlteredActions.Add(a);
                }
                else if (a.Id.Equals("Shoot_On")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            if (comp == null) oldAction(blk);
                            return;
                        }
                        if (comp.Data.Repo.Values.State.Trigger != On)
                            comp.ShootManager.RequestShootSync(comp.Session.PlayerId, Weapon.ShootManager.RequestType.On, Weapon.ShootManager.Signals.On);
                    };

                    var oldWriter = a.Writer;
                    a.Writer = (blk, sb) =>
                    {
                        var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready)
                        {
                            oldWriter(blk, sb);
                            return;
                        }
                        if (comp.Data.Repo.Values.State.Trigger == On)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                    session.AlteredActions.Add(a);
                }
                else if (a.Id.Equals("Shoot_Off")) {

                    var oldAction = a.Action;
                    a.Action = blk => {

                        var comp = blk?.Components?.Get<CoreComponent>() as Weapon.WeaponComponent;
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            if (comp == null)  oldAction(blk);
                            return;
                        }
                        if (comp.Data.Repo.Values.State.Trigger != Off)
                            comp.ShootManager.RequestShootSync(comp.Session.PlayerId, Weapon.ShootManager.RequestType.Off);
                    };

                    var oldWriter = a.Writer;
                    a.Writer = (blk, sb) => {

                        var comp = blk.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
                        if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) {
                            oldWriter(blk, sb);
                            return;
                        }
                        if (comp.Data.Repo.Values.State.Trigger == On)
                            sb.Append("On");
                        else
                            sb.Append("Off");
                    };
                    session.AlteredActions.Add(a);
                }
            }
        }

        private static HashSet<string> _visibleControls = new HashSet<string> 
        {
                "OnOff",
                //"Shoot",
                "ShowInTerminal",
                "ShowInInventory",
                "ShowInToolbarConfig",
                "Name",
                "ShowOnHUD",
                "CustomData",
                "Control",
                "Renamer_Separator",
                "Renamer_Label",
                "Renamer_Textbox",
                "Renamer_RenameButton",
                "Renamer_PrefixButton",
                "Renamer_SuffixButton",
                "Renamer_ResetButton",
                "PanelList",
                "Content",
                "Script",
                "ScriptForegroundColor",
                "ScriptBackgroundColor",
                "ShowTextPanel",
                "Font",
                "FontSize",
                "FontColor",
                "alignment",
                "TextPaddingSlider",
                "BackgroundColor",
                "ImageList",
                "SelectTextures",
                "ChangeIntervalSlider",
                "SelectedImageList",
                "RemoveSelectedTextures",
                "PreserveAspectRatio",

                "Open Toolbar",
                "RotorAzimuth",
                "RotorElevation",
                "CameraList",
                "MultiplierAz",
                "MultiplierEl",
                "AngleDeviation",
                //"ToolList",
                //"AddSelectedTool",
                //"SelectedToolsList",
                //"RemoveSelectedTool",

            };

        internal static void AlterControls<T>(Session session) where T : IMyTerminalBlock //  https://github.com/THDigi/ElectronicsPanel/blob/master/Data/Scripts/ElectronicsPanel/ElectronicsPanelMod.cs#L244
        {
            var validType = typeof(T) == typeof(IMyUserControllableGun);
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);

            for (int i = validType ? 12 : 0; i < controls.Count; i++) {

                var c = controls[i];
                if (session.AlteredControls.Contains(c)) continue;
                if (!_visibleControls.Contains(c.Id)) {
                    c.Visible = TerminalHelpers.NotWcBlock;
                    session.AlteredControls.Add(c);
                    continue;
                }

                switch (c.Id) {
                    case "Control":
                        c.Visible = TerminalHelpers.NotWcOrIsTurret;
                        session.AlteredControls.Add(c);
                        break;

                    case "OnOff":
                        ((IMyTerminalControlOnOffSwitch) c).Setter += OnOffSetter;
                        session.AlteredControls.Add(c);
                        break;
                }
            }
        }

        private static void OnOffSetter(IMyTerminalBlock block, bool on)
        {
            var comp = block?.Components?.Get<CoreComponent>();
            if (comp == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return;
            OnOffAnimations(comp, on);
        }

        private static void OnOffAnimations(CoreComponent comp, bool on)
        {
            if (comp.Platform.State != CorePlatform.PlatformState.Ready || on == comp.LastOnOffState) return;

            comp.LastOnOffState = on;
            var collection = comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? comp.Platform.Weapons : comp.Platform.Phantoms;
            for (int i = 0; i < collection.Count; i++) {

                var w = collection[i];
                if (w == null) continue;

                if (!on) {

                    if (w.TurretController) {
                        var azSteps = w.Azimuth / w.System.AzStep;
                        var elSteps = w.Elevation / w.System.ElStep;

                        if (azSteps < 0) azSteps *= -1;
                        if (azSteps < 0) azSteps *= -1;

                        w.OffDelay = (uint)(azSteps + elSteps > 0 ? azSteps > elSteps ? azSteps : elSteps : 0);

                        if (!w.BaseComp.Session.IsClient) w.Target.Reset(comp.Session.Tick, Target.States.AnimationOff);
                        w.ScheduleWeaponHome(true);
                    }

                    if (w.IsShooting) {
                        w.StopShooting();
                        Log.Line("StopShooting OnOffAnimations");
                    }
                }
                else {

                    uint delay;
                    if (w.System.PartAnimationLengths.TryGetValue(EventTriggers.TurnOn, out delay))
                        w.PartReadyTick = comp.Session.Tick + delay;

                    if (w.LastEvent == EventTriggers.TurnOff && w.AnimationDelayTick > comp.Session.Tick)
                        w.PartReadyTick += w.AnimationDelayTick - comp.Session.Tick;
                }

                if (w.AnimationDelayTick < comp.Session.Tick || w.LastEvent == EventTriggers.TurnOn || w.LastEvent == EventTriggers.TurnOff) {
                    w.EventTriggerStateChanged(EventTriggers.TurnOn, on);
                    w.EventTriggerStateChanged(EventTriggers.TurnOff, !on);
                }
                else {

                    comp.Session.FutureEvents.Schedule(o => {
                        w.EventTriggerStateChanged(EventTriggers.TurnOn, on);
                        w.EventTriggerStateChanged(EventTriggers.TurnOff, !on);
                    },
                        null,
                        w.AnimationDelayTick - comp.Session.Tick
                    );
                }
            }
        }

        public static void PurgeTerminalSystem(Session session)
        {
            foreach (var a in session.CustomActions)
            {
                MyAPIGateway.TerminalControls.RemoveAction<IMyTerminalBlock>(a);

                a.Writer = EmptyWritter;
                a.Action = EmptyAction;
                a.Enabled = EmptyBool;
                a.Action = null;
                a.Enabled = null;
            }
            session.CustomActions.Clear();

            foreach (var a in session.AlteredActions)
            {
                MyAPIGateway.TerminalControls.RemoveAction<IMyTerminalBlock>(a);

                a.Writer = EmptyWritter;
                a.Action = EmptyAction;
                a.Enabled = EmptyBool;
                a.Action = null;
                a.Enabled = null;
            }
            session.AlteredActions.Clear();

            foreach (var c in session.CustomControls)
            {
                MyAPIGateway.TerminalControls.RemoveControl<IMyTerminalBlock>(c);
                c.Enabled = EmptyBool;
                c.Visible = EmptyBool;
                c.Enabled = null;
                c.Visible = null;
            }
            session.CustomControls.Clear();

            foreach (var c in session.AlteredControls)
            {
                MyAPIGateway.TerminalControls.RemoveControl<IMyTerminalBlock>(c);
                c.Enabled = EmptyBool;
                c.Visible = EmptyBool;
                c.Enabled = null;
                c.Visible = null;
            }
            session.AlteredControls.Clear();
            _visibleControls.Clear();
            _visibleControls = null;
        }

        private static void EmptyAction(IMyTerminalBlock obj)
        {
        }

        private static bool EmptyBool(IMyTerminalBlock obj)
        {
            return false;
        }

        public static void EmptyWritter(IMyTerminalBlock myTerminalBlock, StringBuilder stringBuilder)
        {
        }
        #endregion
    }
}