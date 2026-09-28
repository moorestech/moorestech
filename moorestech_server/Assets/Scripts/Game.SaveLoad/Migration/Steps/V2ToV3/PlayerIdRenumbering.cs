using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V2ToV3
{
    // 旧IDの検査を全節で済ませてから、昇順の連番へ一括で写す
    // Validate legacy ids across every section before applying ascending sequential ids
    public static class PlayerIdRenumbering
    {
        internal const string PlayerEntityType = "va:Player";
        private static readonly (string section, string key, bool allowDuplicateIds)[] PlayerIdSections =
        {
            ("playerInventory", "PlayerId", false),
            ("playerRidingStates", "PlayerId", false),
            ("hotbarAssignments", "PlayerId", false),
            ("remainingPlacementCounts", "PlayerId", false),
            ("constructionPayers", "PlayerId", true),
            ("miningCooldowns", "playerId", false),
        };

        internal static bool TryBuildMap(JObject save, out Dictionary<long, int> map, out string reason)
        {
            map = null;
            var oldIds = new SortedSet<long>();
            foreach (var (section, key, allowDuplicateIds) in PlayerIdSections)
            {
                if (!TryCollect(section, key, false, allowDuplicateIds, out reason)) return false;
            }
            if (!TryCollect("entities", "InstanceId", true, false, out reason)) return false;

            // 同じIDを参照する複数の節は、同じ連番に結びつける
            // References to one id in multiple sections share the same sequential id
            map = new Dictionary<long, int>();
            var next = 1;
            foreach (var oldId in oldIds) map[oldId] = next++;
            reason = null;
            return true;

            #region Internal

            bool TryCollect(string section, string key, bool onlyPlayerEntities, bool allowDuplicateIds, out string collectReason)
            {
                collectReason = null;
                var token = save[section];
                if (token == null || token.Type == JTokenType.Null) return true;
                if (token is not JArray array)
                {
                    collectReason = $"{section} が配列でない";
                    return false;
                }

                // 配列内の壊れた要素は無視せず、変換前に拒否する
                // Reject malformed array entries before conversion rather than ignoring them
                var sectionIds = new HashSet<long>();
                foreach (var element in array)
                {
                    if (element is not JObject obj)
                    {
                        collectReason = $"{section} の要素がオブジェクトでない";
                        return false;
                    }
                    if (onlyPlayerEntities)
                    {
                        if (obj["Type"]?.Type != JTokenType.String)
                        {
                            collectReason = "entities の Type が文字列でない";
                            return false;
                        }
                        if ((string)obj["Type"] != PlayerEntityType) continue;
                    }

                    // JSON整数はlongを超え得るため、キャスト前に範囲を検査する
                    // A JSON integer can exceed long, so check its range before casting
                    if (obj[key]?.Type != JTokenType.Integer ||
                        !long.TryParse(obj[key].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var oldId) || oldId <= 0)
                    {
                        collectReason = $"{section} の {key} が正のlong整数でない";
                        return false;
                    }
                    if (!sectionIds.Add(oldId) && !allowDuplicateIds)
                    {
                        collectReason = $"{section} のプレイヤーIDが重複している: {oldId}";
                        return false;
                    }
                    oldIds.Add(oldId);
                }
                return true;
            }

            #endregion
        }

        internal static void Apply(JObject save, Dictionary<long, int> map)
        {
            foreach (var (section, key, _) in PlayerIdSections) Rewrite(section, key, false);
            Rewrite("entities", "InstanceId", true);

            #region Internal

            // 収集時に検証済みの節だけを書き換える
            // Rewrite only sections already validated during collection
            void Rewrite(string section, string key, bool onlyPlayerEntities)
            {
                if (save[section] is not JArray array) return;
                foreach (var obj in array.OfType<JObject>())
                {
                    if (onlyPlayerEntities && (string)obj["Type"] != PlayerEntityType) continue;
                    obj[key] = map[(long)obj[key]];
                }
            }

            #endregion
        }
    }
}
