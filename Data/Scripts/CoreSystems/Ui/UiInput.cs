using System;
using CoreSystems;
using CoreSystems.Support;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;

namespace WeaponCore.Data.Scripts.CoreSystems.Ui
{
    internal class UiInput
    {
        internal int PreviousWheel;
        internal int CurrentWheel;
        internal int ShiftTime;
        internal bool MouseButtonPressed;
        internal bool InputChanged;
        internal bool MouseButtonLeftWasPressed;
        internal bool MouseButtonLeftNewPressed;
        internal bool MouseButtonLeftReleased;
        internal bool MouseButtonMenuWasPressed;

        internal bool MouseButtonRightNewPressed;
        internal bool MouseButtonRightReleased;
        internal bool MouseButtonRightWasPressed;
        internal bool WasInMenu;
        internal bool WheelForward;
        internal bool WheelBackward;
        internal bool ShiftReleased;
        internal bool ShiftPressed;
        internal bool LongShift;
        internal bool AltPressed;
        internal bool ControlKeyPressed;
        internal bool ActionKeyPressed;
        internal bool InfoKeyPressed;
        internal bool ControlKeyReleased;
        internal bool ActionKeyReleased;
        internal bool InfoKeyReleased;
        internal bool BlackListActive1;
        internal bool CtrlPressed;
        internal bool AnyKeyPressed;
        internal bool KeyPrevPressed;
        internal bool UiKeyPressed;
        internal bool UiKeyWasPressed;
        internal bool PlayerCamera;
        internal bool FirstPersonView;
        internal bool CameraBlockView;
        internal long CameraChannelId;
        internal bool Debug = true;
        internal bool MouseShootWasOn;
        internal bool MouseShootOn;
        internal LineD AimRay;
        private readonly Session _session;
        private uint _lastInputUpdate;
        internal readonly InputStateData ClientInputState;
        internal MyKeys ControlKey;
        internal MyKeys ActionKey;
        internal MyKeys InfoKey;

        internal MyMouseButtonsEnum MouseButtonMenu;

        internal UiInput(Session session)
        {
            _session = session;
            ClientInputState = new InputStateData();
        }

        internal void UpdateInputState()
        {
            var s = _session;
            WheelForward = false;
            WheelBackward = false;
            AimRay = new LineD();

            if (!s.InGridAiBlock) s.UpdateLocalAiAndCockpit();

            if (s.InGridAiBlock && !s.InMenu)
            {
                MouseButtonPressed = MyAPIGateway.Input.IsAnyMousePressed();
                MouseButtonLeftNewPressed = MyAPIGateway.Input.IsNewLeftMousePressed();
                MouseButtonLeftReleased = MyAPIGateway.Input.IsNewLeftMouseReleased();
                MouseButtonLeftWasPressed = ClientInputState.MouseButtonLeft;
                MouseButtonMenuWasPressed = ClientInputState.MouseButtonMenu;

                MouseButtonRightNewPressed = MyAPIGateway.Input.IsNewRightMousePressed();
                MouseButtonRightReleased = MyAPIGateway.Input.IsNewRightMouseReleased();
                MouseButtonRightWasPressed = ClientInputState.MouseButtonRight;

                WasInMenu = ClientInputState.InMenu;
                ClientInputState.InMenu = _session.InMenu;

                if (MouseButtonPressed)
                {
                    ClientInputState.MouseButtonLeft = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Left);
                    ClientInputState.MouseButtonMenu = MyAPIGateway.Input.IsMousePressed(MouseButtonMenu);
                    ClientInputState.MouseButtonRight = MyAPIGateway.Input.IsMousePressed(MyMouseButtonsEnum.Right);
                }
                else
                {
                    ClientInputState.MouseButtonLeft = false;
                    ClientInputState.MouseButtonMenu = false;
                    ClientInputState.MouseButtonRight = false;
                }

                _session.PlayerMouseStates[_session.PlayerId] = ClientInputState;

                if (_session.MpActive)
                {
                    var shootButtonActive = ClientInputState.MouseButtonLeft || ClientInputState.MouseButtonRight;

                    MouseShootWasOn = MouseShootOn;
                    if (( s.Tick - _lastInputUpdate >= 29) && shootButtonActive && !MouseShootOn)
                    {
                        _lastInputUpdate = s.Tick;
                        MouseShootOn = true;
                    }
                    else if (MouseShootOn && !shootButtonActive)
                        MouseShootOn = false;

                    InputChanged = MouseShootOn != MouseShootWasOn || WasInMenu != ClientInputState.InMenu;
                }

                ShiftReleased = MyAPIGateway.Input.IsNewKeyReleased(MyKeys.LeftShift);
                ShiftPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.LeftShift);
                ControlKeyReleased = MyAPIGateway.Input.IsNewKeyReleased(ControlKey);

