using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

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
        
        // 欠損・null の判定はここだけが持つ。呼び出し元ごとに先行/後追いの判定を書くと同じ入力が場所によって落ちたり無視されたりする
        // Absence and null are decided only here; per-caller pre/post checks make the same input crash in one place and pass silently in another
        public static bool TryRead<T>(IReadOnlyDictionary<string, object> componentStates, string saveKey, out T value)
        {
            value = default;
            if (componentStates == null)
            {
                Debug.LogWarning($"ブロックのセーブ状態が丸ごとありません。キー {saveKey} の復元を見送ります");
                return false;
            }
            
            if (!componentStates.TryGetValue(saveKey, out var raw))
            {
                Debug.Log($"ブロックのセーブ状態にキー {saveKey} がないため、このコンポーネントの復元を見送ります");
                return false;
            }
            
            // 旧形式のJSON文字列は、CLR文字列でもJValue文字列でも黙って読み飛ばさず移行スクリプトの案内とともに落とす
            // Old-format JSON strings, as CLR strings or JValue strings, fail loudly with a pointer to the migration script
            if (raw is string || (raw is JValue jsonValue && jsonValue.Type == JTokenType.String))
            {
                throw new InvalidOperationException($"キー {saveKey} のセーブ状態が旧形式（JSON文字列）です。scripts/save_migration/migrate_block_state_objects.py で移行してください");
            }
            
            value = raw is JToken token ? token.ToObject<T>() : (T)raw;
            
            // 明示的な null（"key": null）は復元できる値ではない。true を返すと呼び出し元が逆参照して落ちる
            // An explicit null ("key": null) is not a restorable value; returning true would make the caller dereference it
            if (value == null)
            {
                Debug.LogWarning($"キー {saveKey} のセーブ状態が null のため、このコンポーネントの復元を見送ります");
                return false;
            }
            
            return true;
        }
    }
}
