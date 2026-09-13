using System;
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

        // ログに含めるサンプル長。python移行スクリプトのvalue[:80]と揃える
        // Sample length for the log; matches the python migration script's value[:80]
        private const int DoubleEncodedSampleLength = 80;

        public int FromVersion => 1;

        public JObject Migrate(JObject save)
        {
            var expandedCount = ExpandBlockStates();
            var backfilled = BackfillRequiredFields();

            Debug.Log($"セーブを版1から版2へ変換しました。state展開={expandedCount}件 補填={backfilled}");
            return save;

            #region Internal

            // 版1はブロックstateをJSON文字列で保存していた。版2はオブジェクトなので1段展開する
            // Version 1 stored block state as JSON strings; version 2 holds objects, so unwrap one level
            int ExpandBlockStates()
            {
                if (!(save["world"] is JArray world))
                {
                    Debug.LogError("セーブにworld配列が無いため、ブロックstateの展開を行いませんでした。");
                    return 0;
                }

                var expanded = 0;
                foreach (var blockToken in world)
                {
                    if (!(blockToken is JObject block))
                    {
                        Debug.LogError($"world要素がオブジェクトではないため展開をとばします。 type={blockToken.Type}");
                        continue;
                    }

                    var stateToken = block["state"];
                    if (stateToken == null || stateToken.Type == JTokenType.Null)
                    {
                        block["state"] = new JObject();
                        continue;
                    }

                    // stateが非オブジェクト(文字列・配列等)なら元の値を残したまま展開をとばす。無音で捨てない
                    // If state is a non-object (string, array, ...), skip expansion but keep the original value; never discard silently
                    if (!(stateToken is JObject state))
                    {
                        Debug.LogError($"world要素のstateがオブジェクトではありません。元の値を残したまま展開をとばします。 type={stateToken.Type}");
                        continue;
                    }

                    expanded += ExpandStateValues(state);
                }

                return expanded;
            }

            int ExpandStateValues(JObject state)
            {
                var expanded = 0;
                foreach (var property in state.Properties().ToList())
                {
                    if (property.Value.Type != JTokenType.String) continue;

                    var text = property.Value.Value<string>();
                    if (!TryParseJson(text, out var parsed))
                    {
                        Debug.Log($"state['{property.Name}']はJSONとして読めないため展開せずそのまま残します。");
                        continue;
                    }

                    // 二重エンコードは1回の展開では文字列のまま残り、移行済みに見えてロード時に落ちる
                    // A double-encoded value stays a string after one unwrap; it would look migrated yet break the load
                    if (parsed.Type == JTokenType.String)
                    {
                        var sample = text.Length <= DoubleEncodedSampleLength ? text : text.Substring(0, DoubleEncodedSampleLength);
                        var reason = $"state['{property.Name}']が二重エンコードされています。1回の展開では文字列のままです: {sample}";
                        Debug.LogError(reason);
                        throw new InvalidOperationException(reason);
                    }

                    property.Value = parsed;
                    expanded++;
                }

                return expanded;
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

                if (save["currentTick"] == null)
                {
                    save["currentTick"] = 0;
                    added.Add("currentTick");
                }

                if (save["randomState"] == null)
                {
                    save["randomState"] = JArray.FromObject(GameRandom.StateFromSeed(LegacySaveRandomSeed));
                    added.Add("randomState");
                }

                if (save["miningCooldowns"] == null)
                {
                    save["miningCooldowns"] = new JArray();
                    added.Add("miningCooldowns");
                }

                return added.Count == 0 ? "なし" : string.Join(",", added);
            }

            #endregion
        }
    }
}
