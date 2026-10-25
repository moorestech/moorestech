using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    /// <summary>
    /// 版7→8の部品が共有するセーブ JSON の辿り方。欠損と null の判定・world→ブロック→state の走査・拒否理由の返し方を 1 か所に置く
    /// Save-JSON walking shared by the version 7-to-8 parts; the absent/null tests, the world->block->state walk and refusal returns live in one place
    /// </summary>
    public static class V7ToV8SaveJson
    {
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
    }
}
