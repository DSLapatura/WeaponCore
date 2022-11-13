using System;
using VRage.Game.Components;

namespace CoreSystems.Support
{
    public partial class AiComponent : MyEntityComponentBase
    {
        public override void OnAddedToContainer()
        {
            try {

                base.OnAddedToContainer();
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToContainer: {ex}"); }
        }

        public override void OnAddedToScene()
        {
            try
            {
                base.OnAddedToScene();
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }
        
        public override void OnBeforeRemovedFromContainer()
        {
            base.OnBeforeRemovedFromContainer();
        }


        internal void OnAddedToSceneTasks()
        {
            try {

            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToSceneTasks: {ex}"); }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                base.OnRemovedFromScene();
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (Ai.Session != null && Ai.Session.IsServer)
            {
                Ai.Data.Save();
                Ai.Construct.Data.Save();
            }
            return false;
        }

        public override string ComponentTypeDebugString => "CoreSystems";
    }
}
