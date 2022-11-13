using CoreSystems.Projectiles;
using CoreSystems.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using static CoreSystems.Support.Ai;
using static CoreSystems.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

namespace CoreSystems.Platform
{
    public partial class Weapon
    {
        public partial class WeaponComponent 
        {
            private void HandInit(IMyAutomaticRifleGun gun, out IMyAutomaticRifleGun rifle, out MyCharacterWeaponPositionComponent characterPosComp, out IMyHandheldGunObject<MyGunBase> gunBase, out MyEntity topEntity)
            {
                rifle = gun;
                gunBase = gun;
                topEntity = Rifle.Owner;
                characterPosComp = gun.Owner.Components.Get<MyCharacterWeaponPositionComponent>();

                gun.GunBase.OnAmmoAmountChanged += KeenGiveModdersSomeMoreLove;
                gun.OnMarkForClose += OnRifleMarkForClose;
            }

            private void KeenGiveModdersSomeMoreLove()
            {
                Session.FutureEvents.Schedule(ForceAmmoValues, null, 0);
            }

            private void ForceAmmoValues(object o)
            {
                if (PrimaryWeapon.Loading)
                    return;

                if (Rifle.CurrentMagazineAmount != PrimaryWeapon.Reload.CurrentMags)
                    Rifle.CurrentMagazineAmount = PrimaryWeapon.Reload.CurrentMags;

                if (Rifle.CurrentMagazineAmmunition != PrimaryWeapon.ProtoWeaponAmmo.CurrentAmmo + PrimaryWeapon.ClientMakeUpShots)
                    Rifle.CurrentMagazineAmmunition = PrimaryWeapon.ProtoWeaponAmmo.CurrentAmmo + PrimaryWeapon.ClientMakeUpShots;

            }

            private void OnRifleMarkForClose(IMyEntity myEntity)
            {
                Rifle.GunBase.OnAmmoAmountChanged -= KeenGiveModdersSomeMoreLove;
                Rifle.OnMarkForClose -= OnRifleMarkForClose;
            }

            internal void ForceReload()
            {
                Rifle.CurrentMagazineAmount = 0;
                Rifle.CurrentAmmunition = 0;

                RequestForceReload();
            }

            internal void HandheldReload(Weapon w, EventTriggers state, bool active)
            {
                if (active && state == EventTriggers.Reloading)
                {
                    if (Session.IsServer)
                        Rifle.Reload();
                }
                else
                {
                    Session.FutureEvents.Schedule(ForceAmmoValues, null, 15);
                }
            }

            internal void HandhelShoot(Weapon w, EventTriggers state, bool active)
            {
                if (active)
                {
                    Rifle.Shoot(MyShootActionEnum.PrimaryAction, Vector3D.MaxValue, Vector3D.MaxValue);
                }
            }

