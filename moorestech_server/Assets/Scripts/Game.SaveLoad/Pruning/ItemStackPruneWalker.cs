using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning
{
    /// <summary>
    /// セーブの木を歩き、アイテムを参照する綴りに出会うたびMissingItemReferenceCleanerへ渡す
    /// Walks the save tree and hands every item-referencing spelling it meets to MissingItemReferenceCleaner
    /// 文字列に埋め込まれたJSON(ブロックのstate)の中まで降り、除去したときだけ書き戻す
    /// Descends into JSON embedded in strings (block state) and writes back only when something was pruned
    /// 何をアイテム参照とみなすかはSaveItemReferenceFieldsが唯一の正本
    /// SaveItemReferenceFields is the only authority on what counts as an item reference
    /// </summary>
    public sealed class ItemStackPruneWalker
    {
        private MissingItemReferenceCleaner _cleaner;
        private int _nonJsonStringCount;
        private int _unparsableStringCount;

        // 除去前のアイテム参照を返す（除去データJSON用）
        // Returns the original item references that were removed; the caller stores them in the pruned-data JSON
        public ItemPruneWalkResult Walk(JObject save)
        {
            _cleaner = new MissingItemReferenceCleaner();
            _nonJsonStringCount = 0;
            _unparsableStringCount = 0;

            WalkToken(save);
            _cleaner.LogRemovedItemReferences();
            LogSkippedStrings();
            return _cleaner.ToResult();

            #region Internal

            // 文字列は全件ここを通るため、素通しした理由は件数だけ1行で残す
            // Every string passes through here, so the pass-through reason is summarised in a single line
            void LogSkippedStrings()
            {
                if (_nonJsonStringCount == 0 && _unparsableStringCount == 0) return;

                Debug.Log($"JSONとして読めない文字列はアイテム除去の対象外として素通ししました。 nonJson={_nonJsonStringCount} unparsable={_unparsableStringCount}");
            }

            #endregion
        }

        private void WalkToken(JToken token)
        {
            // 配列と文字列は中身へ降りるだけ。除去判定はJObjectに到達してから行う
            // Arrays and strings only lead further down; the removal decision happens once a JObject is reached
            if (token is JArray array)
            {
                foreach (var child in array.ToList()) WalkToken(child);
                return;
            }

            if (token is JValue value)
            {
                RewriteEmbeddedJson(value);
                return;
            }

            if (token is not JObject json) return;

            _cleaner.EmptyItemStackIfMissing(json);

            foreach (var property in json.Properties().ToList())
            {
                if (property.Name == SaveItemReferenceFields.ConnectionCostMaterialsPropertyName)
                {
                    _cleaner.NeutralizeConnectionMaterialsIfMissing(property.Value);
                    continue;
                }

                if (SaveItemReferenceFields.BareItemGuidPropertyNames.Contains(property.Name))
                {
                    _cleaner.ClearBareItemGuidIfMissing(property);
                    continue;
                }

                WalkToken(property.Value);
            }
        }

        private void RewriteEmbeddedJson(JValue value)
        {
            if (value.Type != JTokenType.String) return;

            // JSONで始まらない文字列(base64-MessagePack・guid・名前等)は素通し。件数はWalk末尾でまとめて出す
            // Strings that do not open as JSON (base64 MessagePack, guids, names) pass through; counted at the end of Walk
            var text = value.Value<string>();
            var trimmed = text == null ? string.Empty : text.TrimStart();
            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                _nonJsonStringCount++;
                return;
            }

            JToken embedded;
            // 外部入力(セーブファイル)のパースなので隔離目的のtry-catchを使う。壊れたJSONは素通しする
            // Parsing external input (the save file), so the isolation try-catch is allowed; broken JSON passes through
            try
            {
                embedded = JToken.Parse(text);
            }
            catch (JsonReaderException e)
            {
                _unparsableStringCount++;
                Debug.Log($"JSONとして壊れている状態値はアイテム除去の対象外です。 reason={e.Message}");
                return;
            }

            // 除去が起きたときだけ書き戻す。無関係なstateを整形しなおして差分を出さない
            // Write back only when something was pruned, so untouched state is not reformatted
            var before = _cleaner.RemovalCount;
            WalkToken(embedded);
            if (_cleaner.RemovalCount == before) return;

            value.Value = embedded.ToString(Formatting.None);
        }
    }
}
