using System;
using System.Collections.Generic;
using System.IO;
using Core.Item;
using Core.Update;
using Game.Blueprint;
using Game.Challenge;
using Game.CleanRoom;
using Game.Construction;
using Game.Context;
using Game.Entity.Interface;
using Game.Hotbar;
using Game.Map.Interface;
using Game.Map.Interface.Json;
using Game.Map.Interface.MapObject;
using Game.Paths;
using Game.PlayerInventory.Interface;
using Game.PlayerRiding.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.Research;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using UnityEngine;

namespace Game.SaveLoad.Json
{
    public class WorldLoaderFromJson : IWorldSaveDataLoader
    {
        // 新規ワールドの乱数シード。実際の乱数状態はセーブのrandomStateに載るのでここは固定でよい
        // Seed for a new world; the resulting state rides in the save's randomState, so a fixed value suffices
        private const ulong NewWorldRandomSeed = 0UL;
        
        private readonly ChallengeDatastore _challengeDatastore;
        private readonly ChallengeJsonObject _challengeJsonObject;
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IMapObjectDatastore _mapObjectDatastore;
        private readonly MapInfoJson _mapInfoJson;
        
        private readonly WorldDataDirectory _worldDataDirectory;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        private readonly IResearchDataStore _researchDataStore;
        private readonly TrainSaveLoadService _trainSaveLoadService;
        private readonly RailGraphSaveLoadService _railGraphSaveLoadService;
        private readonly TrainDockingStateRestorer _trainDockingStateRestorer;
        private readonly IPlayerRidingDatastore _playerRidingDatastore;
        private readonly IBlueprintDatastore _blueprintDatastore;
        private readonly HotbarAssignmentDatastore _hotbarAssignmentDatastore;
        private readonly RemainingPlacementCountDataStore _remainingPlacementCountDataStore;
        private readonly ConstructionPayerDataStore _constructionPayerDataStore;
        private readonly ItemStackLevelDataStore _itemStackLevelDataStore;
        private readonly IPlayerInventorySlotLevelDataStore _playerInventorySlotLevelDataStore;
        private readonly CleanRoomDatastore _cleanRoomDatastore;
        private readonly IMiningCooldownDatastore _miningCooldownDatastore;
        private readonly SaveLoadPreparer _saveLoadPreparer;

        public WorldLoaderFromJson(WorldDataDirectory worldDataDirectory,
            IPlayerInventoryDataStore inventoryDataStore, IEntitiesDatastore entitiesDatastore, IWorldSettingsDatastore worldSettingsDatastore,
            ChallengeDatastore challengeDatastore, IGameUnlockStateDataController gameUnlockStateDataController, MapInfoJson mapInfoJson,
            IResearchDataStore researchDataStore, TrainSaveLoadService trainSaveLoadService, RailGraphSaveLoadService railGraphSaveLoadService, TrainDockingStateRestorer trainDockingStateRestorer,
            IPlayerRidingDatastore playerRidingDatastore, IBlueprintDatastore blueprintDatastore, HotbarAssignmentDatastore hotbarAssignmentDatastore, RemainingPlacementCountDataStore remainingPlacementCountDataStore, ConstructionPayerDataStore constructionPayerDataStore, ItemStackLevelDataStore itemStackLevelDataStore,
            IPlayerInventorySlotLevelDataStore playerInventorySlotLevelDataStore, CleanRoomDatastore cleanRoomDatastore, IMiningCooldownDatastore miningCooldownDatastore,
            SaveLoadPreparer saveLoadPreparer)
        {
            _worldBlockDatastore = ServerContext.WorldBlockDatastore;
            _mapObjectDatastore = ServerContext.MapObjectDatastore;

            _worldDataDirectory = worldDataDirectory;
            _inventoryDataStore = inventoryDataStore;
            _entitiesDatastore = entitiesDatastore;
            _worldSettingsDatastore = worldSettingsDatastore;
            _challengeDatastore = challengeDatastore;
            _gameUnlockStateDataController = gameUnlockStateDataController;
            _mapInfoJson = mapInfoJson;
            _researchDataStore = researchDataStore;
            _trainSaveLoadService = trainSaveLoadService;
            _railGraphSaveLoadService = railGraphSaveLoadService;
            _trainDockingStateRestorer = trainDockingStateRestorer;
            _playerRidingDatastore = playerRidingDatastore;
            _blueprintDatastore = blueprintDatastore;
            _hotbarAssignmentDatastore = hotbarAssignmentDatastore;
            _remainingPlacementCountDataStore = remainingPlacementCountDataStore;
            _constructionPayerDataStore = constructionPayerDataStore;
            _itemStackLevelDataStore = itemStackLevelDataStore;
            _playerInventorySlotLevelDataStore = playerInventorySlotLevelDataStore;
            _cleanRoomDatastore = cleanRoomDatastore;
            _miningCooldownDatastore = miningCooldownDatastore;
            _saveLoadPreparer = saveLoadPreparer;
        }
        
