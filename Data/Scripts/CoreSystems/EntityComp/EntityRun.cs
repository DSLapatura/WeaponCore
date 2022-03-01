using System;
using CoreSystems.Platform;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;
using static CoreSystems.Session;
using static CoreSystems.Support.Ai;

namespace CoreSystems.Support
{
    public partial class CoreComponent : MyEntityComponentBase
    {
        public override void OnAddedToContainer()
        {
            try
            {
                ++SceneVersion;
                base.OnAddedToContainer();
                if (Container.Entity.InScene) {

                    LastAddToScene = Session.Tick;
                    if (Platform.State == CorePlatform.PlatformState.Fresh)
                        PlatformInit();
                }
                else 
                    Log.Line($"Tried to add comp but it was already scene - {Platform.State} - AiNull:{Ai == null} ");
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToContainer: {ex}", null, true); }
        }

        public override void OnAddedToScene()
        {
            try
            {
                ++SceneVersion;
                base.OnAddedToScene();

                if (Platform.State == CorePlatform.PlatformState.Inited || Platform.State == CorePlatform.PlatformState.Ready)
                    ReInit();
                else {

                    if (Platform.State == CorePlatform.PlatformState.Delay)
                        return;
                    
                    if (Platform.State != CorePlatform.PlatformState.Fresh)
                        Log.Line($"OnAddedToScene != Fresh, Inited or Ready: {Platform.State}");

                    PlatformInit();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}", null, true); }
        }
        
        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
        }

        internal void PlatformInit()
        {
            switch (Platform.Init()) {

                case CorePlatform.PlatformState.Invalid:
                    Platform.PlatformCrash(this, false, false, $"Platform PreInit is in an invalid state: {SubtypeName}");
                    break;
                case CorePlatform.PlatformState.Valid:
                    Platform.PlatformCrash(this, false, true, $"Something went wrong with Platform PreInit: {SubtypeName}");
                    break;
                case CorePlatform.PlatformState.Delay:
                    Session.CompsDelayedInit.Add(this);
                    break;
                case CorePlatform.PlatformState.Inited:
                    Init();
                    break;
            }
        }

        internal void Init()
        {
            using (CoreEntity.Pin()) 
            {
                if (!CoreEntity.MarkedForClose && Entity != null) 
                {
                    Ai.FirstRun = true;

                    StorageSetup();

                    if (TypeSpecific != CompTypeSpecific.Phantom && TypeSpecific != CompTypeSpecific.Control) {
                        InventoryInit();

                        if (IsBlock)
                            PowerInit();
                    }

                    Entity.NeedsWorldMatrix = NeedsWorldMatrix;
                    WorldMatrixEnabled = NeedsWorldMatrix;

                    if (IsBlock && !Ai.Session.GridToInfoMap.ContainsKey(Ai.TopEntity))
                    {
                        Log.Line($"WeaponComp Init did not have GridToInfoMap");
                        Session.CompReAdds.Add(new CompReAdd { Ai = Ai, AiVersion = Ai.Version, AddTick = Ai.Session.Tick, Comp = this });
                    }
                    else OnAddedToSceneTasks(true);

                    Platform.State = CorePlatform.PlatformState.Ready;



                } 
                else Log.Line("BaseComp Init() failed");
            }
        }

        internal void ReInit(bool checkMap = true)
        {
            using (CoreEntity.Pin())  {

                if (!CoreEntity.MarkedForClose && Entity != null)  {

                    if (IsBlock)
                    {
                        TopEntity = GetTopEntity();
                        GridMap gridMap;
                        if (checkMap && (!Session.GridToInfoMap.TryGetValue(TopEntity, out gridMap) || gridMap.GroupMap == null)) {
                            
                            if (!InReInit)
                                Session.CompsDelayedReInit.Add(this);
                            
                            Session.ReInitTick = Session.Tick;
                            InReInit = true;
                            return;
                        }

                        if (InReInit)
                            RemoveFromReInit();
                    }

                    Ai ai;
                    if (!Session.EntityAIs.TryGetValue(TopEntity, out ai)) {

                        var newAi = Session.AiPool.Count > 0 ? Session.AiPool.Pop() : new Ai(Session);
                        newAi.Init(TopEntity, Session, TypeSpecific);
                        Session.EntityAIs[TopEntity] = newAi;
                        Ai = newAi;
                    }
                    else {
                        Ai = ai;
                    }

                    if (Ai != null) {

                        Ai.FirstRun = true;

                        if (Type == CompType.Weapon && Platform.State == CorePlatform.PlatformState.Inited)
                            Platform.ResetParts();

                        Entity.NeedsWorldMatrix = NeedsWorldMatrix; 
                        WorldMatrixEnabled = NeedsWorldMatrix;

                        // ReInit Counters
                        if (!Ai.PartCounting.ContainsKey(SubTypeId)) // Need to account for reinit case
                            Ai.PartCounting[SubTypeId] = Session.PartCountPool.Get();

                        var pCounter = Ai.PartCounting[SubTypeId];
                        pCounter.Max = Platform.Structure.ConstructPartCap;

                        pCounter.Current++;
                        Constructs.BuildAiListAndCounters(Ai);
                        // end ReInit

                        OnAddedToSceneTasks(false);
                    }
                    else {
                        Log.Line("BaseComp ReInit() failed stage2!");
                    }
                }
                else {
                    Log.Line($"BaseComp ReInit() failed stage1! - marked:{CoreEntity.MarkedForClose} - Entity:{Entity != null} - hasAi:{Session.EntityAIs.ContainsKey(TopEntity)}");
                }
            }
        }

        internal void OnAddedToSceneTasks(bool firstRun)
        {
            try {

                if (Ai.MarkedForClose || CoreEntity.MarkedForClose)
                    Log.Line($"OnAddedToSceneTasks and AI/CoreEntity MarkedForClose - Subtype:{SubtypeName} - grid:{TopEntity.DebugName} - CubeMarked:{CoreEntity.MarkedForClose} - CubeClosed:{CoreEntity.Closed} - CubeInScene:{CoreEntity.InScene} - GridMarked:{TopEntity.MarkedForClose}({CoreEntity.GetTopMostParent()?.MarkedForClose ?? true}) - GridMatch:{TopEntity == Ai.TopEntity} - AiContainsMe:{Ai.CompBase.ContainsKey(CoreEntity)} - MyGridInAi:{Ai.Session.EntityToMasterAi.ContainsKey(TopEntity)}[{Ai.Session.EntityAIs.ContainsKey(TopEntity)}]");
                
                Ai.UpdatePowerSources = true;
                RegisterEvents();
                if (!Ai.AiInit) {

                    Ai.AiInit = true;
                    if (IsBlock)
                    {
                        var fatList = Session.GridToInfoMap[TopEntity].MyCubeBocks;

                        for (int i = 0; i < fatList.Count; i++)
                        {

                            var cubeBlock = fatList[i];
                            var stator = cubeBlock as IMyMotorStator;
                            var tool = cubeBlock as IMyShipToolBase;

                            if (cubeBlock is MyBatteryBlock || cubeBlock.HasInventory || stator != null || tool != null)
                                Ai.FatBlockAdded(cubeBlock);
                        }
                        var bigOwners = Ai.GridEntity.BigOwners;
                        Ai.AiOwner = bigOwners.Count > 0 ? bigOwners[0] : 0;
                    }
                }

                if (Type == CompType.Control)
                {
                    var cComp = ((ControlSys.ControlComponent) this);
                    if (cComp.Platform.Control.TrackingWeapon != null) {
                        cComp.Platform.Control.TrackingWeapon.MasterComp = null;
                        cComp.Platform.Control.TrackingWeapon.RotorTurretTracking = false;
                    }
                }

                if (Type == CompType.Weapon)
                    ((Weapon.WeaponComponent)this).OnAddedToSceneWeaponTasks(firstRun);


                Ai.CompBase[CoreEntity] = this;

                Ai.CompChange(true, this);

                Ai.IsStatic = Ai.TopEntity.Physics?.IsStatic ?? false;

                if (IsBlock)
                {
                    if (Platform.Weapons.Count > 0)
                    {
                        MyOrientedBoundingBoxD obb;
                        SUtils.GetBlockOrientedBoundingBox(Cube, out obb);
                        foreach (var weapon in Platform.Weapons)
                        {
                            var scopeInfo = weapon.GetScope.Info;
                            if (!obb.Contains(ref scopeInfo.Position))
                            {
                                var rayBack = new RayD(scopeInfo.Position, -scopeInfo.Direction);
                                weapon.ScopeDistToCheckPos = obb.Intersects(ref rayBack) ?? 0;
                            }
                            Session.FutureEvents.Schedule(weapon.DelayedStart, FunctionalBlock.Enabled, 1);
                        }
                    }

                    if (Ai.AiSpawnTick > Ai.Construct.LastRefreshTick || Ai.Construct.LastRefreshTick == 0)
                        Ai.GridMap.GroupMap.UpdateAis();
                }
                Status = !IsWorking ? Start.Starting : Start.ReInit;
            }

            catch (Exception ex) { Log.Line($"Exception in OnAddedToSceneTasks: {ex} AiNull:{Ai == null} - SessionNull:{Session == null} EntNull{Entity == null} MyCubeNull:{TopEntity == null}", null, true); }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                ++SceneVersion;
                base.OnRemovedFromScene();
                RemoveComp();
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}", null, true); }
        }

        public override bool IsSerialized()
        {
            if (Platform.State == CorePlatform.PlatformState.Ready) {

                if (CoreEntity?.Storage != null) {
                    BaseData.Save();
                }
            }
            return false;
        }

        public override string ComponentTypeDebugString => "CoreSystems";
    }
}