            internal void AmmoStorage(bool load = false)
            {

                if (Session.IsCreative)
                {
                    if (load)
                        PrimaryWeapon.ProtoWeaponAmmo.CurrentAmmo = Rifle.CurrentMagazineAmmunition;
                    return;
                }

                foreach (var item in CoreInventory.GetItems())
                {
                    var physGunOb = item.Content as MyObjectBuilder_PhysicalGunObject;

                    if (physGunOb?.GunEntity is MyObjectBuilder_AutomaticRifle && Session.CoreSystemsDefs.ContainsKey(physGunOb.SubtypeId.String))
                    {

                        WeaponObStorage storage;
                        var newStorage = false;
                        if (!Ai.WeaponAmmoCountStorage.TryGetValue(physGunOb, out storage))
                        {
                            newStorage = true;
                            Rifle.CurrentMagazineAmount = PrimaryWeapon.Reload.CurrentMags;
                            storage = new WeaponObStorage
                            {
                                CurrentAmmunition = Rifle.CurrentAmmunition,
                                CurrentMagazineAmmunition = Rifle.CurrentMagazineAmmunition,
                                CurrentMagazineAmount = Rifle.CurrentMagazineAmount
                            };

                            Ai.WeaponAmmoCountStorage[physGunOb] = storage;
                            Log.Line($"creating new storage for: loading:{load} - isMe:{physGunOb.GunEntity.EntityId == GunBase.PhysicalObject.GunEntity.EntityId} - {physGunOb.GunEntity.EntityId}[{GunBase.PhysicalObject.GunEntity.EntityId}] - {physGunOb.SubtypeName}");
                        }
                        else
                            Log.Line($"retrived storage for: loading:{load} - {physGunOb.GunEntity.EntityId}[{GunBase.PhysicalObject.GunEntity.EntityId}] - {physGunOb.SubtypeName}");

                        if (physGunOb.GunEntity.EntityId != GunBase.PhysicalObject.GunEntity.EntityId)
                        {
                            Log.Line($"ammoStorage skipping: {physGunOb.SubtypeName}[{physGunOb.GunEntity.EntityId}] not active entity: {GunBase.PhysicalObject.SubtypeName}[{GunBase.PhysicalObject.GunEntity.EntityId}]");
                            continue;
                        }

                        if (!load)
                        {
                            storage.CurrentAmmunition = Rifle.CurrentAmmunition;
                            storage.CurrentMagazineAmmunition = Rifle.CurrentMagazineAmmunition;
                            storage.CurrentMagazineAmount = Rifle.CurrentMagazineAmount;
                        }
                        else if (!newStorage)
                        {
                            Rifle.CurrentAmmunition = storage.CurrentAmmunition;
                            Rifle.CurrentMagazineAmmunition = storage.CurrentMagazineAmmunition;
                            PrimaryWeapon.ProtoWeaponAmmo.CurrentAmmo = storage.CurrentMagazineAmmunition;
                            Rifle.CurrentMagazineAmount = storage.CurrentMagazineAmount;
                        }

                        /*
                        var whateverThisIs = physGunOb.GunEntity as IMyObjectBuilder_GunObject<MyObjectBuilder_GunBase>;
                        if (whateverThisIs != null)
                        {
                            var gunbaseOB = whateverThisIs.GetDevice();

                            Log.Line($"{gunbaseOB.RemainingAmmo}");
                        }
                        */
                    }
                }
            }

            internal void CycleHandAmmo()
            {
                if (ConsumableSelectionPartIds.Count > 0)
                {
                    CycleAmmo();
                }
            }

            internal void HandReloadNotify(Weapon w)
            {
                if (w.Comp.Data.Repo.Values.State.PlayerId == Session.PlayerId)
                    Session.ShowLocalNotify($"Ammo type swapped to: {w.ActiveAmmoDef.AmmoName}", 1500, "White", true);
            }

            internal Matrix GetHandWeaponApproximateWorldMatrix(bool offset)
            {
                var rifleLocalMatrix = Rifle.PositionComp.LocalMatrixRef;
                rifleLocalMatrix.Translation = (CharacterPosComp.LogicalPositionWorld + (TopEntity.Physics.LinearVelocity * Projectile.StepConst));

                if (offset)
                {
                    rifleLocalMatrix.Translation += (rifleLocalMatrix.Forward * 0.25f);
                    rifleLocalMatrix.Translation += (rifleLocalMatrix.Right * 0.15f);
                }
                rifleLocalMatrix.Translation += (rifleLocalMatrix.Down * 0.25f);

                return rifleLocalMatrix;
            }

            internal MatrixD GetWhyKeenTransformedWorldMatrix()
            {
                var rifleLocalMatrix = Rifle.PositionComp.LocalMatrixRef;
                rifleLocalMatrix.Translation = (CharacterPosComp.LogicalPositionWorld + (TopEntity.Physics.LinearVelocity * Projectile.StepConst));

                rifleLocalMatrix.Translation += (rifleLocalMatrix.Forward * 0.25f);
                rifleLocalMatrix.Translation += (rifleLocalMatrix.Down * 0.25f);
                rifleLocalMatrix.Translation += (rifleLocalMatrix.Right * 0.25f);

                return rifleLocalMatrix;
            }

        }
    }
}
