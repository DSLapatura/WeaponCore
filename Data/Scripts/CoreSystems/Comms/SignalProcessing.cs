using CoreSystems.Platform;
using CoreSystems.Support;
using System.Collections.Generic;
using VRage.Utils;
using static WeaponCore.Data.Scripts.CoreSystems.Comms.Radio;
using VRageMath;

namespace WeaponCore.Data.Scripts.CoreSystems.Comms
{
    internal class RadioStation
    {
        internal readonly Radios Radios = new Radios();
        private readonly Ai.Constructs _station;
        private readonly Spectrum _spectrum;
        private Vector3D _lastUpdatePosition;
        internal int PruningProxyId = -1;

        internal RadioStation(Ai.Constructs rootConstruct, Spectrum spectrum)
        {
            _station = rootConstruct;
            _spectrum = spectrum;
        }

        internal void RegisterAndUpdateVolume()
        {
            var topEntity = _station.Ai.TopEntity;
            var center = topEntity.PositionComp.WorldAABB.Center;
            var sizeSqr = topEntity.PositionComp.LocalVolume.Radius * topEntity.PositionComp.LocalVolume.Radius;
            var vel = topEntity.Physics.LinearVelocity;
            foreach (var channel in Radios.RadioMap.Keys)
            {
                var freq = _spectrum.Channels[channel];
                var volume = new BoundingSphereD(center, Radios.MaxInfluenceRange);
                if (PruningProxyId == -1)
                    freq.Tree.RegisterSignal(this, ref volume);
                else if (Vector3D.DistanceSquared(_lastUpdatePosition, center) > sizeSqr)
                {
                    _lastUpdatePosition = center;
                    freq.Tree.OnSignalMoved(this, ref vel, ref volume);
                }
            }
        }

        internal void UnRegisterAll()
        {
            foreach (var map in Radios.RadioMap)
            {
                var freq = _spectrum.Channels[map.Key];
                if (PruningProxyId != -1)
                    freq.Tree.UnregisterSignal(this);
            }
        }

        internal void UnRegisterAll(MyStringHash id)
        {
            var freq = _spectrum.Channels[id];
            if (PruningProxyId != -1)
                freq.Tree.UnregisterSignal(this);
        }

        internal void Clean()
        {

        }
    }

    public class Radios
    {
        internal readonly Dictionary<int, Radio> RadioTypeMap = new Dictionary<int, Radio>();
        internal readonly Dictionary<MyStringHash, List<Radio>> RadioMap = new Dictionary<MyStringHash, List<Radio>>();

        internal double FurthestTransmiter;
        internal double FurthestReceiver;
        internal double FurthestJammer;
        internal double MaxInfluenceRange;

        internal void UpdateLocalInfluenceBounds()
        {
            FurthestTransmiter = 0;
            FurthestReceiver = 0;
            FurthestJammer = 0;
            MaxInfluenceRange = 0;
            foreach (var pair in RadioTypeMap)
            {
                var type = (RadioTypes)pair.Key;
                var radio = pair.Value;
                switch (type)
                {
                    case RadioTypes.Transmitter:
                        if (radio.TransmitRange > FurthestTransmiter)
                            FurthestTransmiter = radio.TransmitRange;

                        if (radio.TransmitRange > MaxInfluenceRange)
                            MaxInfluenceRange = radio.TransmitRange;
                        break;
                    case RadioTypes.Receiver:
                        if (radio.ReceiveRange > FurthestReceiver)
                            FurthestReceiver = radio.ReceiveRange;

                        if (radio.ReceiveRange > MaxInfluenceRange)
                            MaxInfluenceRange = radio.ReceiveRange;
                        break;
                    case RadioTypes.Jammer:
                        if (radio.JamRange > FurthestJammer)
                            FurthestJammer = radio.JamRange;

                        if (radio.JamRange > MaxInfluenceRange)
                            MaxInfluenceRange = radio.JamRange;
                        break;
                }
            }
        }
        internal void Clean()
        {

        }
    }

    public class Radio
    {
        private Weapon _weapon;
        internal Radio(Weapon w)
        {
            _weapon = w;
            Type = w.System.RadioType;
        }

        public enum RadioTypes
        {
            None,
            Slave,
            Master,
            Transmitter,
            Repeater,
            Receiver,
            Jammer,
            Relay
        }

        internal RadioTypes Type;
        internal double TransmitRange;
        internal double ReceiveRange;
        internal double JamRange;

        internal void Clean()
        {

        }
    }
}
