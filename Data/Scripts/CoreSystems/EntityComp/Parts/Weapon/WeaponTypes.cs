using System.Collections.Generic;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

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
                var masterWeapon = Weapon.System.TrackTargets ? Weapon : Weapon.Comp.TrackingWeapon;
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
            internal bool ShootToggled;
            internal bool EarlyOff;
            internal bool WaitingShootResponse;
            internal bool FreezeClientShoot;
            internal uint CompletedCycles;
            internal uint LastCycle;
            internal uint LastShootTick;
            internal uint LockingTick;
            internal uint RequestShootBurstId;
            internal int WeaponsFired;

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

            public ShootManager(WeaponComponent comp)
            {
                Comp = comp;
            }

            #region Main

            internal void RestoreWeaponShot()
            {
                for (int i = 0; i < Comp.Collection.Count; i++)
                {
                    var w = Comp.Collection[i];
                    if (w.ActiveAmmoDef.AmmoDef.Const.MustCharge)
                    {
                        Log.Line($"RestoreWeaponShot, recharge", Session.InputLog);
                        w.ProtoWeaponAmmo.CurrentCharge = w.MaxCharge;
                        w.EstimatedCharge = w.MaxCharge;
                    }
                    else
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
                    w.ShootDelay = w.Comp.Data.Comp.Data.Repo.Values.Set.Overrides.BurstDelay;

                    if (++CompletedCycles >= LastCycle)
                    {
                        EndShootMode();
                    }
                    else
                    {
                        MakeReadyToShoot(true);
                    }
                }

                LastShootTick = Comp.Session.Tick;
            }

            internal bool MakeReadyToShoot(bool skipReady = false)
            {
                var weaponsReady = 0;
                var totalWeapons = Comp.Collection.Count;
                var burstTarget = Comp.Data.Repo.Values.Set.Overrides.BurstCount;
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

                        var canShoot = !w.PartState.Overheated && (!reloading || skipReload);
                        
                        if (canShoot && skipReload)
                            Log.Line($"ReadyToShoot succeeded on client but with CurrentAmmo > 0", Session.InputLog);

                        var weaponReady = canShoot && !w.IsShooting;

                        if (!weaponReady && !skipReady)
                            break;

                        weaponsReady += 1;

                        w.ShootCount = MathHelper.Clamp(burstTarget, 1, w.ProtoWeaponAmmo.CurrentAmmo + w.ClientMakeUpShots);
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

            internal void EndShootMode()
            {
                for (int i = 0; i < Comp.TotalWeapons; i++)
                {
                    var w = Comp.Collection[i];
                    if (Comp.Session.MpActive) Log.Line($"[clear] ammo: {w.ProtoWeaponAmmo.CurrentAmmo} - CompletedCycles:{CompletedCycles} - WeaponsFired:{WeaponsFired}", Session.InputLog);

                    w.ShootCount = 0;
                    w.ShootDelay = 0;
                }

                if (Comp.Session.IsServer)
                {
                    var state = Comp.Data.Repo.Values.State;
                    var oldState = state.ShootSyncStateId;

                    if (RequestShootBurstId != 65535)
                        state.ShootSyncStateId = RequestShootBurstId;
                    else
                    {
                        state.ShootSyncStateId = 0;
                        RequestShootBurstId = 0;
                    }

                    if (Comp.Session.MpActive && oldState != state.ShootSyncStateId)
                    {
                        Comp.Session.SendState(Comp);
                    }
                }
                else
                {
                    RequestShootBurstId = Comp.Data.Repo.Values.State.ShootSyncStateId;
                }

                CompletedCycles = 0;
                WeaponsFired = 0;
                FreezeClientShoot = false;
                WaitingShootResponse = false;
                EarlyOff = false;
                LastCycle = 0;
                ShootToggled = false;
            }

            internal void FailSafe()
            {
                Log.Line($"ShootMode failsafe triggered: Toggled:{ShootToggled} - LastCycle:{LastCycle} - CompletedCycles:{CompletedCycles} - WeaponsFired:{WeaponsFired} - wait:{WaitingShootResponse} - freeze:{FreezeClientShoot} - reqState:{RequestShootBurstId} - state:{Comp.Data.Repo.Values.State.ShootSyncStateId}", Session.InputLog);
                EndShootMode();
            }

            #endregion

            #region InputManager

            internal void RequestShootSync(long playerId) // this shoot method mixes client initiation with server delayed server confirmation in order to maintain sync while avoiding authoritative delays in the common case. 
            {
                var values = Comp.Data.Repo.Values;
                var state = values.State;
                var set = values.Set;

                if (set.Overrides.ShootMode == ShootModes.AiShoot && !ShootToggled  || !ProcessInput(playerId) || !MakeReadyToShoot())
                    return;

                if (Comp.IsBlock && Comp.Session.HandlesInput)
                    Comp.Session.TerminalMon.HandleInputUpdate(Comp);

                RequestShootBurstId = state.ShootSyncStateId + 1;

                state.PlayerId = playerId;

                var sendRequest = !Comp.Session.IsClient || playerId == Comp.Session.PlayerId; // this method is used both by initiators and by receives. 
                
                LastCycle = ShootToggled ? uint.MaxValue : 1;
                
                if (Comp.Session.MpActive && sendRequest)
                {
                    WaitingShootResponse = Comp.Session.IsClient; // this will be set false on the client once the server responds to this packet
                    LockingTick = Comp.Session.Tick;

                    var code = Comp.Session.IsServer ? playerId == 0 ? ShootCodes.ServerRequest : ShootCodes.ServerRelay : ShootCodes.ClientRequest;
                    ulong packagedMessage;
                    Session.EncodeShootState(state.ShootSyncStateId, (uint)set.Overrides.ShootMode, CompletedCycles, (uint)code, out packagedMessage);
                    if (playerId > 0) // if this is the server responding to a request, rewrite the packet sent to the origin client with a special response code.
                        Comp.Session.SendShootRequest(Comp, packagedMessage, PacketType.ShootSync, RewriteShootSyncToServerResponse, playerId);
                    else
                        Comp.Session.SendShootRequest(Comp, packagedMessage, PacketType.ShootSync, null, playerId);
                }
            }


            internal bool ProcessInput(long playerId, bool skipUpdateInputState = false)
            {
                if (!skipUpdateInputState && ShootRequestPending())
                    return false;

                var sendRequest = !Comp.Session.IsClient || playerId == Comp.Session.PlayerId; // this method is used both by initiators and by receives. 
                var set = Comp.Data.Repo.Values.Set;
                var state = Comp.Data.Repo.Values.State;
                var wasToggled = ShootToggled;
                var toggleChange = false;
                if (sendRequest)
                {
                    if (EarlyOff) {
                        Log.Line($"early off: wasSet:{ShootToggled}", Session.InputLog);
                        ShootToggled = false;
                        EarlyOff = false;
                    }
                    else if (set.Overrides.ShootMode == ShootModes.KeyToggle || set.Overrides.ShootMode == ShootModes.MouseControl)
                        ShootToggled = !ShootToggled;
                    else
                        ShootToggled = false;

                    toggleChange = wasToggled && !ShootToggled;

                    if (toggleChange)
                    { // only run on initiators when this call is toggling off 

                        if (Comp.Session.MpActive)
                        {
                            FreezeClientShoot = Comp.Session.IsClient; //if the initiators is a client pause future cycles until the server returns which cycle state to terminate on.
                            LockingTick = Comp.Session.Tick;

                            ulong packagedMessage;
                            Session.EncodeShootState(state.ShootSyncStateId, 0, CompletedCycles, (uint)ShootCodes.ToggleServerOff, out packagedMessage);
                            Comp.Session.SendShootRequest(Comp, packagedMessage, PacketType.ShootSync, RewriteShootSyncToServerResponse, playerId);
                        }

                        if (Comp.Session.IsServer)
                        {
                            EndShootMode();
                            if (Comp.Session.MpActive)
                                Log.Line($"server toggled shoot off, calling EndShoot", Session.InputLog);
                        }
                    }
                }

                var pendingRequest = Comp.IsDisabled || wasToggled || RequestShootBurstId != state.ShootSyncStateId || Comp.IsBlock && !Comp.Cube.IsWorking;
                var deadEnd = pendingRequest && !toggleChange;
                if (deadEnd)
                    Log.Line($"ProcessInput hit deadend: wasToggled:{wasToggled} - stateMisMatch:{RequestShootBurstId != state.ShootSyncStateId} - blockNotWorking:{Comp.IsBlock && !Comp.Cube.IsWorking}", Session.InputLog);

                return !pendingRequest;
            }


            private bool ShootRequestPending()
            {
                var set = Comp.Data.Repo.Values.Set;
                var sMode = set.Overrides.ShootMode;

                if (WaitingShootResponse || FreezeClientShoot)
                {
                    if (set.Overrides.ShootMode == ShootModes.KeyToggle || set.Overrides.ShootMode == ShootModes.MouseControl)
                    {
                        EarlyOff = !EarlyOff && ShootToggled;
                    }
                    Log.Line($"QueueShot:{EarlyOff} - WaitingShootResponse:{WaitingShootResponse} - FreezeClientShoot:{FreezeClientShoot}", Session.InputLog);
                    return true;
                }

                return false;
            }

            #endregion

            #region Network
            internal void ServerToggleOffByClient(uint interval)
            {
                if (interval > CompletedCycles)
                {
                    Log.Line($"client had a higher interval than server: client: {interval} > server:{CompletedCycles} - LastCycle:{LastCycle}", Session.InputLog);
                }
                else if (interval < CompletedCycles)
                {
                    Log.Line($"client had a lower interval than server: client: {interval} < server:{CompletedCycles} - LastCycle:{LastCycle}", Session.InputLog);
                }

                var clientMakeupRequest = interval > CompletedCycles && LastCycle == uint.MaxValue;
                var endCycle = !clientMakeupRequest ? CompletedCycles : interval;

                ulong packagedMessage;
                Session.EncodeShootState(Comp.Data.Repo.Values.State.ShootSyncStateId, (uint)Comp.Data.Repo.Values.Set.Overrides.ShootMode, endCycle, (uint)ShootCodes.ToggleClientOff, out packagedMessage);
                Comp.Session.SendShootRequest(Comp, packagedMessage, PacketType.ShootSync, null, 0);

                if (!clientMakeupRequest)
                    EndShootMode();
                else
                {
                    Log.Line($"server catching up to client -- from:{CompletedCycles} to:{interval}", Session.InputLog);
                    LastCycle = endCycle;
                }
            }

            internal void ServerRejectResponse(ulong clientId)
            {
                Log.Line($"failed to burst on server, sending reject message", Session.InputLog);

                var values = Comp.Data.Repo.Values;
                ulong packagedMessage;
                Session.EncodeShootState(values.State.ShootSyncStateId, (uint)values.Set.Overrides.ShootMode, CompletedCycles, (uint)ShootCodes.ClientRequestReject, out packagedMessage);
                Comp.Session.SendBurstReject(Comp, packagedMessage, PacketType.ShootSync, clientId);
            }

            internal void ClientToggledOffByServer(uint interval)
            {
                if (interval > CompletedCycles)
                {
                    Log.Line($"server interval {interval} > client: {CompletedCycles} - frozen:{FreezeClientShoot} - wait:{WaitingShootResponse}", Session.InputLog);
                }
                else if (interval < CompletedCycles)
                {
                    Log.Line($"server interval {interval} < client:{CompletedCycles} - frozen:{FreezeClientShoot} - wait:{WaitingShootResponse}", Session.InputLog);
                }

                if (interval <= CompletedCycles)
                {
                    EndShootMode();
                }
                else if (LastCycle == uint.MaxValue)
                {
                    Log.Line($"ClientToggleResponse makeup attempt: Current: {CompletedCycles} freeze:{FreezeClientShoot} - target:{interval} - LastCycle:{LastCycle}", Session.InputLog);
                    
                    FreezeClientShoot = false;
                    LastCycle = interval;
                }

            }

            internal void ServerReject()
            {
                Log.Line($"client received reject message reset - wait:{WaitingShootResponse} - frozen:{FreezeClientShoot} - stateMatch:{RequestShootBurstId == Comp.Data.Repo.Values.State.ShootSyncStateId}", Session.InputLog);
                if (CompletedCycles > 0)
                    RestoreWeaponShot();

                EndShootMode();
            }

            private static object RewriteShootSyncToServerResponse(object o)
            {
                var ulongPacket = (ULongUpdatePacket)o;

                uint stateId;
                ShootModes mode;
                ShootCodes code;
                uint internval;

                Session.DecodeShootState(ulongPacket.Data, out stateId, out mode, out internval, out code);

                code = ShootCodes.ServerResponse;
                ulong packagedMessage;
                Session.EncodeShootState(stateId, (uint)mode, internval, (uint)code, out packagedMessage);

                ulongPacket.Data = packagedMessage;

                return ulongPacket;
            }

            #endregion
        }
    }
}
