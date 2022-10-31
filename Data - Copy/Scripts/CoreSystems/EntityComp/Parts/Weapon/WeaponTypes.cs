using System;
using System.Collections.Generic;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GameDefinition;
using WeaponCore.Data.Scripts.CoreSystems.Ui;

namespace CoreSystems.Platform
{
    public partial class Weapon 
    {
        internal class ParallelRayCallBack
        {
            internal Weapon Weapon;

            internal ParallelRayCallBack(Weapon weapon)
            {
                Weapon = weapon;
            }

            public void NormalShootRayCallBack(IHitInfo hitInfo)
            {
                Weapon.Casting = false;
                Weapon.PauseShoot = false;
                var masterWeapon = Weapon.System.TrackTargets ? Weapon : Weapon.Comp.PrimaryWeapon;
                var ignoreTargets = Weapon.Target.TargetState == Target.TargetStates.IsProjectile || Weapon.Target.TargetEntity is IMyCharacter;
                var scope = Weapon.GetScope;
                var trackingCheckPosition = scope.CachedPos;
                double rayDist = 0;


                if (Weapon.System.Session.DebugLos)
                {
                    var hitPos = hitInfo.Position;
                    if (rayDist <= 0) Vector3D.Distance(ref trackingCheckPosition, ref hitPos, out rayDist);

                    Weapon.System.Session.AddLosCheck(new Session.LosDebug { Part = Weapon, HitTick = Weapon.System.Session.Tick, Line = new LineD(trackingCheckPosition, hitPos) });
                }

                
                if (Weapon.Comp.Ai.ShieldNear)
                {
                    var targetPos = Weapon.Target.Projectile?.Position ?? Weapon.Target.TargetEntity.PositionComp.WorldMatrixRef.Translation;
                    var targetDir = targetPos - trackingCheckPosition;
                    if (Weapon.HitFriendlyShield(trackingCheckPosition, targetPos, targetDir))
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        return;
                    }
                }

                var hitTopEnt = (MyEntity)hitInfo?.HitEntity?.GetTopMostParent();
                if (hitTopEnt == null)
                {
                    if (ignoreTargets)
                        return;
                    masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckMiss);
                    if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckMiss);
                    return;
                }

                var targetTopEnt = Weapon.Target.TargetEntity?.GetTopMostParent();
                if (targetTopEnt == null)
                    return;

                var unexpectedHit = ignoreTargets || targetTopEnt != hitTopEnt;
                var topAsGrid = hitTopEnt as MyCubeGrid;