                if (ShiftPressed)
                {
                    ShiftTime++;
                    LongShift = ShiftTime > 59;
                }
                else
                {
                    if (LongShift) ShiftReleased = false;
                    ShiftTime = 0;
                    LongShift = false;
                }

                AltPressed = MyAPIGateway.Input.IsAnyAltKeyPressed();
                CtrlPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Control);
                KeyPrevPressed = AnyKeyPressed;
                AnyKeyPressed = MyAPIGateway.Input.IsAnyKeyPress();
                UiKeyWasPressed = UiKeyPressed;
                UiKeyPressed = CtrlPressed || AltPressed || ShiftPressed;
                PlayerCamera = MyAPIGateway.Session.IsCameraControlledObject;
                FirstPersonView = PlayerCamera && MyAPIGateway.Session.CameraController.IsInFirstPersonView;

                CameraBlockView = !PlayerCamera && !FirstPersonView && s.ActiveCameraBlock != null && s.ActiveCameraBlock.IsActive && s.ActiveCameraBlock.IsWorking;
                if (CameraBlockView && s.ActiveCameraBlock != null)
                    CameraChannelId = s.CameraChannelMappings[s.ActiveCameraBlock];
                else CameraChannelId = 0;

                if ((!UiKeyPressed && !UiKeyWasPressed) || !AltPressed && CtrlPressed && !FirstPersonView)
                {
                    PreviousWheel = MyAPIGateway.Input.PreviousMouseScrollWheelValue();
                    CurrentWheel = MyAPIGateway.Input.MouseScrollWheelValue();
                }


            }
            else if (!s.InMenu)
            {
                CtrlPressed = MyAPIGateway.Input.IsKeyPress(MyKeys.Control);
                ControlKeyPressed = MyAPIGateway.Input.IsKeyPress(ControlKey);
                CameraChannelId = 0;

                if (CtrlPressed && ControlKeyPressed && GetAimRay(s, out AimRay) && Debug)
                {
                    DsDebugDraw.DrawLine(AimRay, Color.Red, 0.1f);
                }
            }

            if (!s.InMenu)
            {
                ActionKeyReleased = MyAPIGateway.Input.IsNewKeyReleased(ActionKey);
                ActionKeyPressed = MyAPIGateway.Input.IsKeyPress(ActionKey);
                InfoKeyReleased = MyAPIGateway.Input.IsNewKeyReleased(InfoKey);
                if (ActionKeyPressed || InfoKeyReleased)
                {
                    if (!BlackListActive1)
                        BlackList1(true);

                    if (ActionKeyPressed && _session.CanChangeHud)
                    {
                        var evenTicks = _session.Tick % 2 == 0;
                        if (evenTicks)
                        {

                            if (MyAPIGateway.Input.IsKeyPress(MyKeys.Up))
                            {
                                _session.Settings.ClientConfig.HudPos.Y += 0.01f;
                                _session.Settings.VersionControl.UpdateClientCfgFile();
                            }
                            else if (MyAPIGateway.Input.IsKeyPress(MyKeys.Down))
                            {
                                _session.Settings.ClientConfig.HudPos.Y -= 0.01f;
                                _session.Settings.VersionControl.UpdateClientCfgFile();
                            }
                            else if (MyAPIGateway.Input.IsKeyPress(MyKeys.Left))
                            {
                                _session.Settings.ClientConfig.HudPos.X -= 0.01f;
                                _session.Settings.VersionControl.UpdateClientCfgFile();
                            }
                            else if (MyAPIGateway.Input.IsKeyPress(MyKeys.Right))
                            {
                                _session.Settings.ClientConfig.HudPos.X += 0.01f;
                                _session.Settings.VersionControl.UpdateClientCfgFile();
                            }

                        }

                        if (_session.Tick10)
                        {
                            if (MyAPIGateway.Input.IsKeyPress(MyKeys.Add))
                            {
                                _session.HudUi.NeedsUpdate = true;
                                _session.Settings.ClientConfig.HudScale = MathHelper.Clamp(_session.Settings.ClientConfig.HudScale + 0.01f, 0.1f, 10f);
                                _session.Settings.VersionControl.UpdateClientCfgFile();
                            }
                            else if (MyAPIGateway.Input.IsKeyPress(MyKeys.Subtract))
                            {
                                _session.HudUi.NeedsUpdate = true;
                                _session.Settings.ClientConfig.HudScale = MathHelper.Clamp(_session.Settings.ClientConfig.HudScale - 0.01f, 0.1f, 10f);
                                _session.Settings.VersionControl.UpdateClientCfgFile();
                            }
                        }
                    }

                    if (InfoKeyReleased)
                    {
                        _session.Settings.ClientConfig.MinimalHud = !_session.Settings.ClientConfig.MinimalHud;
                        _session.Settings.VersionControl.UpdateClientCfgFile();
                    }


                }
            }
            else
            {
                ActionKeyPressed = false;
                ActionKeyReleased = false;
                InfoKeyPressed = false;
                InfoKeyReleased = false;
            }

            if (_session.MpActive && !s.InGridAiBlock)
            {
                if (ClientInputState.InMenu || ClientInputState.MouseButtonRight || ClientInputState.MouseButtonMenu || ClientInputState.MouseButtonRight)
                {
                    ClientInputState.InMenu = false;
                    ClientInputState.MouseButtonLeft = false;
                    ClientInputState.MouseButtonMenu = false;
                    ClientInputState.MouseButtonRight = false;
                    InputChanged = true;
                }
            }

            if (CurrentWheel != PreviousWheel && CurrentWheel > PreviousWheel)
                WheelForward = true;
            else if (s.UiInput.CurrentWheel != s.UiInput.PreviousWheel)
                WheelBackward = true;

            if (!ActionKeyPressed && BlackListActive1)
                BlackList1(false);
        }

        internal bool GetAimRay(Session s, out LineD ray)
        {
            var character = MyAPIGateway.Session.Player.Character;
            if (character != null)
            {
                ray = new LineD(s.PlayerPos, s.PlayerPos + (character.WorldMatrix.Forward * 1000000));
                return true;
            }
            ray = new LineD();
            return false;
        }

        private void BlackList1(bool activate)
        {
            try
            {
                var upKey = MyAPIGateway.Input.GetControl(MyKeys.Up);
                var downKey = MyAPIGateway.Input.GetControl(MyKeys.Down);
                var leftKey = MyAPIGateway.Input.GetControl(MyKeys.Left);
                var rightkey = MyAPIGateway.Input.GetControl(MyKeys.Right);
                var addKey = MyAPIGateway.Input.GetControl(MyKeys.Add);
                var subKey = MyAPIGateway.Input.GetControl(MyKeys.Subtract);
                var actionKey = MyAPIGateway.Input.GetControl(MyKeys.NumPad0);
                var controlKey = MyAPIGateway.Input.GetControl(MyKeys.R);
                var detailKey = MyAPIGateway.Input.GetControl(MyKeys.Decimal);

                if (upKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(upKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                if (downKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(downKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                if (leftKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(leftKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                if (rightkey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(rightkey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                if (addKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(addKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                if (subKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(subKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }

                if (actionKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(actionKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }

                if (controlKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }

                if (detailKey != null)
                {
                    MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(detailKey.GetGameControlEnum().String, _session.PlayerId, !activate);
                }
                BlackListActive1 = activate;
            }
            catch (Exception ex) { Log.Line($"Exception in BlackList1: {ex}"); }
        }
    }
}
