using System.Collections.Generic;
using Game.Entity.Interface;
using Game.MapObject.Interface.Json;
using Game.PlayerInventory.Interface;
using Game.Quest.Interface;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.Save.Json.WorldVersions
{
    public class WorldSaveAllInfoV1
    {
        [JsonProperty("worldVersion")] public int WorldVersion = 1;

        public WorldSaveAllInfoV1(List<SaveBlockData> world, List<SaveInventoryData> inventory,
            List<SaveEntityData> entities, Dictionary<int, List<SaveQuestData>> quests, WorldSettingSaveData setting,
            List<SaveMapObjectData> mapObjects)
        {
            World = world;
            Inventory = inventory;
            Entities = entities;
            Quests = quests;
            Setting = setting;
            MapObjects = mapObjects;
        }

        [JsonProperty("world")] public List<SaveBlockData> World { get; }
        [JsonProperty("playerInventory")] public List<SaveInventoryData> Inventory { get; }
        [JsonProperty("entities")] public List<SaveEntityData> Entities { get; }
        [JsonProperty("quests")] public Dictionary<int, List<SaveQuestData>> Quests { get; }
        [JsonProperty("setting")] public WorldSettingSaveData Setting { get; }
        [JsonProperty("mapObjects")] public List<SaveMapObjectData> MapObjects { get; set; }
    }
}