        public void LoadOrInitialize()
        {
            if (File.Exists(_worldDataDirectory.SaveJsonFilePath))
            {
                var json = File.ReadAllText(_worldDataDirectory.SaveJsonFilePath);

                // 版の変換とマスタ欠損の除去はロードの前段で終わらせる。Loadは整った形だけを受ける
                // Version migration and missing-master pruning finish before load; Load only ever sees a prepared shape
                var prepared = _saveLoadPreparer.Prepare(json);
                if (!prepared.CanLoad)
                {
                    Debug.LogError($"セーブファイルパス {_worldDataDirectory.SaveJsonFilePath}");
                    throw new Exception($"セーブファイルをロードできないため起動を中断しました。\n Cause : {prepared.BlockedCause} \n Reason : {prepared.BlockedReason}");
                }

                try
                {
                    Load(prepared.SaveJsonText);
                    Debug.Log("セーブデータのロードが完了しました。");
                    return;
                }
                catch (Exception e)
                {
                    //TODO ログ基盤
                    Debug.Log("セーブデータが破損していたか古いバージョンでした。削除したら治る可能性があります。\nサポートが必要な場合はDiscordサーバー ( https://discord.gg/ekFYmY3rDP ) にて連絡をお願いします。");
                    Debug.Log($"セーブファイルパス {_worldDataDirectory.SaveJsonFilePath}");
                    throw new Exception(
                        $"セーブファイルのロードに失敗しました。セーブファイルを確認してください。\n Message : {e.Message} \n StackTrace : {e.StackTrace}");
                }
            }
            
            Debug.Log("セーブデータがありませんでした。新規作成します。");
            WorldInitialize();
        }
        
