using System;
using System.IO;
using Core.Update;
using Game.Challenge;
using Game.Map.Interface.Json;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.World.Interface.DataStore;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Json
{
    public class WorldLoaderFromJson : IWorldSaveDataLoader
    {
        // 新規ワールドの乱数シード。実際の乱数状態はセーブのrandomStateに載るのでここは固定でよい
        // Seed for a new world; the resulting state rides in the save's randomState, so a fixed value suffices
        private const ulong NewWorldRandomSeed = 0UL;

        private readonly ChallengeDatastore _challengeDatastore;
        private readonly MapInfoJson _mapInfoJson;
        private readonly WorldDataDirectory _worldDataDirectory;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;
        private readonly SaveLoadPreparer _saveLoadPreparer;
        private readonly WorldSaveDataRestorer _worldSaveDataRestorer;
        private readonly SaveBackfilledFieldsRecord _saveBackfilledFieldsRecord;

        public WorldLoaderFromJson(WorldDataDirectory worldDataDirectory, IWorldSettingsDatastore worldSettingsDatastore, ChallengeDatastore challengeDatastore,
            MapInfoJson mapInfoJson, SaveLoadPreparer saveLoadPreparer, WorldSaveDataRestorer worldSaveDataRestorer, SaveBackfilledFieldsRecord saveBackfilledFieldsRecord)
        {
            _worldDataDirectory = worldDataDirectory;
            _worldSettingsDatastore = worldSettingsDatastore;
            _challengeDatastore = challengeDatastore;
            _mapInfoJson = mapInfoJson;
            _saveLoadPreparer = saveLoadPreparer;
            _worldSaveDataRestorer = worldSaveDataRestorer;
            _saveBackfilledFieldsRecord = saveBackfilledFieldsRecord;
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
                    // 整えた木をそのまま渡す。文字列へ戻すと再シリアライズと再パースが丸ごと無駄になる
                    // Hand over the prepared tree as is; turning it back into text would waste a full serialize and re-parse
                    Load(prepared.Save);
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
            Load(SaveJsonObjectReader.Read(jsonText));
        }

        public void Load(JObject save)
        {
            _worldSaveDataRestorer.Restore(save.ToObject<WorldSaveAllInfo>());
        }

        public void WorldInitialize()
        {
            // 同一プロセスで前のワールドを動かした後でも、新規ワールドは常に同じ時刻と乱数列から始める
            // A new world always starts from the same clock and random stream, even after another world ran in this process
            GameUpdater.RestoreCurrentTick(0);
            GameRandom.Reseed(NewWorldRandomSeed);
            Debug.Log($"新規ワールドの時刻と乱数を初期化しました tick:0 seed:{NewWorldRandomSeed}");

            // 新規ワールドは何も補填していない。前のワールドの一覧を持ち越すと実値を捏造扱いにしてしまう
            // A new world has backfilled nothing; carrying over the previous world's list would mark real values as fabricated
            _saveBackfilledFieldsRecord.SetFields(Array.Empty<string>());

            _worldSettingsDatastore.Initialize(_mapInfoJson);
            _challengeDatastore.InitializeCurrentChallenges();
        }
    }
}
