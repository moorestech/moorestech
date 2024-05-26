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

        [JsonProperty("world")] public List<BlockJsonObject> World { get; }
        [JsonProperty("playerInventory")] public List<PlayerInventoryJsonObject> Inventory { get; }
        [JsonProperty("entities")] public List<EntityJsonObject> Entities { get; }
        [JsonProperty("setting")] public WorldSettingJsonObject Setting { get; }
        [JsonProperty("mapObjects")] public List<MapObjectJsonObject> MapObjects { get; set; }

        public WorldSaveAllInfoV1(List<BlockJsonObject> world, List<PlayerInventoryJsonObject> inventory,
            List<EntityJsonObject> entities, WorldSettingJsonObject setting,
            List<MapObjectJsonObject> mapObjects)
        {
            World = world;
            Inventory = inventory;
            Entities = entities;
            Setting = setting;
            MapObjects = mapObjects;
        }
    }
}