using CoreSystems.Platform;
using CoreSystems.Support;
using Sandbox.Game.Entities;
using static CoreSystems.Support.Ai;
namespace CoreSystems
{
    public partial class Session
    {
        public void CleanClientPackets()
        {
            foreach (var packet in ClientPacketsToClean)
                PacketObjPool.Return(packet);

            ClientPacketsToClean.Clear();
        }

        public void ReproccessClientErrorPackets()
        {
            foreach (var packetObj in ClientSideErrorPkt)
            {
                var errorPacket = packetObj.ErrorPacket;
                var packet = packetObj.Packet;

                if (errorPacket.MaxAttempts == 0)  {
                    Log.LineShortDate($"        [ClientReprocessing] Entity:{packet.EntityId} - Type:{packet.PType}", "net");
                    //set packet retry variables, based on type
                    errorPacket.MaxAttempts = 7;
                    errorPacket.RetryDelayTicks = 15;
                    errorPacket.RetryTick = Tick + errorPacket.RetryDelayTicks;
                }

                if (errorPacket.RetryTick > Tick) continue;
                errorPacket.RetryAttempt++;
                var success = ProccessClientPacket(packetObj, false) || packetObj.Report.PacketValid;

                if (success || errorPacket.RetryAttempt > errorPacket.MaxAttempts)  {

                    if (!success)  
                        Log.LineShortDate($"        [BadReprocess] Entity:{packet.EntityId} Cause:{errorPacket.Error ?? string.Empty} Type:{packet.PType}", "net");
                    else Log.LineShortDate($"        [ReprocessSuccess] Entity:{packet.EntityId} - Type:{packet.PType} - Retries:{errorPacket.RetryAttempt}", "net");

                    ClientSideErrorPkt.Remove(packetObj);
                    ClientPacketsToClean.Add(packetObj);
                }
                else
                    errorPacket.RetryTick = Tick + errorPacket.RetryDelayTicks;
            }
            ClientSideErrorPkt.ApplyChanges();
        }

