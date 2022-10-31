using System;
using CoreSystems.Support;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace CoreSystems
{
    public class ConstructData
    {
        internal Ai Ai;
        public ConstructDataValues Repo;


        internal void Init(Ai ai)
        {
            Ai = ai;

            StorageInit();
            Load();

            if (Ai.Session.IsServer)
            {
                Repo.FocusData = new FocusData();
            }
        }

        public void Clean()
        {
            Repo.FocusData = null;
            Repo = null;
            Ai = null;
        }

        public void StorageInit()
        {
            if (Ai.TopEntity.Storage == null)
                Ai.TopEntity.Storage = new MyModStorageComponent { [Ai.Session.ConstructDataGuid] = "" };
            else if (!Ai.TopEntity.Storage.ContainsKey(Ai.Session.ConstructDataGuid))
                Ai.TopEntity.Storage[Ai.Session.ConstructDataGuid] = "";
        }

        public void Save()
        {
            if (Ai.TopEntity.Storage == null)  return;
            var binary = MyAPIGateway.Utilities.SerializeToBinary(Repo);
            Ai.TopEntity.Storage[Ai.Session.ConstructDataGuid] = Convert.ToBase64String(binary);
        }

        public void Load()
        {
            if (Ai.TopEntity.Storage == null) return;

            ConstructDataValues load = null;
            string rawData;
            bool validData = false;

            if (Ai.TopEntity.Storage.TryGetValue(Ai.Session.ConstructDataGuid, out rawData))
            {
                try
                {
                    var base64 = Convert.FromBase64String(rawData);
                    load = MyAPIGateway.Utilities.SerializeFromBinary<ConstructDataValues>(base64);
                    validData = load != null;
                }
                catch (Exception e)
                {
                    //Log.Line("Invalid PartState Loaded, Re-init");
                }
            }
            else Log.Line("Storage didn't contain ConstructDataGuid");

            Repo = validData ? load : new ConstructDataValues();

            if (Repo.FocusData == null)
                Repo.FocusData = new FocusData();
        }
    }
}
