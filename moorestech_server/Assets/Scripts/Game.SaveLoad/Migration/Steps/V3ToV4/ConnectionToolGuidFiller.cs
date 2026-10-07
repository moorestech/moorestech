using System;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V3ToV4
{
    /// <summary>
    /// 1ブロックの接続配列へ種類を書く（冪等）
    /// Write the tool into one block's connection array (idempotent)
    /// </summary>
    public static class ConnectionToolGuidFiller
    {
        public static ConnectionToolGuidFillResult Fill(JObject state, string saveKey, Guid connectToolGuid)
        {
            // この種の接続を持たないブロックは対象外
            // Blocks without this kind of connection are out of scope
            var componentState = state[saveKey];
            if (componentState == null || componentState.Type == JTokenType.Null) return ConnectionToolGuidFillResult.Filled(0);

            // 辿れない形を素通しすると未変換のまま版4が刻まれ、必須キーの読み込みで後から落ちる
            // Passing an unwalkable shape would stamp version 4 on an unconverted save that later fails on the required key
            if (!(componentState is JObject componentObject) || !(componentObject["connections"] is JArray connections))
            {
                return ConnectionToolGuidFillResult.Failed($"state['{saveKey}'].connectionsが配列ではないため種類を書き込めません。 type={componentState.Type}");
            }

            var filled = 0;
            foreach (var connectionToken in connections)
            {
                if (!(connectionToken is JObject connection))
                {
                    return ConnectionToolGuidFillResult.Failed($"state['{saveKey}']の接続要素がオブジェクトではありません。 type={connectionToken.Type}");
                }

                // 既存値は読めるGuidだけ保持
                // Preserve only readable existing GUIDs; stop required-key failures
                var existingTool = connection["connectToolGuid"];
                if (existingTool != null)
                {
                    if ((existingTool.Type != JTokenType.String && existingTool.Type != JTokenType.Guid) ||
                        !Guid.TryParse(existingTool.ToString(), out _))
                    {
                        return ConnectionToolGuidFillResult.Failed($"state['{saveKey}'].connectionsのconnectToolGuidが有効なGuidではありません。 type={existingTool.Type}");
                    }

                    continue;
                }

                connection["connectToolGuid"] = connectToolGuid.ToString();
                filled++;
            }

            return ConnectionToolGuidFillResult.Filled(filled);
        }
    }
}
