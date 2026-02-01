using System.Collections.Generic;
using Game.Challenge;
using Game.CraftTree;
using Game.CraftTree.Json;
using Game.Entity.Interface;
using Game.Map.Interface.Json;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;

namespace Game.SaveLoad.Json.WorldVersions
{
    public class WorldSaveAllInfoV1
    {
        [JsonProperty("worldVersion")] public int WorldVersion = 1;
        
        public WorldSaveAllInfoV1(
            List<BlockJsonObject> world, 
            List<PlayerInventorySaveJsonObject> inventory,
            List<EntityJsonObject> entities, 
            WorldSettingJsonObject setting,
            List<MapObjectJsonObject> mapObjects,
            ChallengeJsonObject challenge,
            GameUnlockStateJsonObject gameUnlockStateJsonObject,
            List<PlayerCraftTreeJsonObject> craftTreeInfo,
            ResearchSaveJsonObject research,
            List<TrainUnitSaveData> trainUnits,
            List<RailSegmentSaveData> railSegments)
        {
            World = world;
            Inventory = inventory;
            Entities = entities;
            Setting = setting;
            MapObjects = mapObjects;
            Challenge = challenge;
            GameUnlockStateJsonObject = gameUnlockStateJsonObject;
            CraftTreeInfo = craftTreeInfo;
            Research = research;
            TrainUnits = trainUnits ?? new List<TrainUnitSaveData>();
            RailSegments = railSegments ?? new List<RailSegmentSaveData>();
        }
        
        [JsonProperty("world")] public List<BlockJsonObject> World { get; }
        [JsonProperty("playerInventory")] public List<PlayerInventorySaveJsonObject> Inventory { get; }
        [JsonProperty("entities")] public List<EntityJsonObject> Entities { get; }
        [JsonProperty("setting")] public WorldSettingJsonObject Setting { get; }
        [JsonProperty("mapObjects")] public List<MapObjectJsonObject> MapObjects { get; set; }
        [JsonProperty("challenge")] public ChallengeJsonObject Challenge { get; set; }
        [JsonProperty("gameUnlockState")] public GameUnlockStateJsonObject GameUnlockStateJsonObject { get; set; }
        [JsonProperty("craftTreeInfo")] public List<PlayerCraftTreeJsonObject> CraftTreeInfo { get; set; }
        [JsonProperty("currentlyActiveChallenge")] public ChallengeJsonObject CurrentlyActiveChallenge { get; set; }
        [JsonProperty("research")] public ResearchSaveJsonObject Research { get; }
        [JsonProperty("trainUnits")] public List<TrainUnitSaveData> TrainUnits { get; }
        [JsonProperty("railSegments")] public List<RailSegmentSaveData> RailSegments { get; }
    }
}
