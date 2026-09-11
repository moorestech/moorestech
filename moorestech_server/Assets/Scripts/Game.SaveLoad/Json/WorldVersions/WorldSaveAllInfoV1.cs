using System.Collections.Generic;
using Game.Blueprint;
using Game.Challenge;
using Game.CleanRoom.Save;
using Game.Construction;
using Game.Entity.Interface;
using Game.Hotbar;
using Game.Map.Interface.Json;
using Game.PlayerInventory.Interface;
using Game.PlayerRiding.Interface;
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
            ResearchSaveJsonObject research,
            List<TrainUnitSaveData> trainUnits,
            List<RailSegmentSaveData> railSegments,
            List<PlayerRidingSaveData> playerRidingStates,
            List<BlueprintJsonObject> blueprints,
            List<PlayerHotbarSaveJsonObject> hotbarAssignments,
            List<PlayerRemainingPlacementCountSaveJsonObject> remainingPlacementCounts,
            List<ConstructionPayerSaveJsonObject> constructionPayers,
            Dictionary<string, int> itemStackLevels,
            int inventorySlotLevel,
            List<CleanRoomSaveData> cleanRoomRooms,
            ulong[] randomState)
        {
            World = world;
            Inventory = inventory;
            Entities = entities;
            Setting = setting;
            MapObjects = mapObjects;
            Challenge = challenge;
            GameUnlockStateJsonObject = gameUnlockStateJsonObject;
            Research = research;
            TrainUnits = trainUnits ?? new List<TrainUnitSaveData>();
            RailSegments = railSegments ?? new List<RailSegmentSaveData>();
            PlayerRidingStates = playerRidingStates ?? new List<PlayerRidingSaveData>();
            Blueprints = blueprints ?? new List<BlueprintJsonObject>();
            HotbarAssignments = hotbarAssignments ?? new List<PlayerHotbarSaveJsonObject>();
            RemainingPlacementCounts = remainingPlacementCounts ?? new List<PlayerRemainingPlacementCountSaveJsonObject>();
            ConstructionPayers = constructionPayers ?? new List<ConstructionPayerSaveJsonObject>();
            ItemStackLevels = itemStackLevels ?? new Dictionary<string, int>();
            InventorySlotLevel = inventorySlotLevel;
            CleanRoomRooms = cleanRoomRooms ?? new List<CleanRoomSaveData>();
            RandomState = randomState;
        }
        
        [JsonProperty("world")] public List<BlockJsonObject> World { get; }
        [JsonProperty("playerInventory")] public List<PlayerInventorySaveJsonObject> Inventory { get; }
        [JsonProperty("entities")] public List<EntityJsonObject> Entities { get; }
        [JsonProperty("setting")] public WorldSettingJsonObject Setting { get; }
        [JsonProperty("mapObjects")] public List<MapObjectJsonObject> MapObjects { get; set; }
        [JsonProperty("challenge")] public ChallengeJsonObject Challenge { get; set; }
        [JsonProperty("gameUnlockState")] public GameUnlockStateJsonObject GameUnlockStateJsonObject { get; set; }
        [JsonProperty("currentlyActiveChallenge")] public ChallengeJsonObject CurrentlyActiveChallenge { get; set; }
        [JsonProperty("research")] public ResearchSaveJsonObject Research { get; }
        [JsonProperty("trainUnits")] public List<TrainUnitSaveData> TrainUnits { get; }
        [JsonProperty("railSegments")] public List<RailSegmentSaveData> RailSegments { get; }
        [JsonProperty("playerRidingStates")] public List<PlayerRidingSaveData> PlayerRidingStates { get; }
        [JsonProperty("blueprints")] public List<BlueprintJsonObject> Blueprints { get; set; }
        [JsonProperty("hotbarAssignments")] public List<PlayerHotbarSaveJsonObject> HotbarAssignments { get; set; }
        [JsonProperty("remainingPlacementCounts")] public List<PlayerRemainingPlacementCountSaveJsonObject> RemainingPlacementCounts { get; set; }
        [JsonProperty("constructionPayers")] public List<ConstructionPayerSaveJsonObject> ConstructionPayers { get; set; }
        [JsonProperty("itemStackLevels")] public Dictionary<string, int> ItemStackLevels { get; }
        [JsonProperty("inventorySlotLevel")] public int InventorySlotLevel { get; }
        [JsonProperty("cleanRoomRooms")] public List<CleanRoomSaveData> CleanRoomRooms { get; }

        // スナップショットからの再生に必要な時刻と乱数状態。ロードの先頭で復元する
        // Tick and random state required to replay from a snapshot; restored first on load
        // 値型のままだと欠損が既定の0として成立し、tickが無音で巻き戻る。欠損を型で見分けるためnull許容にする
        // As a value type a missing field would pass as the default 0 and silently rewind the clock, so it is nullable to make absence visible
        // ctor引数にすると欠損時に Newtonsoft が 0 を詰めて HasValue が true になるため、代入で受ける
        // As a constructor parameter Newtonsoft fills a missing value with 0 and HasValue becomes true, so it is assigned instead
        [JsonProperty("currentTick")] public ulong? CurrentTick { get; set; }
        [JsonProperty("randomState")] public ulong[] RandomState { get; }
    }
}
