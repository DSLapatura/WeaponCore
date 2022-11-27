using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoreSystems;
using CoreSystems.Platform;
using CoreSystems.Support;
using VRage.Library.Utils;
using VRage.Utils;
using VRageMath;
using WeaponCore.Data.Scripts.CoreSystems.Support;

namespace WeaponCore.Data.Scripts.CoreSystems.Comms
{
    internal class Spectrum
    {
        private Session _session;
        internal Dictionary<MyStringHash, Frequency> Channels = new Dictionary<MyStringHash, Frequency>();
        internal Spectrum(Session session)
        {
            _session = session;
        }
    }

    internal class Frequency
    {
        public enum LicensedFor
        {
            BroadCasters,
            Relayers,
            Both,
        }
        internal readonly Dictionary<LicensedFor, List<RadioSource>> Nodes = new Dictionary<LicensedFor, List<RadioSource>>();
        internal readonly SpaceTrees Tree = new SpaceTrees();
        internal readonly LicensedFor Rights;
        internal readonly MyStringHash HashId;
        internal readonly LicensedFor[] Licenses;
        internal readonly string Id;
        internal bool Dirty;

        public Frequency(LicensedFor rights, MyStringHash hashId)
        {
            Rights = rights;
            HashId = hashId;
            Id = hashId.String;
            Licenses = new LicensedFor[Enum.GetNames(typeof(LicensedFor)).Length];

            for (int i = 0; i < Licenses.Length; i++)
            {
                var license = (LicensedFor) i;
                Licenses[i] = license;
                Nodes[license] = new List<RadioSource>();
            }

        }

        public bool TryAddOrUpdateSource(RadioSource source)
        {
            switch (Rights)
            {
                case LicensedFor.Both:
                    break;
                case LicensedFor.BroadCasters:
                    break;
                case LicensedFor.Relayers:
                    break;
            }

            return false;
        }

        public bool TryRemoveSource(RadioSource source)
        {
            switch (Rights)
            {
                case LicensedFor.Both:
                    break;
                case LicensedFor.BroadCasters:
                    break;
                case LicensedFor.Relayers:
                    break;
            }

            return false;
        }
    }

    internal class RadioSource
    {
        private readonly Ai.Constructs _station;
        private readonly Spectrum _spectrum;
        private Vector3D LastUpdatePosition;
        internal int PruningProxyId = -1;

        internal RadioSource(Ai.Constructs rootConstruct, Spectrum spectrum)
        {
            _station = rootConstruct;
            _spectrum = spectrum;
        }

        internal void UpdatePosition()
        {
            var topEntity = _station.Ai.TopEntity;
            var center = topEntity.PositionComp.WorldAABB.Center;
            var sizeSqr = topEntity.PositionComp.LocalVolume.Radius * topEntity.PositionComp.LocalVolume.Radius;
            var vel = topEntity.Physics.LinearVelocity;
            foreach (var map in _station.RadioMap)
            {
                var freq = _spectrum.Channels[map.Key];
                var radios = _station.RadioMap[map.Key];
                var volume = new BoundingSphereD(center, radios.MaxInfluenceRange);
                if (PruningProxyId == -1)
                    freq.Tree.RegisterSignal(this, ref volume);
                else if (Vector3D.DistanceSquared(LastUpdatePosition, center) > sizeSqr)
                {
                    LastUpdatePosition = center;
                    freq.Tree.OnSignalMoved(this, ref vel, ref volume);
                }
            }
        }
    }

    public class Radios
    {
        internal HashSet<Radio> Participants = new HashSet<Radio>();
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
            foreach (var radio in Participants)
            {
                switch (radio.Type)
                {
                    case Radio.RadioTypes.Transmitter:
                        if (radio.TransmitRange > FurthestTransmiter)
                            FurthestTransmiter = radio.TransmitRange;

                        if (radio.TransmitRange > MaxInfluenceRange)
                            MaxInfluenceRange = radio.TransmitRange;
                        break;
                    case Radio.RadioTypes.Receiver:
                        if (radio.ReceiveRange > FurthestReceiver)
                            FurthestReceiver = radio.ReceiveRange;

                        if (radio.ReceiveRange > MaxInfluenceRange)
                            MaxInfluenceRange = radio.ReceiveRange;
                        break;
                    case Radio.RadioTypes.Jammer:
                        if (radio.JamRange > FurthestJammer)
                            FurthestJammer = radio.JamRange;

                        if (radio.JamRange > MaxInfluenceRange)
                            MaxInfluenceRange = radio.JamRange;
                        break;
                }
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
                Transmitter,
                Receiver,
                Jammer,
                Relay
            }

            internal RadioTypes Type;
            internal double TransmitRange;
            internal double ReceiveRange;
            internal double JamRange;
        }
    }
}
