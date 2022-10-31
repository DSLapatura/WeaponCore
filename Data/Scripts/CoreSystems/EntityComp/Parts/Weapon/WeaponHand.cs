using CoreSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
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
            private void HandInit(IMyAutomaticRifleGun gun, out IMyAutomaticRifleGun rifle, out IMyHandheldGunObject<MyGunBase> gunBase, out MyEntity topEntity)
            {
                rifle = gun;
                gunBase = gun;
                topEntity = Rifle.Owner;
                gun.GunBase.OnAmmoAmountChanged += KeenGiveModdersSomeMoreLove;
                gun.OnMarkForClose += OnRifleMarkForClose;
            }

            private void KeenGiveModdersSomeMoreLove()
            {
                Session.FutureEvents.Schedule(ForceAmmoValues, null, 0);
            }

            private void ForceAmmoValues(object o)
            {
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

            internal void HandheldReload(Weapon w, EventTriggers state, bool active)
            {
                if (Session.HandlesInput)
                {
                    if (active && state == EventTriggers.Reloading)
                    {
                        Log.Line($"reloadActive: wAmmo:{w.ProtoWeaponAmmo.CurrentAmmo} - wcMag:{w.Reload.CurrentMags} - ammo:{Rifle.CurrentAmmunition} - magCurrentAmmo:{Rifle.CurrentMagazineAmmunition} - magAmount:{Rifle.CurrentMagazineAmount}");

                        Rifle.Reload();

                    }
                    else
                    {
                        Log.Line($"reloadInactive: wAmmo:{w.ProtoWeaponAmmo.CurrentAmmo} - wcMag:{w.Reload.CurrentMags} - ammo:{Rifle.CurrentAmmunition} - magCurrentAmmo:{Rifle.CurrentMagazineAmmunition} - magAmount:{Rifle.CurrentMagazineAmount}");
                    }
                }
            }

            internal void HandhelShoot(Weapon w, EventTriggers state, bool active)
            {
                if (Session.HandlesInput && active)
                {
                    Log.Line($"HandhelShoot: wAmmo:{w.ProtoWeaponAmmo.CurrentAmmo} - wcMag:{w.Reload.CurrentMags} - ammo:{Rifle.CurrentAmmunition} - magCurrentAmmo:{Rifle.CurrentMagazineAmmunition} - magAmount:{Rifle.CurrentMagazineAmount}");

                    Rifle.Shoot(MyShootActionEnum.PrimaryAction, Vector3D.Zero, null);

                }
            }

            internal void AmmoStorage(bool load = false)
            {

                foreach (var item in CoreInventory.GetItems())
                {

                    var physGunOb = item.Content as MyObjectBuilder_PhysicalGunObject;

                    if (physGunOb?.GunEntity != null)
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

                        if (physGunOb.GunEntity.EntityId == GunBase.PhysicalObject.GunEntity.EntityId)
                        {
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

                            Log.Line($"ammo[s:{storage.CurrentAmmunition} r:{Rifle.CurrentAmmunition} w:{PrimaryWeapon.ProtoWeaponAmmo.CurrentAmmo}] - mAmmo[s:{storage.CurrentMagazineAmmunition} r:{Rifle.CurrentMagazineAmmunition}] - mags[s:{storage.CurrentMagazineAmount} r:{Rifle.CurrentMagazineAmount} w:{PrimaryWeapon.Reload.CurrentMags}]");

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

        }
    }
}
