using System.Collections.Generic;
using Game.Entity.Interface;
using Game.Map.Interface.Json;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.SaveLoad.Json.WorldVersions
{
    public class WorldSaveAllInfoV1
    {
        [JsonProperty("worldVersion")] public int WorldVersion = 1;

        public WorldSaveAllInfoV1(List<SaveBlockData> world, List<SaveInventoryData> inventory,
            List<SaveEntityData> entities,  WorldSettingSaveData setting,
            List<SaveMapObjectData> mapObjects)
        {
            World = world;
            Inventory = inventory;
            Entities = entities;
            Setting = setting;
            MapObjects = mapObjects;
        }

        [JsonProperty("world")] public List<SaveBlockData> World { get; }
        [JsonProperty("playerInventory")] public List<SaveInventoryData> Inventory { get; }
        [JsonProperty("entities")] public List<SaveEntityData> Entities { get; }
        [JsonProperty("setting")] public WorldSettingSaveData Setting { get; }
        [JsonProperty("mapObjects")] public List<SaveMapObjectData> MapObjects { get; set; }
    }
}