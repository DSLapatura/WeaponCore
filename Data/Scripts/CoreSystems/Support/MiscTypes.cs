using System;
using System.Collections.Generic;
using CoreSystems.Platform;
using CoreSystems.Projectiles;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition.TargetingDef;

namespace CoreSystems.Support
{
    public class Target
    {
        internal Part Part;
        internal MyEntity TargetEntity;
        internal Projectile Projectile;
        internal Vector3D TargetPos;
        internal Vector3D TargetingOrigin;
        internal Vector3D OriginTargetDir;

        internal States CurrentState = States.NotSet;
        internal TargetStates TargetState;

        internal bool HasTarget;
        internal bool IsAligned;
        internal bool SoftProjetileReset;
        internal bool TargetChanged;
        internal bool IsTargetStorage;
        internal bool ClientDirty;
        internal bool IsDrone;

        internal uint ExpiredTick;
        internal uint ResetTick;
        internal uint ProjectileEndTick;
        internal long TargetId;
        internal long TopEntityId;

        internal double HitShortDist;
        internal double OrigDistance;

        public enum TargetStates
        {
            None,
            IsProjectile,
            WasProjectile,
            IsFake,
            WasFake,
            IsEntity,
            WasEntity,
        }

        public enum States
        {
            NotSet,
            ControlReset,
            Expired,
            WeaponNotReady,
            AnimationOff,
            Designator,
            Acquired,
            NoTargetsSeen,
            RayCheckFailed,
            RayCheckSelfHit,
            RayCheckFriendly,
            RayCheckDistOffset,
            RayCheckVoxel,
            RayCheckProjectile,
            RayCheckDeadBlock,
            RayCheckDistExceeded,
            RayCheckOther,
            RayCheckMiss,
            ServerReset,
            Transfered,
            Fake,
            FiredBurst,
            AiLost,
            Offline,
            LostTracking,
            ProjectileClean,
            ProjetileIntercept,
            ProjectileClose,
            ProjectileNewTarget,
        }

        internal Target(Part part = null, bool main = false)
        {
            Part = part;
            IsTargetStorage = main;
        }

        internal void PushTargetToClient(Weapon w)
        {
            if (!w.System.Session.MpActive || w.System.Session.IsClient)
                return;

            w.TargetData.TargetPos = TargetPos;
            w.TargetData.PartId = w.PartId;
            w.TargetData.EntityId = w.Target.TargetId;
            
            if (!w.ActiveAmmoDef.AmmoDef.Const.Reloadable && w.Target.TargetId != 0)
                w.ProjectileCounter = 0;

            w.System.Session.SendTargetChange(w.Comp, w.PartId);
        }

