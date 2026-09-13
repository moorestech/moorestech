using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning
{
    /// <summary>
    /// セーブの木を歩き、マスタに無いアイテムのスタックを空スタックへ落とす
    /// Walks the save tree and empties every item stack whose item is absent from the master
    /// 文字列に埋め込まれたJSON(ブロックのstate)の中まで降り、除去したときだけ書き戻す
    /// Descends into JSON embedded in strings (block state) and writes back only when something was pruned
    /// </summary>
    public sealed class ItemStackPruneWalker
    {
        private const string ItemGuidKey = "itemGuid";
        private const string CountKey = "count";
        private const int LoggedItemGuidLimit = 10;

        private readonly Dictionary<string, int> _emptiedCountByItemGuid = new();
        private int _nonJsonStringCount;
        private int _unparsableStringCount;

        // 除去した元のスタックを返す。呼び出し側はこれを除去データJSONへ載せる
        // Returns the original stacks that were emptied; the caller stores them in the pruned-data JSON
        public JArray Walk(JObject save)
        {
            _emptiedCountByItemGuid.Clear();
            _nonJsonStringCount = 0;
            _unparsableStringCount = 0;

            var removed = new JArray();
            WalkToken(save, removed);
            LogEmptiedItemStacks();
            LogSkippedStrings();
            return removed;
        }

        private void WalkToken(JToken token, JArray removed)
        {
            // 配列と文字列は中身へ降りるだけ。除去判定はJObjectに到達してから行う
            // Arrays and strings only lead further down; the removal decision happens once a JObject is reached
            if (token is JArray array)
            {
                foreach (var child in array.ToList()) WalkToken(child, removed);
                return;
            }

            if (token is JValue value)
            {
                RewriteEmbeddedJson(value, removed);
                return;
            }

            if (token is not JObject json) return;

            if (json[ItemGuidKey] is JValue guidValue && IsMissingItem(guidValue, out var guidText))
            {
                removed.Add(json.DeepClone());
                _emptiedCountByItemGuid[guidText] = _emptiedCountByItemGuid.GetValueOrDefault(guidText) + 1;
                json[ItemGuidKey] = Guid.Empty.ToString();
                // countを持たない別形のJSONへ新しいキーを生やさない
                // Never grow a count key on a differently shaped JSON that did not have one
                if (json[CountKey] != null) json[CountKey] = 0;
            }

            foreach (var property in json.Properties().ToList()) WalkToken(property.Value, removed);
        }

        private bool IsMissingItem(JValue guidValue, out string guidText)
        {
            // guidとして読めない値は壊れているかスタックでない。残す判断の理由を出す
            // A value that is no guid is corrupt or not a stack at all; log why it is left alone
            guidText = guidValue.Value<string>();
            if (!Guid.TryParse(guidText, out var guid))
            {
                Debug.LogWarning($"itemGuidがguidとして読めないため除去判定せず残します。 itemGuid={guidText}");
                return false;
            }

            if (guid == Guid.Empty) return false;
            return !MasterHolder.ItemMaster.ExistItemId(guid);
        }

        private void RewriteEmbeddedJson(JValue value, JArray removed)
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
            var before = removed.Count;
            WalkToken(embedded, removed);
            if (removed.Count == before) return;

            value.Value = embedded.ToString(Formatting.None);
        }

        // modを外すと数千件になるのでguidごとに集約して1行にまとめる
        // Dropping a mod can empty thousands of stacks, so the report is aggregated per guid into one line
        private void LogEmptiedItemStacks()
        {
            if (_emptiedCountByItemGuid.Count == 0) return;

            var total = _emptiedCountByItemGuid.Values.Sum();
            var digest = string.Join(", ", _emptiedCountByItemGuid.Take(LoggedItemGuidLimit).Select(pair => $"{pair.Key}x{pair.Value}"));
            Debug.LogWarning($"マスタに存在しないアイテムを空スタックへ落としました。 total={total} guidKinds={_emptiedCountByItemGuid.Count} detail={digest}");
        }

        // 文字列は全件ここを通るため、素通しした理由は件数だけ1行で残す
        // Every string passes through here, so the pass-through reason is summarised in a single line
        private void LogSkippedStrings()
        {
            if (_nonJsonStringCount == 0 && _unparsableStringCount == 0) return;

            Debug.Log($"JSONとして読めない文字列はアイテム除去の対象外として素通ししました。 nonJson={_nonJsonStringCount} unparsable={_unparsableStringCount}");
        }
    }
}
