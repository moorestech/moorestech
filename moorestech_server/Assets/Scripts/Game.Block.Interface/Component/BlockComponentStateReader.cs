using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.Block.Interface.Component
{
    // セーブ状態の値は「ロード時はJToken」「同一プロセス内の往復ではオブジェクト」の2形。文字列は旧形式として拒否する
    // A state value is a JToken when loaded from disk or the object itself for in-process round trips; strings are the old format
    public static class BlockComponentStateReader
    {
        public static T Read<T>(IReadOnlyDictionary<string, object> componentStates, string saveKey)
        {
            if (!TryRead<T>(componentStates, saveKey, out var value))
            {
                throw new KeyNotFoundException($"ブロックのセーブ状態にキー {saveKey} がありません");
            }
            
            return value;
        }
        
        public static bool TryRead<T>(IReadOnlyDictionary<string, object> componentStates, string saveKey, out T value)
        {
            if (!componentStates.TryGetValue(saveKey, out var raw))
            {
                value = default;
                return false;
            }
            
            // 旧形式のJSON文字列は、CLR文字列でもJValue文字列でも黙って読み飛ばさず移行スクリプトの案内とともに落とす
            // Old-format JSON strings, as CLR strings or JValue strings, fail loudly with a pointer to the migration script
            if (raw is string || (raw is JValue jsonValue && jsonValue.Type == JTokenType.String))
            {
                throw new InvalidOperationException($"キー {saveKey} のセーブ状態が旧形式（JSON文字列）です。scripts/save_migration/migrate_block_state_objects.py で移行してください");
            }
            
            if (raw is JToken token)
            {
                value = token.ToObject<T>();
                return true;
            }
            
            value = (T)raw;
            return true;
        }
    }
}
