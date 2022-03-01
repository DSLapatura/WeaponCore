using System;
using System.IO;
using CoreSystems.Support;
using Sandbox.ModAPI;
using VRage.Game.Components;
namespace Scripts
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate, int.MaxValue)]
    public class Session : MySessionComponentBase
    {
        public override void LoadData()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(7772, Handler);
            Init();
            SendModMessage(true);
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(7772, Handler);
            Array.Clear(Storage, 0, Storage.Length);
            Storage = null;
        }

        void Handler(object o)
        {
            if (o == null) SendModMessage(false);
        }

        void SendModMessage(bool sending)
        {
            MyAPIGateway.Utilities.SendModMessage(7771, Storage);
        }

        internal byte[] Storage;

        internal void Init()
        {
            ContainerDefinition baseDefs;
            Parts.GetBaseDefinitions(out baseDefs);
            Parts.SetModPath(baseDefs, ModContext.ModPath);
            Storage = MyAPIGateway.Utilities.SerializeToBinary(baseDefs);
        }
    }
}

