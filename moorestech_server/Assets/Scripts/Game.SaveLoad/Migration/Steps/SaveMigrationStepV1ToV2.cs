using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    /// <summary>版1のセーブを版2へ上げる。stateのJSON文字列を展開し、版2で必須になった3項目を補う</summary>
    /// <summary>Raises a version 1 save to version 2: expands the JSON-string state and backfills the three fields version 2 requires</summary>
    public sealed class SaveMigrationStepV1ToV2 : ISaveMigrationStep
    {
        // 旧セーブは乱数状態を保存しておらず、復元すべき正しい値が存在しない
        // Legacy saves stored no random state, so there is no correct value to restore
        // python版はworld.jsonのseedを使ったが、ステップはsave.jsonしか見えず種を持てない
        // The python script used world.json's seed, but a step sees only save.json and cannot know it
        // 既にプレイ済みのワールドでは乱数列の位置はいずれにせよ復元不能で、決定性さえあれば足りる
        // For an already-played world the position in the sequence is unrecoverable anyway; determinism alone suffices
        private const ulong LegacySaveRandomSeed = 0UL;

        // 失敗理由に含めるサンプル長。python移行スクリプトのvalue[:80]と揃える
        // Sample length for the failure reason; matches the python migration script's value[:80]
        private const int FailureSampleLength = 80;

        public int FromVersion => 1;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            if (!TryExpandBlockStates(out var expandedCount, out var failureReason)) return SaveMigrationStepResult.Failed(failureReason);

            var backfilled = BackfillRequiredFields();

            Debug.Log($"セーブを版1から版2へ変換しました。state展開={expandedCount}件 補填={backfilled}");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            // 版1はブロックstateをJSON文字列で保存していた。版2はオブジェクトなので1段展開する
            // Version 1 stored block state as JSON strings; version 2 holds objects, so unwrap one level
            bool TryExpandBlockStates(out int expanded, out string reason)
            {
                expanded = 0;
                reason = null;

                if (!(save["world"] is JArray world))
                {
                    reason = $"セーブのworldが配列ではないため、ブロックstateを展開できません。 type={save["world"]?.Type}";
                    return false;
                }

                foreach (var blockToken in world)
                {
                    if (!(blockToken is JObject block))
                    {
                        reason = $"world要素がオブジェクトではないため、ブロックstateを展開できません。 type={blockToken.Type}";
                        return false;
                    }

                    var stateToken = block["state"];
                    if (stateToken == null || stateToken.Type == JTokenType.Null)
                    {
                        block["state"] = new JObject();
                        continue;
                    }

                    // stateが非オブジェクト(文字列・配列等)なら変換できない。無音でとばすと未変換のまま版だけ上がる
                    // A non-object state (string, array, ...) cannot be converted; skipping silently would raise the version on an unconverted save
                    if (!(stateToken is JObject state))
                    {
                        reason = $"world要素のstateがオブジェクトではないため、展開できません。 type={stateToken.Type}";
                        return false;
                    }

                    if (!TryExpandStateValues(state, out var stateExpanded, out reason)) return false;
                    expanded += stateExpanded;
                }

                return true;
            }

            bool TryExpandStateValues(JObject state, out int expanded, out string reason)
            {
                expanded = 0;
                reason = null;

                foreach (var property in state.Properties().ToList())
                {
                    if (property.Value.Type != JTokenType.String) continue;

                    var text = property.Value.Value<string>();
                    var sample = text.Length <= FailureSampleLength ? text : text.Substring(0, FailureSampleLength);

                    // JSONでない値は版1の形として解釈できない。素通しすると未変換のまま版2が刻まれる
                    // A non-JSON value cannot be read as the version 1 shape; passing it would stamp version 2 on an unconverted save
                    // 止めれば原本は版1のまま残り、後から復号ステップを足して遡って救える
                    // Stopping keeps the original at version 1 so a later decoding step can still rescue it
                    if (!TryParseJson(text, out var parsed))
                    {
                        reason = $"state['{property.Name}']がJSONとして読めないため展開できません: {sample}";
                        return false;
                    }

                    // 二重エンコードは1回の展開では文字列のまま残り、移行済みに見えてロード時に落ちる
                    // A double-encoded value stays a string after one unwrap; it would look migrated yet break the load
                    if (parsed.Type == JTokenType.String)
                    {
                        reason = $"state['{property.Name}']が二重エンコードされています。1回の展開では文字列のままです: {sample}";
                        return false;
                    }

                    property.Value = parsed;
                    expanded++;
                }

                return true;
            }

            // 外部境界: 旧セーブの状態値は任意の外部入力で、JSONでない綴り（base64-MessagePack等）を含みうる
            // External boundary: a legacy state value is arbitrary external input and may not be JSON at all, such as base64 MessagePack
            bool TryParseJson(string text, out JToken parsed)
            {
                try
                {
                    parsed = JToken.Parse(text);
                    return true;
                }
                catch (JsonReaderException)
                {
                    parsed = null;
                    return false;
                }
            }

            // 既に値がある項目は触らない。塗り潰すと進んでいた時刻や乱数列が無音で消える
            // Leave fields that already have a value; overwriting would silently erase the tick and random sequence
            string BackfillRequiredFields()
            {
                var added = new List<string>();

                if (IsAbsent("currentTick"))
                {
                    save["currentTick"] = 0;
                    added.Add("currentTick");
                }

                if (IsAbsent("randomState"))
                {
                    save["randomState"] = JArray.FromObject(GameRandom.StateFromSeed(LegacySaveRandomSeed));
                    added.Add("randomState");
                }

                if (IsAbsent("miningCooldowns"))
                {
                    save["miningCooldowns"] = new JArray();
                    added.Add("miningCooldowns");
                }

                return added.Count == 0 ? "なし" : string.Join(",", added);
            }

            // キーが無いのと値がnullなのは、デシリアライズ後はどちらも欠損になるので同じに扱う
            // A missing key and a null value both deserialize as absent, so they are treated alike
            bool IsAbsent(string key)
            {
                var token = save[key];
                return token == null || token.Type == JTokenType.Null;
            }

            #endregion
        }
    }
}
