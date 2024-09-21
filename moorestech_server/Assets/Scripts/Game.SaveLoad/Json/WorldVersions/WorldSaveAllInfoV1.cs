using System.Collections.Generic;
using Game.Challenge;
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
        
        public WorldSaveAllInfoV1(List<BlockJsonObject> world, List<PlayerInventorySaveJsonObject> inventory,
            List<EntityJsonObject> entities, WorldSettingJsonObject setting,
            List<MapObjectJsonObject> mapObjects, List<ChallengeJsonObject> challenge)
        {
            World = world;
            Inventory = inventory;
            Entities = entities;
            Setting = setting;
            MapObjects = mapObjects;
            Challenge = challenge;
        }
        
        [JsonProperty("world")] public List<BlockJsonObject> World { get; }
        [JsonProperty("playerInventory")] public List<PlayerInventorySaveJsonObject> Inventory { get; }
        [JsonProperty("entities")] public List<EntityJsonObject> Entities { get; }
        [JsonProperty("setting")] public WorldSettingJsonObject Setting { get; }
        [JsonProperty("mapObjects")] public List<MapObjectJsonObject> MapObjects { get; set; }
        [JsonProperty("challenge")] public List<ChallengeJsonObject> Challenge { get; set; }
    }
}