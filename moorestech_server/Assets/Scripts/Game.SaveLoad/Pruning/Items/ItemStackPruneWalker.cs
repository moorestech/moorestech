using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning.Items
{
    /// <summary>
    /// 形がコンポーネントごとに違う持ち物（ブロックのstate・貨車のコンテナ）を歩き、アイテム参照をMissingItemReferenceCleanerへ渡す
    /// Walks owner contents whose shape differs per component (block state, train car containers) and hands item references to MissingItemReferenceCleaner
    /// 文字列に埋め込まれたJSONの中まで降り、除去したときだけ書き戻す。位置は持ち主の住所へ足して控える
    /// Descends into JSON embedded in strings, writes back only when something was pruned, and records positions onto the owner's address
    /// </summary>
    public sealed class ItemStackPruneWalker
    {
        private readonly MissingItemReferenceCleaner _cleaner;
        private int _nonJsonStringCount;
        private int _unparsableStringCount;

        public ItemStackPruneWalker(MissingItemReferenceCleaner cleaner)
        {
            _cleaner = cleaner;
        }

        public void Walk(JToken ownerContents, PrunedItemOrigin ownerOrigin)
        {
            WalkToken(ownerContents, ownerContents, ownerOrigin);
        }

        // 文字列は全件ここを通るため、素通しした理由は件数だけ1行で残す
        // Every string passes through here, so the pass-through reason is summarised in a single line
        public void LogSkippedStrings(string sectionName)
        {
            if (_nonJsonStringCount == 0 && _unparsableStringCount == 0) return;

            Debug.Log($"JSONとして読めない文字列はアイテム除去の対象外として素通ししました。 section={sectionName} nonJson={_nonJsonStringCount} unparsable={_unparsableStringCount}");
        }

        private void WalkToken(JToken token, JToken root, PrunedItemOrigin rootOrigin)
        {
            // 配列と文字列は中身へ降りるだけ。除去判定はJObjectに到達してから行う
            // Arrays and strings only lead further down; the removal decision happens once a JObject is reached
            if (token is JArray array)
            {
                foreach (var child in array.ToList()) WalkToken(child, root, rootOrigin);
                return;
            }

            if (token is JValue value)
            {
                RewriteEmbeddedJson(value, root, rootOrigin);
                return;
            }

            if (token is not JObject json) return;

            _cleaner.EmptyItemStackIfMissing(json, Locate(json, root, rootOrigin));

            foreach (var property in json.Properties().ToList())
            {
                if (property.Name == BlockStateItemReferenceFields.ConnectionCostMaterialsPropertyName && property.Value is JArray materials)
                {
                    foreach (var material in materials.OfType<JObject>()) _cleaner.NeutralizeConnectionMaterialIfMissing(material, Locate(material, root, rootOrigin));
                    continue;
                }

                if (BlockStateItemReferenceFields.BareItemGuidPropertyNames.Contains(property.Name))
                {
                    _cleaner.ClearBareItemGuidIfMissing(property, Locate(json, root, rootOrigin));
                    continue;
                }

                WalkToken(property.Value, root, rootOrigin);
            }
        }

        private void RewriteEmbeddedJson(JValue value, JToken root, PrunedItemOrigin rootOrigin)
        {
            if (value.Type != JTokenType.String) return;

            // JSONで始まらない文字列(base64-MessagePack・guid・名前等)は素通し。件数は末尾でまとめて出す
            // Strings that do not open as JSON (base64 MessagePack, guids, names) pass through; counted in the summary line
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

            // 埋め込みJSONは独立した木なので、文字列の位置を住所へ足してから内側を歩く
            // Embedded JSON is a separate tree, so the string's position is added to the address before walking inside
            var before = _cleaner.RemovalCount;
            WalkToken(embedded, embedded, Locate(value, root, rootOrigin));
            if (_cleaner.RemovalCount == before) return;

            // 除去が起きたときだけ書き戻す。無関係なstateを整形しなおして差分を出さない
            // Write back only when something was pruned, so untouched state is not reformatted
            value.Value = embedded.ToString(Formatting.None);
        }

        // 持ち主の中身の根からの相対位置。格納先は最寄りのプロパティ名、スロットは親配列の添字
        // The position relative to the owner contents root: the container is the nearest property name, the slot the index in the parent array
        private static PrunedItemOrigin Locate(JToken token, JToken root, PrunedItemOrigin rootOrigin)
        {
            if (ReferenceEquals(token, root)) return rootOrigin.At(null, null, null);

            int? slot = token.Parent is JArray parentArray ? parentArray.IndexOf(token) : null;
            string container = null;
            for (var ancestor = token.Parent; ancestor != null && !ReferenceEquals(ancestor, root.Parent); ancestor = ancestor.Parent)
            {
                if (ancestor is not JProperty property) continue;
                container = property.Name;
                break;
            }

            var rootPath = root.Path;
            var relativePath = token.Path.Substring(rootPath.Length).TrimStart('.');
            return rootOrigin.At(container, slot, relativePath);
        }
    }
}