        internal void ClientUpdate(Weapon w, ProtoWeaponTransferTarget tData)
        {
            if (w.System.Session.Tick < w.Target.ProjectileEndTick)
            {
                var first = w.Target.SoftProjetileReset;
                if (first)
                {
                    w.TargetData.WeaponRandom.AcquireRandom = new XorShiftRandomStruct((ulong)w.TargetData.WeaponRandom.CurrentSeed);
                    w.Target.SoftProjetileReset = false;
                }

                if (first || w.System.Session.Tick20)
                {
                    Ai.TargetType targetType;
                    Ai.AcquireProjectile(w, out targetType);

                    if (targetType == Ai.TargetType.None)
                    {
                        if (w.NewTarget.CurrentState != States.NoTargetsSeen)
                            w.NewTarget.Reset(w.Comp.Session.Tick, States.NoTargetsSeen);

                        if (w.Target.CurrentState != States.NoTargetsSeen)
                            w.Target.Reset(w.Comp.Session.Tick, States.NoTargetsSeen, !w.Comp.Data.Repo.Values.State.TrackingReticle && w.Comp.Data.Repo.Values.Set.Overrides.Control != ProtoWeaponOverrides.ControlModes.Painter);
                    }
                }
                return;
            }

            MyEntity targetEntity = null;
            if (tData.EntityId <= 0 || MyEntities.TryGetEntityById(tData.EntityId, out targetEntity, true))
            {
                TargetEntity = targetEntity;

                if (tData.EntityId == 0)
                {
                    if (w.Target.TargetState == TargetStates.IsFake)
                        w.Target.Reset(w.System.Session.Tick, States.ServerReset);
                    else
                        w.DelayedTargetResetTick = w.System.Session.Tick + 30;
                }
                else
                {
                    StateChange(true, tData.EntityId == -2 ? States.Fake : States.Acquired);

                    if (w.Target.TargetState == TargetStates.None && tData.EntityId != 0)
                        w.TargetData.SyncTarget(w);

                    if (w.Target.TargetState == TargetStates.IsProjectile)
                    {
                        Ai.TargetType targetType;
                        Ai.AcquireProjectile(w, out targetType);

                        if (targetType == Ai.TargetType.None)
                        {
                            if (w.NewTarget.CurrentState != States.NoTargetsSeen)
                                w.NewTarget.Reset(w.Comp.Session.Tick, States.NoTargetsSeen);

                            if (w.Target.CurrentState != States.NoTargetsSeen)
                            {
                                w.Target.Reset(w.Comp.Session.Tick, States.NoTargetsSeen, !w.Comp.Data.Repo.Values.State.TrackingReticle && w.Comp.Data.Repo.Values.Set.Overrides.Control != ProtoWeaponOverrides.ControlModes.Painter);
                            }
                        }
                    }
                }

                if (w.System.Session.Tick != w.Target.ProjectileEndTick)
                    w.TargetData.WeaponRandom.AcquireRandom = new XorShiftRandomStruct((ulong)w.TargetData.WeaponRandom.CurrentSeed);

                ClientDirty = false;
            }

            w.Target.ExpiredTick = w.System.Session.Tick;
        }

        internal void TransferTo(Target target, uint expireTick, bool drone = false)
        {
            target.IsDrone = drone;
            target.TargetEntity = TargetEntity;
            target.Projectile = Projectile;
            target.TargetPos = TargetPos;
            target.TargetingOrigin = TargetingOrigin;

            target.HitShortDist = HitShortDist;
            target.OrigDistance = OrigDistance;
            target.TopEntityId = TopEntityId;
            target.TargetState = TargetState;

            target.StateChange(HasTarget, CurrentState);
            Reset(expireTick, States.Transfered);
        }

        internal void Set(MyEntity ent, Vector3D pos, Vector3D targetingOrigin, double shortDist, double origDist, long topEntId, Projectile projectile = null, bool isFakeTarget = false)
        {
            TargetEntity = ent;
            Projectile = projectile;
            TargetPos = pos;
            TargetingOrigin = targetingOrigin;
            HitShortDist = shortDist;
            OrigDistance = origDist;
            TopEntityId = topEntId;

            if (projectile != null)
                TargetState = TargetStates.IsProjectile;
            else if (isFakeTarget)
                TargetState = TargetStates.IsFake;
            else if (ent != null)
                TargetState = TargetStates.IsEntity;
            else
                TargetState = TargetStates.None;

            StateChange(true, States.Acquired);
        }


        internal void SetFake(uint expiredTick, Vector3D pos, Vector3D targetingOrigin)
        {
            Reset(expiredTick, States.Fake, false);
            TargetState = TargetStates.IsFake;
            TargetPos = pos;
            TargetingOrigin = targetingOrigin;
            StateChange(true, States.Fake);
        }

        internal void Reset(uint expiredTick, States reason, bool expire = true)
        {
            switch (TargetState)
            {
                case TargetStates.IsProjectile:
                    TargetState = TargetStates.WasProjectile;
                    break;
                case TargetStates.IsFake:
                    TargetState = TargetStates.WasFake;
                    break;
                case TargetStates.IsEntity:
                    TargetState = TargetStates.WasEntity;
                    break;
                default:
                    TargetState = TargetStates.None;
                    break;
            }

            IsDrone = false;
            TargetEntity = null;
            IsAligned = false;
            Projectile = null;
            TargetPos = Vector3D.Zero;
            TargetingOrigin = Vector3D.Zero;
            HitShortDist = 0;
            OrigDistance = 0;
            TopEntityId = 0;
            TargetId = 0;
            ResetTick = expiredTick;
            SoftProjetileReset = false;
            if (expire)
            {
                StateChange(false, reason);
                ExpiredTick = expiredTick;
            }
        }

