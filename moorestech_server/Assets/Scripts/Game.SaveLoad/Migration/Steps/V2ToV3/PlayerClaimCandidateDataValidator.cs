using System.Globalization;
using Game.SaveLoad.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V2ToV3
{
    // 候補の比較に使う外部JSONを、IDの書き換え前に検証する
    // Validate external JSON used to rank candidates before rewriting any ids
    internal static class PlayerClaimCandidateDataValidator
    {
        internal static bool TryValidate(JObject save, out string reason)
        {
            reason = null;
            if (save["setting"] is JToken setting && setting.Type != JTokenType.Null)
            {
                if (setting is not JObject spawn || !CoordinatesValid(spawn, "Spawn"))
                {
                    reason = "setting のスポーン座標が不正";
                    return false;
                }
            }

            // プレイヤー以外の座標は候補選定に使わない
            // Coordinates of non-player entities do not participate in candidate selection
            if (save[PlayerScopedSaveSections.Entities] is JArray entities)
            {
                foreach (JObject entity in entities)
                {
                    if ((string)entity["Type"] != PlayerScopedSaveSections.PlayerEntityType) continue;
                    if (CoordinatesValid(entity, "")) continue;
                    reason = "プレイヤーの座標が不正";
                    return false;
                }
            }

            if (save[PlayerScopedSaveSections.PlayerInventory] is not JArray inventories) return true;
            foreach (JObject inventory in inventories)
            {
                if (StacksValid(inventory["MainInventoryItems"]) && StacksValid(inventory["EquipmentInventoryItems"]) &&
                    StackValid(inventory["GrabInventoryItems"])) continue;
                reason = "playerInventory のアイテム配列または個数が不正";
                return false;
            }
            return true;

            #region Internal

            bool CoordinatesValid(JObject source, string prefix)
            {
                foreach (var axis in new[] { "X", "Y", "Z" })
                {
                    var token = source[prefix + axis];
                    if (token == null || token.Type == JTokenType.Null) continue;
                    if ((token.Type != JTokenType.Integer && token.Type != JTokenType.Float) ||
                        !double.TryParse(token.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                        double.IsNaN(value) || double.IsInfinity(value)) return false;
                }
                return true;
            }

            bool StacksValid(JToken token)
            {
                if (token == null || token.Type == JTokenType.Null) return true;
                if (token is not JArray stacks) return false;
                foreach (var stack in stacks)
                {
                    if (!StackValid(stack)) return false;
                }
                return true;
            }

            bool StackValid(JToken token)
            {
                if (token == null || token.Type == JTokenType.Null) return true;
                if (token is not JObject stack) return false;
                var count = stack["count"];
                return count?.Type == JTokenType.Integer &&
                    int.TryParse(count.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && 0 <= value;
            }

            #endregion
        }
    }
}