                if (unexpectedHit)
                {
                    if (hitTopEnt is MyVoxelBase)
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckVoxel);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckVoxel);
                        return;
                    }

                    if (topAsGrid == null)
                        return;
                    if (Weapon.Target.TargetEntity != null && (Weapon.Comp.Ai.AiType == Ai.AiTypes.Grid && topAsGrid.IsSameConstructAs(Weapon.Comp.Ai.GridEntity)))
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckSelfHit);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckSelfHit);
                        Weapon.PauseShoot = true;
                        return;
                    }
                    if (!topAsGrid.DestructibleBlocks || topAsGrid.Immune || topAsGrid.GridGeneralDamageModifier <= 0 || !Session.GridEnemy(Weapon.Comp.Ai.AiOwner, topAsGrid))
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckFriendly);
                        return;
                    }
                    return;
                }
                if (Weapon.System.ClosestFirst && topAsGrid != null && topAsGrid == targetTopEnt)
                {
                    var halfExtMin = topAsGrid.PositionComp.LocalAABB.HalfExtents.Min();
                    var minSize = topAsGrid.GridSizeR * 8;
                    var maxChange = halfExtMin > minSize ? halfExtMin : minSize;
                    var targetPos = Weapon.Target.TargetEntity.PositionComp.WorldAABB.Center;
                    var weaponPos = trackingCheckPosition;

                    if (rayDist <= 0) Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                    var newHitShortDist = rayDist * (1 - hitInfo.Fraction);
                    var distanceToTarget = rayDist * hitInfo.Fraction;

                    var shortDistExceed = newHitShortDist - Weapon.Target.HitShortDist > maxChange;
                    var escapeDistExceed = distanceToTarget - Weapon.Target.OrigDistance > Weapon.Target.OrigDistance;
                    if (shortDistExceed || escapeDistExceed)
                    {
                        masterWeapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckDistOffset);
                        if (masterWeapon != Weapon) Weapon.Target.Reset(Weapon.Comp.Session.Tick, Target.States.RayCheckDistOffset);
                    }
                }
            }
        }

        internal class Muzzle
        {
            internal Muzzle(Weapon weapon, int id, Session session)
            {
                MuzzleId = id;
                UniqueId = session.UniqueMuzzleId.Id;
                Weapon = weapon;
            }

            internal Weapon Weapon;
            internal Vector3D Position;
            internal Vector3D Direction;
            internal Vector3D UpDirection;
            internal Vector3D DeviatedDir;
            internal uint LastUpdateTick;
            internal uint LastAv1Tick;
            internal uint LastAv2Tick;
            internal int MuzzleId;
            internal ulong UniqueId;
            internal bool Av1Looping;
            internal bool Av2Looping;

        }

        public class ShootManager
        {
            public readonly WeaponComponent Comp;
            internal bool WaitingShootResponse;
            internal bool FreezeClientShoot;
            internal Signals Signal;
            internal uint CompletedCycles;
            internal uint LastCycle = uint.MaxValue;
            internal uint LastShootTick;
            internal uint PrevShootEventTick;
            internal uint WaitingTick;
            internal uint FreezeTick;
            internal int ShootDelay;
            internal uint ClientToggleCount;
            internal int WeaponsFired;

            public enum RequestType
            {
                On,
                Off,
                Once,
            }

            public enum Signals
            {
                None,
                Manual,
                MouseControl,
                On,
                Once,
                KeyToggle,
            }

            public enum ShootModes
            {
                AiShoot,
                MouseControl,
                KeyToggle,
                KeyFire,
            }

            internal enum ShootCodes
            {
                ServerResponse,
                ClientRequest,
                ServerRequest,
                ServerRelay,
                ToggleServerOff,
                ToggleClientOff,
                ClientRequestReject,
            }

            internal enum EndReason
            {
                Overheat,
                Reload,
                Toggle,
                ServerRequested,
                ServerAhead,
                Failed,
                Finished,
                Rejected,
            }

            public ShootManager(WeaponComponent comp)
            {
                Comp = comp;
            }


            #region InputManager
            internal bool RequestShootSync(long playerId, RequestType request, Signals signal = Signals.None) // this shoot method mixes client initiation with server delayed server confirmation in order to maintain sync while avoiding authoritative delays in the common case. 
            {
                var values = Comp.Data.Repo.Values;
                var state = values.State;
                var isRequestor = !Comp.Session.IsClient || playerId == Comp.Session.PlayerId;
                
                if (isRequestor && Comp.Session.IsClient && request == RequestType.Once && (WaitingShootResponse || FreezeClientShoot || CompletedCycles > 0 || ClientToggleCount > state.ToggleCount || state.Trigger != CoreComponent.Trigger.Off)) 
                    return false;

                if (isRequestor && !ProcessInput(playerId, request, signal) || !MakeReadyToShoot()) {
                    ChangeState(request, playerId, false);
                    return false;
                }

                Signal = request != RequestType.Off ? signal : Signals.None;

                if (Comp.IsBlock && Comp.Session.HandlesInput)
                    Comp.Session.TerminalMon.HandleInputUpdate(Comp);

                var sendRequest = !Comp.Session.IsClient || playerId == Comp.Session.PlayerId; // this method is used both by initiators and by receives. 
                
                if (Comp.Session.MpActive && sendRequest)
                {
                    WaitingShootResponse = Comp.Session.IsClient; // this will be set false on the client once the server responds to this packet
                    
                    if (WaitingShootResponse)
                        ClientToggleCount = state.ToggleCount + 1;

                    WaitingTick = Comp.Session.Tick;

                    var code = Comp.Session.IsServer ? playerId == 0 ? ShootCodes.ServerRequest : ShootCodes.ServerRelay : ShootCodes.ClientRequest;
                    ulong packagedMessage;
                    EncodeShootState((uint)request, (uint)signal, CompletedCycles, (uint)code, out packagedMessage);
                    if (playerId > 0) // if this is the server responding to a request, rewrite the packet sent to the origin client with a special response code.
                        Comp.Session.SendShootRequest(Comp, packagedMessage, PacketType.ShootSync, RewriteShootSyncToServerResponse, playerId);
                    else
                        Comp.Session.SendShootRequest(Comp, packagedMessage, PacketType.ShootSync, null, playerId);
                }

                ChangeState(request, playerId, true);

                return true;
            }

            internal bool ProcessInput(long playerId, RequestType request, Signals signal)
            {
                if (ShootRequestPending(request))
                    return false;

                var state = Comp.Data.Repo.Values.State;
                var wasToggled = ClientToggleCount > state.ToggleCount || state.Trigger == CoreComponent.Trigger.On;
                if (wasToggled && request != RequestType.On && !FreezeClientShoot) // toggle off
                {
                    if (Comp.Session.MpActive)
                    {
                        FreezeClientShoot = Comp.Session.IsClient; //if the initiators is a client pause future cycles until the server returns which cycle state to terminate on.
                        FreezeTick = Comp.Session.Tick;

                        ulong packagedMessage;
                        EncodeShootState((uint)request, (uint)signal, CompletedCycles, (uint)ShootCodes.ToggleServerOff, out packagedMessage);
                        Comp.Session.SendShootRequest(Comp, packagedMessage, PacketType.ShootSync, RewriteShootSyncToServerResponse, playerId);
                    }

                    if (Comp.Session.IsServer) 
                        EndShootMode(EndReason.ServerRequested);

                    Signal = request != RequestType.Off ? signal : Signals.None;
                }

                var pendingRequest = Comp.IsDisabled || wasToggled || Comp.IsBlock && !Comp.Cube.IsWorking;

                return !pendingRequest;
            }

            private bool ShootRequestPending(RequestType requestType)
            {
                if (FreezeClientShoot || WaitingShootResponse)
                {
                    return true;
                }
                return false;
            }

            private void ChangeState(RequestType request, long playerId, bool activated)
            {

                var state = Comp.Data.Repo.Values.State;

                // Pretty sus that this is allowed by client, possible race condition... likely needed by client side prediction
                state.PlayerId = playerId;
                if (Comp.Session.IsServer)
                {
                    switch (request)
                    {
                        case RequestType.Off:
                            state.Trigger = CoreComponent.Trigger.Off;
                            break;
                        case RequestType.On:
                            state.Trigger = CoreComponent.Trigger.On;
                            break;
                        case RequestType.Once:
                            state.Trigger = CoreComponent.Trigger.Once;
                            break;
                    }

                    if (activated)
                        ++state.ToggleCount;

                    if (Comp.Session.MpActive)
                        Comp.Session.SendState(Comp);
                }

                if (activated)
                    LastCycle = ClientToggleCount > state.ToggleCount && request != RequestType.Once || state.Trigger == CoreComponent.Trigger.On || Comp.Session.IsClient && request == RequestType.On && playerId == 0 ? uint.MaxValue : 1;

            }
            #endregion

            #region Main
            internal void RestoreWeaponShot()
            {
                for (int i = 0; i < Comp.Collection.Count; i++)
                {
                    var w = Comp.Collection[i];
                    var predicted = w.ActiveAmmoDef.AmmoDef.Const.ClientPredictedAmmo;
                    if (w.ActiveAmmoDef.AmmoDef.Const.MustCharge && !predicted)
                    {
                        Log.Line($"RestoreWeaponShot, recharge", Session.InputLog);
                        w.ProtoWeaponAmmo.CurrentCharge = w.MaxCharge;
                        w.EstimatedCharge = w.MaxCharge;
                    }
                    else if (!predicted)
                    {
                        w.ProtoWeaponAmmo.CurrentAmmo += (int)CompletedCycles;
                        Log.Line($"RestoreWeaponShot, return ammo:{CompletedCycles}", Session.InputLog);
                    }
                }
            }

            internal void UpdateShootSync(Weapon w)
            {
                if (--w.ShootCount == 0 && ++WeaponsFired >= Comp.TotalWeapons)
                {
                    var overrides = w.Comp.Data.Comp.Data.Repo.Values.Set.Overrides;
                    var state = w.Comp.Data.Comp.Data.Repo.Values.State;

                    ++CompletedCycles;
                    
                    var toggled = w.Comp.ShootManager.ClientToggleCount > state.ToggleCount || state.Trigger == CoreComponent.Trigger.On;
                    var overCount = CompletedCycles >= LastCycle;

                    if (!toggled || overCount)
                        EndShootMode(EndReason.Finished);
                    else
                    {
                        MakeReadyToShoot(true);
                        ShootDelay = overrides.BurstDelay;
                    }
                }
            }

            internal bool MakeReadyToShoot(bool skipReady = false)
            {
                var weaponsReady = 0;
                var totalWeapons = Comp.Collection.Count;
                var overrides = Comp.Data.Repo.Values.Set.Overrides;
                var burstTarget = overrides.BurstCount;
                var client = Comp.Session.IsClient;
                for (int i = 0; i < totalWeapons; i++)
                {
                    var w = Comp.Collection[i];
                    if (!w.System.DesignatorWeapon)
                    {
                        var aConst = w.ActiveAmmoDef.AmmoDef.Const;
                        var reloading = aConst.Reloadable && w.ClientMakeUpShots == 0 && (w.Loading || w.ProtoWeaponAmmo.CurrentAmmo == 0 || w.Reload.WaitForClient);

                        var reloadMinusAmmoCheck = aConst.Reloadable && w.ClientMakeUpShots == 0 && (w.Loading || w.Reload.WaitForClient);
                        var skipReload = client && reloading && !skipReady && !FreezeClientShoot && !WaitingShootResponse && !reloadMinusAmmoCheck && Comp.Session.Tick - LastShootTick > 30;
                        var overHeat = w.PartState.Overheated && w.OverHeatCountDown == 0;
                        var canShoot = !overHeat && (!reloading || skipReload);

                        if (canShoot && skipReload)
                            Log.Line($"ReadyToShoot succeeded on client but with CurrentAmmo > 0 - shooting:{w.IsShooting} - charging:{w.Charging} - charge:{w.ProtoWeaponAmmo.CurrentCharge}({w.MaxCharge})", Session.InputLog);

                        var weaponReady = canShoot && !w.IsShooting;

                        if (!weaponReady && !skipReady)
                        {
                            if (Comp.Session.IsServer) 
                                Log.Line($"MakeReadyToShoot: canShoot:{canShoot} - alreadyShooting:{w.IsShooting} - reloading:{reloading} - skipReload:{skipReload} - CurrentAmmo:{w.ProtoWeaponAmmo.CurrentAmmo} - wait:{w.Reload.WaitForClient}", Session.InputLog);
                            break;
                        }

                        weaponsReady += 1;

                        w.ShootCount += MathHelper.Clamp(burstTarget, 1, w.ProtoWeaponAmmo.CurrentAmmo + w.ClientMakeUpShots);
                    }
                    else
                        weaponsReady += 1;
                }

                var ready = weaponsReady == totalWeapons;

                if (!ready && weaponsReady > 0)
                {
                    Log.Line($"not ready to MakeReadyToShoot", Session.InputLog);
                    ResetShootRequest();
                }

                return ready;
            }

            internal void ResetShootRequest()
            {
                for (int i = 0; i < Comp.Collection.Count; i++)
                    Comp.Collection[i].ShootCount = 0;

                WeaponsFired = 0;
            }

            internal void EndShootMode(EndReason reason, bool skipNetwork = false)
            {
                var wValues = Comp.Data.Repo.Values;

                for (int i = 0; i < Comp.TotalWeapons; i++)
                {
                    var w = Comp.Collection[i];
                    if (Comp.Session.MpActive) Log.Line($"[clear] Reason:{reason} - ammo:{w.ProtoWeaponAmmo.CurrentAmmo} - Trigger:{wValues.State.Trigger} - Signal:{Signal} - Cycles:{CompletedCycles}[{LastCycle}] - Count:{wValues.State.ToggleCount}[{ClientToggleCount}] - WeaponsFired:{WeaponsFired}", Session.InputLog);

                    w.ShootCount = 0;
                }

                ShootDelay = 0;
                CompletedCycles = 0;
                WeaponsFired = 0;
                LastCycle = uint.MaxValue;

                ClientToggleCount = 0;
                FreezeClientShoot = false;
                WaitingShootResponse = false;
                Signal = Signals.None;

                if (Comp.Session.IsServer)
                {
                    wValues.State.Trigger = CoreComponent.Trigger.Off;
                    if (Comp.Session.MpActive && !skipNetwork)
                    {
                        Comp.Session.SendState(Comp);
                    }
                }
            }
            #endregion


            #region Network

            internal void ServerRejectResponse(ulong clientId, RequestType requestType)
            {
                Log.Line($"[server rejecting] Signal:{Signal} - CompletedCycles:{CompletedCycles} requestType:{requestType} - Trigger:{Comp.Data.Repo.Values.State.Trigger}", Session.InputLog);
                ulong packagedMessage;
                EncodeShootState(0, (uint)Signals.None, CompletedCycles, (uint)ShootCodes.ClientRequestReject, out packagedMessage);
                Comp.Session.SendShootReject(Comp, packagedMessage, PacketType.ShootSync, clientId);

                EndShootMode(EndReason.Rejected);
            }


            internal void ReceivedServerReject()
            {
                Log.Line($"[client rejection] message reset - wait:{WaitingShootResponse} - frozen:{FreezeClientShoot}", Session.InputLog);
                if (CompletedCycles > 0)
                    RestoreWeaponShot();

                EndShootMode(EndReason.Rejected);
            }

            internal void FailSafe()
            {
                Log.Line($"ShootMode failsafe triggered: LastCycle:{LastCycle} - CompletedCycles:{CompletedCycles} - WeaponsFired:{WeaponsFired} - wait:{WaitingShootResponse} - freeze:{FreezeClientShoot}", Session.InputLog);
                EndShootMode(EndReason.Failed);
            }

            internal void ServerToggleOffByClient(uint interval)
            {

                var clientMakeupRequest = interval > CompletedCycles && LastCycle == uint.MaxValue;
                var endCycle = !clientMakeupRequest ? CompletedCycles : interval;

                ulong packagedMessage;
                EncodeShootState(0, (uint)Signals.None, endCycle, (uint)ShootCodes.ToggleClientOff, out packagedMessage);
                Comp.Session.SendShootRequest(Comp, packagedMessage, PacketType.ShootSync, null, 0);

                if (!clientMakeupRequest)
                {
                    EndShootMode(EndReason.Toggle);
                }
                else
                {
                    Log.Line($"server catching up to client -- from:{CompletedCycles} to:{interval}", Session.InputLog);
                    LastCycle = endCycle;
                }
            }


            internal void ClientToggledOffByServer(uint interval, bool server = false)
            {
                if (server)
                    Log.Line($"server requested toggle off? - wait:{WaitingShootResponse} - mode:{Comp.Data.Repo.Values.Set.Overrides.ShootMode} - freeze:{FreezeClientShoot} - CompletedCycles:{CompletedCycles}({interval}) - LastCycle:{LastCycle}", Session.InputLog);

                if (interval > CompletedCycles)
                {
                    Log.Line($"[ClientToggledOffByServer] server interval {interval} > client: {CompletedCycles} - frozen:{FreezeClientShoot} - wait:{WaitingShootResponse}", Session.InputLog);
                }
                else if (interval < CompletedCycles) // look into adding a condition where the requesting client can cause the server to shoot for n burst to match client without exceeding reload, would need to freeze client.
                {
                    Log.Line($"[ClientToggledOffByServer] server interval {interval} < client:{CompletedCycles} - frozen:{FreezeClientShoot} - wait:{WaitingShootResponse}", Session.InputLog);
                }

                if (interval <= CompletedCycles)
                {
                    EndShootMode(EndReason.Toggle);
                }
                else if (interval > CompletedCycles)
                {
                    Log.Line($"[ClientToggleResponse] client is behind server: Current: {CompletedCycles} freeze:{FreezeClientShoot} - target:{interval} - LastCycle:{LastCycle}", Session.InputLog);

                    //LastCycle = interval;
                    EndShootMode(EndReason.ServerAhead);

                }
                FreezeClientShoot = false;
            }

            private static object RewriteShootSyncToServerResponse(object o1, object o2)
            {
                var ulongPacket = (ULongUpdatePacket)o1;

                RequestType type;
                Signals signal;
                ShootCodes code;
                uint internval;

                DecodeShootState(ulongPacket.Data, out type, out signal, out internval, out code);

                code = ShootCodes.ServerResponse;
                ulong packagedMessage;
                EncodeShootState((uint)type, (uint)signal, internval, (uint)code, out packagedMessage);

                ulongPacket.Data = packagedMessage;

                return ulongPacket;
            }


            internal static void DecodeShootState(ulong id, out RequestType type, out Signals shootState, out uint interval, out ShootCodes code)
            {
                type = (RequestType)(id >> 48);

                shootState = (Signals)((id << 16) >> 48);
                interval = (uint)((id << 32) >> 48);
                code = (ShootCodes)((id << 48) >> 48);
            }

            internal static void EncodeShootState(uint type, uint shootState, uint interval, uint code, out ulong id)
            {
                id = ((ulong)(type << 16 | shootState) << 32) | (interval << 16 | code);
            }

            #endregion
        }
    }
}