        public void Load(string jsonText)
        {
            var load = JsonConvert.DeserializeObject<WorldSaveAllInfoV1>(jsonText);

            // 版が古いセーブの欠損はV1→V2ステップが補うので、ここで補填はしない。欠けたまま進むと理由の無い素の例外になる
            // An older save's gaps are backfilled by the V1-to-V2 step, not here; passing one through would fail with a reasonless bare exception
            ThrowIfRequiredFieldMissing();

            // 時刻と乱数状態を最初に戻す。以降の復元（残りtick等）がこの時刻を基準にする
            // Restore the clock and random state first; later restorations reference this tick
            GameUpdater.RestoreCurrentTick(load.CurrentTick.Value);
            GameRandom.RestoreState(load.RandomState);
            
            _gameUnlockStateDataController.LoadUnlockState(load.GameUnlockStateJsonObject);
            // ブロック・インベントリ復元前にスタックレベルを復元する（上限超過例外の防止）
            // Restore stack levels before blocks/inventories to avoid over-limit exceptions
            _itemStackLevelDataStore.LoadUnlockedLevels(load.ItemStackLevels);
            // スロットレベルはプレイヤーインベントリより先にロードする
            // Load the slot level before player inventories
            _playerInventorySlotLevelDataStore.LoadLevel(load.InventorySlotLevel);
            _worldBlockDatastore.LoadBlockDataList(load.World);
            // クリーンルームを再検出し、セーブされた純度と行を照合復元する
            // Re-detect clean rooms then restore saved purity and class rows by matching
            _cleanRoomDatastore.RebuildAll();
            _cleanRoomDatastore.Restore(load.CleanRoomRooms);
            // レールセグメントを復元する
            // Restore rail segments
            var segments = load.RailSegments ?? new List<RailSegmentSaveData>();
            _railGraphSaveLoadService.RestoreRailSegments(segments);
            _inventoryDataStore.LoadPlayerInventory(load.Inventory);
            _entitiesDatastore.LoadBlockDataList(load.Entities);
            _worldSettingsDatastore.LoadSettingData(load.Setting);
            _mapObjectDatastore.LoadMapObject(load.MapObjects);
            _researchDataStore.LoadResearchData(load.Research ?? new ResearchSaveJsonObject());
            
            // Challengeがnullまたはリストがnullでないことを確認
            if (load.Challenge == null)
            {
                load.Challenge = new ChallengeJsonObject
                {
                    CompletedGuids = new System.Collections.Generic.List<string>(),
                    CurrentChallengeGuids = new System.Collections.Generic.List<string>(),
                    PlayedSkitIds = new System.Collections.Generic.List<string>()
                };
            }
            else
            {
                if (load.Challenge.CompletedGuids == null)
                    load.Challenge.CompletedGuids = new System.Collections.Generic.List<string>();
                if (load.Challenge.CurrentChallengeGuids == null)
                    load.Challenge.CurrentChallengeGuids = new System.Collections.Generic.List<string>();
                if (load.Challenge.PlayedSkitIds == null)
                    load.Challenge.PlayedSkitIds = new System.Collections.Generic.List<string>();
            }
            
            _challengeDatastore.LoadChallenge(load.Challenge);

            _trainSaveLoadService.RestoreTrainStates(load.TrainUnits);
            _trainDockingStateRestorer.RestoreDockingState();

            _playerRidingDatastore.LoadSaveData(load.PlayerRidingStates);

            _blueprintDatastore.LoadBlueprints(load.Blueprints);

            // 割当検証がBP一覧を参照するため、BPロード後にホットバーを復元する
            // Hotbar restore must follow blueprint load since assignment validation reads the BP list
            _hotbarAssignmentDatastore.LoadHotbar(load.HotbarAssignments);

            // 残り設置数はマスタだけに依存するためロード順の制約なし
            // Remaining placements depend only on the master, so there is no load-order constraint
            _remainingPlacementCountDataStore.LoadRemainingCounts(load.RemainingPlacementCounts);

            // 課金元プレイヤーはブロックインスタンスIDで持つためワールドのロード順に依存しない
            // The paying player is keyed by block instance id, so it does not depend on the world load order
            _constructionPayerDataStore.LoadPayers(load.ConstructionPayers);

            // 採掘クールダウンは保存時のtick基準で効く。戻さないとロード直後の再生が保存前に拒否された採掘を通す
            // The mining cooldown is measured from the saved tick; skipping it lets a replay accept mining the live world rejected
            _miningCooldownDatastore.LoadMiningCooldowns(load.MiningCooldowns);
            
            // 復元は乱数を引かない（ID類はセーブから引き継ぐ）。引いていたら巻き戻して隠さず、ずれた事実を出す
            // Restoration draws no randomness because ids come from the save; a drift is reported instead of being hidden by another rewind
            WarnIfRandomStreamAdvanced(load.RandomState);

            #region Internal

            void ThrowIfRequiredFieldMissing()
            {
                var missing = new List<string>();
                if (!load.CurrentTick.HasValue) missing.Add("currentTick");
                if (load.RandomState == null) missing.Add("randomState");
                if (load.MiningCooldowns == null) missing.Add("miningCooldowns");
                if (missing.Count == 0) return;

                var reason = $"セーブに {string.Join(" / ", missing)} がありません。版が古いセーブはマイグレーション連鎖（Game.SaveLoad/Migration）が補填するため、現在版のセーブで欠けているのは手編集による破損です";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }

            void WarnIfRandomStreamAdvanced(ulong[] savedState)
            {
                var currentState = GameRandom.ExportState();
                for (var i = 0; i < savedState.Length; i++)
                {
                    if (savedState[i] == currentState[i]) continue;

                    // 巻き戻すと、ロード後に採番されるIDがセーブ時と衝突する。ここは直さず気付けるようにする
                    // Rewinding would make ids allocated after load collide with the saved ones, so this is surfaced rather than patched
                    Debug.LogError($"ロード中に乱数列が進みました saved:[{string.Join(",", savedState)}] current:[{string.Join(",", currentState)}]。復元経路のID採番を疑ってください");
                    return;
                }
            }

            #endregion
        }
        
        public void WorldInitialize()
        {
            // 同一プロセスで前のワールドを動かした後でも、新規ワールドは常に同じ時刻と乱数列から始める
            // A new world always starts from the same clock and random stream, even after another world ran in this process
            GameUpdater.RestoreCurrentTick(0);
            GameRandom.Reseed(NewWorldRandomSeed);
            Debug.Log($"新規ワールドの時刻と乱数を初期化しました tick:0 seed:{NewWorldRandomSeed}");
            
            _worldSettingsDatastore.Initialize(_mapInfoJson);
            _challengeDatastore.InitializeCurrentChallenges();
        }
    }
}