        private bool ClientConstruct(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var cgPacket = (ConstructPacket)packet;
            if (myGrid == null) return Error(data, Msg($"Grid: {packet.EntityId}"));

            Ai ai;
            if (EntityToMasterAi.TryGetValue(myGrid, out ai)) {
                var rootConstruct = ai.Construct.RootAi.Construct;

                rootConstruct.Data.Repo.Sync(rootConstruct, cgPacket.Data);
                rootConstruct.UpdateLeafs();

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{EntityToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientConstructFoci(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var fociPacket = (ConstructFociPacket)packet;
            if (myGrid == null) return Error(data, Msg($"Grid: {packet.EntityId}"));

            Ai ai;
            if (EntityToMasterAi.TryGetValue(myGrid, out ai))
            {
                var rootConstruct = ai.Construct.RootAi.Construct;
                rootConstruct.Data.Repo.FocusData.Sync(ai, fociPacket.Data);

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{EntityToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientAiDataUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var aiSyncPacket = (AiDataPacket)packet;
            if (myGrid == null) return Error(data, Msg($"Grid: {packet.EntityId}"));

            Ai ai;
            if (EntityAIs.TryGetValue(myGrid, out ai)) {

                ai.Data.Repo.Sync(aiSyncPacket.Data);

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{EntityToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientWeaponComp(PacketObj data)
        {
            var packet = data.Packet;
            var compDataPacket = (WeaponCompPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            if (!comp.Data.Repo.Values.Sync(comp, compDataPacket.Data))
                Log.Line($"ClientWeaponComp: version fail - senderId:{packet.SenderId} - version:{comp.Data.Repo.Values.Revision}({compDataPacket.Data.Revision})");

            if (comp.IsBomb) comp.Cube.UpdateTerminal();
            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientWeaponState(PacketObj data)
        {
            var packet = data.Packet;
            var compStatePacket = (WeaponStatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>() as Weapon.WeaponComponent;
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            comp.Data.Repo.Values.State.Sync(comp, compStatePacket.Data);

            if (comp.IsBomb) comp.Cube.UpdateTerminal();
            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientUpgradeComp(PacketObj data)
        {
            var packet = data.Packet;
            var compDataPacket = (UpgradeCompPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>() as Upgrade.UpgradeComponent;
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            if (!comp.Data.Repo.Values.Sync(comp, compDataPacket.Data))
                Log.Line($"ClientUpgradeComp: version fail - senderId:{packet.SenderId} - version:{comp.Data.Repo.Values.Revision}({compDataPacket.Data.Revision})");

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientUpgradeState(PacketObj data)
        {
            var packet = data.Packet;
            var compStatePacket = (UpgradeStatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>() as Upgrade.UpgradeComponent;
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            if (!comp.Data.Repo.Values.State.Sync(comp, compStatePacket.Data, ProtoUpgradeState.Caller.Direct))
                Log.Line($"ClientUpgradeState: version fail - senderId:{packet.SenderId} - version:{comp.Data.Repo.Values.Revision}({compStatePacket.Data.Revision})");

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientSupportComp(PacketObj data)
        {
            var packet = data.Packet;
            var compDataPacket = (SupportCompPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>() as SupportSys.SupportComponent;
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            if (!comp.Data.Repo.Values.Sync(comp, compDataPacket.Data))
                Log.Line($"ClientSupportComp: version fail - senderId:{packet.SenderId} - version:{comp.Data.Repo.Values.Revision}({compDataPacket.Data.Revision})");

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientSupportState(PacketObj data)
        {
            var packet = data.Packet;
            var compStatePacket = (SupportStatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>() as SupportSys.SupportComponent;
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            if (!comp.Data.Repo.Values.State.Sync(comp, compStatePacket.Data, ProtoSupportState.Caller.Direct))
                Log.Line($"ClientSupportState: version fail - senderId:{packet.SenderId} - version:{comp.Data.Repo.Values.Revision}({compStatePacket.Data.Revision})");

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientControlComp(PacketObj data)
        {
            var packet = data.Packet;
            var compDataPacket = (ControlCompPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            if (!comp.Data.Repo.Values.Sync(comp, compDataPacket.Data))
                Log.Line($"ClientSupportComp: version fail - senderId:{packet.SenderId} - version:{comp.Data.Repo.Values.Revision}({compDataPacket.Data.Revision})");

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientControlState(PacketObj data)
        {
            var packet = data.Packet;
            var compStatePacket = (ControlStatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>() as ControlSys.ControlComponent;
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            if (!comp.Data.Repo.Values.State.Sync(comp, compStatePacket.Data, ProtoControlState.Caller.Direct))
                Log.Line($"ClientSupportState: version fail - senderId:{packet.SenderId} - version:{comp.Data.Repo.Values.Revision}({compStatePacket.Data.Revision})");

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientWeaponReloadUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var weaponReloadPacket = (WeaponReloadPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>();
            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            var collection = comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? comp.Platform.Weapons : comp.Platform.Phantoms;
            var w = collection[weaponReloadPacket.PartId];
            w.Reload.Sync(w, weaponReloadPacket.Data, false);

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var targetPacket = (TargetPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>();

            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready ) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));
            var collection = comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? comp.Platform.Weapons : comp.Platform.Phantoms;
            var w = collection[targetPacket.Target.PartId];
            targetPacket.Target.SyncTarget(w, false);

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientWeaponAmmoUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var ammoPacket = (WeaponAmmoPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>();

            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));
            var collection = comp.TypeSpecific != CoreComponent.CompTypeSpecific.Phantom ? comp.Platform.Weapons : comp.Platform.Phantoms;
            var w = collection[ammoPacket.PartId];
            w.ProtoWeaponAmmo.Sync(w, ammoPacket.Data);

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientFakeTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            data.ErrorPacket.NoReprocess = true;
            var targetPacket = (FakeTargetPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            Ai ai;
            if (myGrid != null && EntityAIs.TryGetValue(myGrid, out ai)) {

                long playerId;
                if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId)) {

                    FakeTargets dummyTargets;
                    if (PlayerDummyTargets.TryGetValue(playerId, out dummyTargets)) {
                        dummyTargets.ManualTarget.Sync(targetPacket, ai);
                    }
                    else
                        return Error(data, Msg("Player dummy target not found"));
                }
                else
                    return Error(data, Msg("SteamToPlayer missing Player"));

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridId: {packet.EntityId}", myGrid != null), Msg("Ai"));

            return true;
        }

        private bool ClientPaintedTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            data.ErrorPacket.NoReprocess = true;
            var targetPacket = (PaintedTargetPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            Ai ai;
            if (myGrid != null && EntityAIs.TryGetValue(myGrid, out ai))
            {

                long playerId;
                if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                {

                    FakeTargets dummyTargets;
                    if (PlayerDummyTargets.TryGetValue(playerId, out dummyTargets))
                    {
                        dummyTargets.PaintedTarget.Sync(targetPacket, ai);
                    }
                    else
                        return Error(data, Msg("Player dummy target not found"));
                }
                else
                    return Error(data, Msg("SteamToPlayer missing Player"));

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg($"GridId: {packet.EntityId}", myGrid != null), Msg("Ai"));

            return true;
        }
        // no storge sync


        private bool ClientPlayerIdUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var updatePacket = (BoolUpdatePacket)packet;

            if (updatePacket.Data)
                PlayerConnected(updatePacket.EntityId);
            else //remove
                PlayerDisconnected(updatePacket.EntityId);

            data.Report.PacketValid = true;
            return true;
        }

        private bool ClientServerData(PacketObj data)
        {
            var packet = data.Packet;
            var updatePacket = (ServerPacket)packet;

            ServerVersion = updatePacket.VersionString;
            Settings.VersionControl.UpdateClientEnforcements(updatePacket.Data);
            data.Report.PacketValid = true;
            Log.Line("Server enforcement received");

            UpdateEnforcement();

            return true;
        }

        private bool ClientNotify(PacketObj data)
        {
            var packet = data.Packet;
            var clientNotifyPacket = (ClientNotifyPacket)packet;

            if (clientNotifyPacket.Message == string.Empty || clientNotifyPacket.Color == string.Empty) return Error(data, Msg("BaseData"));

            ShowClientNotify(clientNotifyPacket);
            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientEwarBlocks(PacketObj data)
        {
            var packet = data.Packet;
            var queueShot = (EwaredBlocksPacket)packet;
            if (queueShot?.Data == null)
            {
                return false;
            }

            CurrentClientEwaredCubes.Clear();

            for (int i = 0; i < queueShot.Data.Count; i++)
            {
                var values = queueShot.Data[i];
                CurrentClientEwaredCubes[values.EwaredBlockId] = values;
            }
            ClientEwarStale = true;

            data.Report.PacketValid = true;

            return true;
        }

        // Unmanaged state changes below this point

        private bool ClientSentReport(PacketObj data)
        {
            var packet = data.Packet;
            var sentReportPacket = (ProblemReportPacket)packet;
            if (sentReportPacket.Data == null) return Error(data, Msg("SentReport"));
            Log.Line("remote data received");
            ProblemRep.RemoteData = sentReportPacket.Data;
            data.Report.PacketValid = true;

            return true;

        }

        private bool ClientProjectileSyncs(PacketObj data)
        {
            var packet = data.Packet;
            var proSync = (ProjectileSyncPacket)packet;
            if (proSync.Data == null) return Error(data, Msg("ProSyncData"));

            for (int i = 0; i < proSync.Data.Count; i++)
            {
                var sync = proSync.Data[i];
                var dict = WeaponProSyncs[sync.UniquePartId];
                var protoWeaponProSync = dict[sync.ProId];
                protoWeaponProSync.Position = sync.Position;
                protoWeaponProSync.Velocity = sync.Velocity;
                protoWeaponProSync.Type = sync.Type;
                protoWeaponProSync.ProId = sync.ProId;
                protoWeaponProSync.TargetId = sync.TargetId;
                protoWeaponProSync.UniquePartId = sync.UniquePartId;
            }
            proSync.CleanUp();
            return true;
        }

        private bool ClientShootSyncs(PacketObj data)
        {
            var packet = data.Packet;
            var dPacket = (ULongUpdatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<CoreComponent>();

            if (comp?.Ai == null || comp.Platform.State != CorePlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == CorePlatform.PlatformState.Ready));

            Weapon.ShootManager.RequestType type;
            Weapon.ShootManager.Signals signal;
            Weapon.ShootManager.ShootCodes code;
            uint interval;

            Weapon.ShootManager.DecodeShootState(dPacket.Data, out type, out signal, out interval, out code);

            var wComp = comp as Weapon.WeaponComponent;
            if (wComp != null)
            {
                switch (code)
                {
                    case Weapon.ShootManager.ShootCodes.ClientRequestReject:
                        wComp.ShootManager.ServerReject();
                        break;
                    case Weapon.ShootManager.ShootCodes.ToggleClientOff:
                        wComp.ShootManager.ClientToggledOffByServer(interval);
                        break;
                    case Weapon.ShootManager.ShootCodes.ServerResponse:
                        wComp.ShootManager.WaitingShootResponse = false;
                        break;
                    case Weapon.ShootManager.ShootCodes.ToggleServerOff:
                        wComp.ShootManager.ClientToggledOffByServer(interval, true);
                        break;
                    default:
                        Log.Line($"default server response");
                        long playerId;
                        SteamToPlayer.TryGetValue(packet.SenderId, out playerId);
                        wComp.ShootManager.RequestShootSync(0, type, signal);
                        break;
                }
            }

            data.Report.PacketValid = true;
            return true;
        }
    }
}
