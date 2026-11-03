using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.Shared
{
    /// <summary>
    /// 全移行部品が共有するセーブ JSON の辿り方と、部品の検査→適用ループ。欠損と null の判定・world→ブロック→state の走査・拒否理由の返し方を 1 か所に置く
    /// Save-JSON walking and the inspect-then-apply loop shared by all migration parts; the absent/null tests, the world->block->state walk and refusal returns live in one place
    /// </summary>
    public static class MigrationSaveJson
    {
        // 全部品を検査してから適用する。どれかが拒否したら原本を書き換えず、直接実行しても拒否理由をログに残す
        // Inspect every part, then apply; a refusal leaves the original untouched and logs the reason even when a step is called directly
        public static SaveMigrationStepResult RunParts(int fromVersion, IReadOnlyList<ISaveMigrationPart> parts, JObject save)
        {
            var toVersion = fromVersion + 1;
            foreach (var part in parts)
            {
                if (part.TryPrepare(save, out var reason))
                    continue;
                Debug.LogWarning($"セーブを版{fromVersion}から版{toVersion}へ変換できません: {reason}");
                return SaveMigrationStepResult.Failed(reason);
            }

            // 検査を通った対象だけを書き換える
            // Rewrite only the targets that passed inspection
            var summaries = new List<string>();
            foreach (var part in parts) summaries.Add(part.ApplyAndSummarize());

            Debug.Log($"セーブを版{fromVersion}から版{toVersion}へ変換しました。{string.Join(" / ", summaries)}");
            return SaveMigrationStepResult.Converted(save);
        }

        // 清浄室機械ProcessorのSaveKey(typeof FullName)。ステップは現行の型を参照しないので文字列で持つ
        // The clean-room processor's SaveKey (typeof FullName); the step never references live types, so it is kept as a string
        public const string CleanRoomMachineSaveKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";

        // キーが無いか、値が JSON の null（どちらも「値が無い」として扱う箇所で使う）
        // The key is absent or its value is JSON null (used where both mean "no value")
        public static bool IsAbsentOrNull(JToken token)
        {
            return token == null || token.Type == JTokenType.Null;
        }

        // キーはあり、値が JSON の null（欠損と分けて数える箇所で使う）
        // The key exists and its value is JSON null (used where it is counted apart from absence)
        public static bool IsExplicitNull(JToken token)
        {
            return token != null && token.Type == JTokenType.Null;
        }

        // 拒否理由を out に入れて false を返す（TryPrepare の拒否の書き方を 1 つにする）
        // Put the refusal reason into the out parameter and return false (one way to refuse in TryPrepare)
        public static bool Reject(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }

        // world 配列の各ブロックの state を集める。state の無いブロックは機械ではないので飛ばし、形が違えば拒否する
        // Collect each world block's state; blocks without state are not machines and are skipped, a wrong shape is refused
        public static bool TryCollectBlockStates(JObject save, out List<JObject> states, out string failureReason)
        {
            states = new List<JObject>();
            failureReason = null;
            if (!(save["world"] is JArray world)) return Reject($"セーブのworldが配列ではありません。 type={save["world"]?.Type}", out failureReason);

            foreach (var blockToken in world)
            {
                if (!(blockToken is JObject block)) return Reject($"world要素がオブジェクトではありません。 type={blockToken.Type}", out failureReason);
                var stateToken = block["state"];
                if (IsAbsentOrNull(stateToken)) continue;
                if (!(stateToken is JObject state)) return Reject($"world要素のstateがオブジェクトではありません。 type={stateToken.Type}", out failureReason);
                states.Add(state);
            }

            return true;
        }

        // save[sectionKey][arrayKey] を辿り、無い（null 含む）／配列の2状態を result に入れる。形が違えば拒否する
        // Walk save[sectionKey][arrayKey] and put one of absent(incl. null)/array into result; a wrong shape is refused
        public static bool TryGetNestedArray(JObject save, string sectionKey, string arrayKey, out NestedSaveArray result, out string failureReason)
        {
            result = new AbsentNestedSaveArray();
            failureReason = null;
            var sectionToken = save[sectionKey];
            if (sectionToken == null) return true;
            if (IsExplicitNull(sectionToken))
            {
                result = new AbsentNestedSaveArray();
                return true;
            }
            if (!(sectionToken is JObject section)) return Reject($"{sectionKey} がオブジェクトではありません。 type={sectionToken.Type}", out failureReason);

            var arrayToken = section[arrayKey];
            if (arrayToken == null) return true;
            if (IsExplicitNull(arrayToken))
            {
                result = new AbsentNestedSaveArray();
                return true;
            }
            if (!(arrayToken is JArray found)) return Reject($"{sectionKey}.{arrayKey} が配列ではありません。 type={arrayToken.Type}", out failureReason);
            result = new PresentNestedSaveArray(found);
            return true;
        }
    }
}
