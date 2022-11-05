using CoreSystems.Support;
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
                if (PrimaryWeapon.Loading || PrimaryWeapon.NoMagsToLoad || PrimaryWeapon.Reload.CurrentMags == 0)
                    return;

                Rifle.CurrentMagazineAmount = 0;
                Rifle.CurrentAmmunition = 0;

                if (!Session.IsClient)
                    PrimaryWeapon.ServerReload();
                else
                    Session.RequestToggle(this, PacketType.ForceReload);
            }

            internal void HandheldReload(Weapon w, EventTriggers state, bool active)
            {
                if (active && state == EventTriggers.Reloading)
                {
                    Log.Line($"reloadActive: wAmmo:{w.ProtoWeaponAmmo.CurrentAmmo} - wcMag:{w.Reload.CurrentMags} - ammo:{Rifle.CurrentAmmunition} - magCurrentAmmo:{Rifle.CurrentMagazineAmmunition} - magAmount:{Rifle.CurrentMagazineAmount}");

                    if (Session.IsServer)
                        Rifle.Reload();
                }
                else
                {
                    Session.FutureEvents.Schedule(ForceAmmoValues, null, 15);
                    Log.Line($"reloadInactive: wAmmo:{w.ProtoWeaponAmmo.CurrentAmmo} - wcMag:{w.Reload.CurrentMags} - ammo:{Rifle.CurrentAmmunition} - magCurrentAmmo:{Rifle.CurrentMagazineAmmunition} - magAmount:{Rifle.CurrentMagazineAmount}");
                }
            }

            internal void HandhelShoot(Weapon w, EventTriggers state, bool active)
            {
                if (active)
                {
                    Log.Line($"HandhelShoot: wAmmo:{w.ProtoWeaponAmmo.CurrentAmmo} - wcMag:{w.Reload.CurrentMags} - ammo:{Rifle.CurrentAmmunition} - magCurrentAmmo:{Rifle.CurrentMagazineAmmunition} - magAmount:{Rifle.CurrentMagazineAmount}");

                    Rifle.Shoot(MyShootActionEnum.PrimaryAction, Vector3D.Zero, null);
                    MyCharacterWeaponPositionComponent positionComponent = w.Comp.Rifle.Owner.Components.Get<MyCharacterWeaponPositionComponent>();
                }
            }

            internal void AmmoStorage(bool load = false)
            {
                foreach (var item in CoreInventory.GetItems())
                {
                    var physGunOb = item.Content as MyObjectBuilder_PhysicalGunObject;

                    if (physGunOb?.GunEntity != null && physGunOb.GunEntity.EntityId == GunBase.PhysicalObject.GunEntity.EntityId)
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

            internal void GetHandWeaponDummyInfo(out Vector3D position, out Vector3D direction, out Vector3D upDir, out Vector3D localPos)
            {
                position = CharacterPosComp.LogicalPositionWorld;
                direction = CharacterPosComp.LogicalOrientationWorld;
                upDir = TopEntity.PositionComp.WorldMatrixRef.Up;
                localPos = CharacterPosComp.LogicalPositionLocalSpace;
            }

            internal MatrixD GetWhyKeenTransformedWorldMatrix()
            {
                var childOffsetWorldMatrix = Rifle.PositionComp.WorldMatrixRef;
                var parentWorldMatrix = TopEntity.PositionComp.WorldMatrixRef;
                parentWorldMatrix.Translation = CharacterPosComp.LogicalPositionWorld;

                return parentWorldMatrix * childOffsetWorldMatrix;
            }

            internal Vector3D GetWhyKeenTransformedCenter(IMyAutomaticRifleGun childEntity, MyEntity topEntity)
            {
                return CharacterPosComp.LogicalPositionWorld;
            }


        }
    }
}