        internal void StateChange(bool setTarget, States reason)
        {
            SetTargetId(setTarget, reason);
            TargetChanged = !HasTarget && setTarget || HasTarget && !setTarget;

            if (TargetChanged && IsTargetStorage) {

                if (setTarget) {
                    Part.BaseComp.Ai.WeaponsTracking++;
                    Part.BaseComp.PartTracking++;
                }
                else {
                    Part.BaseComp.Ai.WeaponsTracking--;
                    Part.BaseComp.PartTracking--;
                }
            }
            HasTarget = setTarget;
            CurrentState = reason;
        }

        internal void SetTargetId(bool setTarget, States reason)
        {
            switch (TargetState)
            {
                case TargetStates.IsProjectile:
                    TargetId = -1;
                    break;
                case TargetStates.IsFake:
                    TargetId = -2;
                    break;
                case TargetStates.IsEntity:
                    TargetId = TargetEntity.EntityId;
                    break;
                default:
                    TargetId = 0;
                    break;
            }
        }
    }

    public class ParticleEvent
    {
        private readonly Guid _uid;
        public readonly Dummy MyDummy;
        public readonly Vector4 Color;
        public readonly Vector3 Offset;
        public readonly Vector3D EmptyPos;
        public readonly string ParticleName;
        public readonly string EmptyNames;
        public readonly string[] MuzzleNames;
        public readonly string PartName;
        public readonly float MaxPlayTime;
        public readonly uint StartDelay;
        public readonly uint LoopDelay;
        public readonly float Scale;
        public readonly float Distance;
        public readonly bool DoesLoop;
        public readonly bool Restart;
        public readonly bool ForceStop;

        public bool Playing;
        public bool Stop;
        public bool Triggered;
        public uint PlayTick;
        public MyParticleEffect Effect;

        public ParticleEvent(string particleName, string emptyName, Vector4 color, Vector3 offset, float scale, float distance, float maxPlayTime, uint startDelay, uint loopDelay, bool loop, bool restart, bool forceStop, params string[] muzzleNames)
        {
            ParticleName = particleName;
            EmptyNames = emptyName;
            MuzzleNames = muzzleNames;
            Color = color;
            Offset = offset;
            Scale = scale;
            Distance = distance;
            MaxPlayTime = maxPlayTime > 0 ? maxPlayTime : float.MaxValue;
            StartDelay = startDelay;
            LoopDelay = loopDelay;
            DoesLoop = loop;
            Restart = restart;
            ForceStop = forceStop;
            _uid = Guid.NewGuid();
        }

        public ParticleEvent(ParticleEvent copyFrom, Dummy myDummy, string partName, Vector3 pos)
        {
            MyDummy = myDummy;
            PartName = partName;
            EmptyNames = copyFrom.EmptyNames;
            MuzzleNames = copyFrom.MuzzleNames;
            ParticleName = copyFrom.ParticleName;
            Color = copyFrom.Color;
            Offset = copyFrom.Offset;
            EmptyPos = pos;
            Scale = copyFrom.Scale;
            Distance = copyFrom.Distance;
            MaxPlayTime = copyFrom.MaxPlayTime;
            StartDelay = copyFrom.StartDelay;
            LoopDelay = copyFrom.LoopDelay;
            DoesLoop = copyFrom.DoesLoop;
            Restart = copyFrom.Restart;
            ForceStop = copyFrom.ForceStop;
            _uid = Guid.NewGuid();
        }

        protected bool Equals(ParticleEvent other)
        {
            return Equals(_uid, other._uid);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ParticleEvent)obj);
        }

        public override int GetHashCode()
        {
            return _uid.GetHashCode();
        }
    }
}
