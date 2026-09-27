using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V2ToV3
{
    // 持ち物総数が最大の旧プレイヤーを結びつけ候補に選ぶ。同数はスポーンから遠い方、次に新IDが小さい方（ユーザー裁定 2026-09-27）
    // Picks the legacy player with the most items; ties go to the one farther from spawn, then the smaller new id (user ruling 2026-09-27)
    public static class PlayerClaimCandidateSelector
    {
        public static int? Select(JObject save, Dictionary<long, int> map)
        {
            if (map.Count == 0) return null;

            var spawn = save["setting"] as JObject;
            var spawnX = (double?)spawn?["SpawnX"] ?? 0;
            var spawnY = (double?)spawn?["SpawnY"] ?? 0;
            var spawnZ = (double?)spawn?["SpawnZ"] ?? 0;

            return map.Values
                .Select(newId => (newId, items: CountItems(newId), distance: DistanceFromSpawn(newId)))
                .OrderByDescending(c => c.items)
                .ThenByDescending(c => c.distance)
                .ThenBy(c => c.newId)
                .First().newId;

            #region Internal

            // 変換後の節を読むので新IDで引く。スタックは itemGuid/count 形
            // Reads the already-renumbered sections by new id; stacks are itemGuid/count
            long CountItems(int newId)
            {
                var inventory = (save["playerInventory"] as JArray)?.OfType<JObject>().FirstOrDefault(p => (int)p["PlayerId"] == newId);
                if (inventory == null) return 0;
                var stacks = new List<JToken>();
                if (inventory["MainInventoryItems"] is JArray main) stacks.AddRange(main);
                if (inventory["EquipmentInventoryItems"] is JArray equipment) stacks.AddRange(equipment);
                if (inventory["GrabInventoryItems"] is JObject grab) stacks.Add(grab);
                return stacks.OfType<JObject>().Sum(stack => (long?)stack["count"] ?? 0);
            }

            double DistanceFromSpawn(int newId)
            {
                var entity = (save["entities"] as JArray)?.OfType<JObject>()
                    .FirstOrDefault(e => (string)e["Type"] == "va:Player" && (long)e["InstanceId"] == newId);
                if (entity == null) return 0;
                var dx = ((double?)entity["X"] ?? spawnX) - spawnX;
                var dy = ((double?)entity["Y"] ?? spawnY) - spawnY;
                var dz = ((double?)entity["Z"] ?? spawnZ) - spawnZ;
                return dx * dx + dy * dy + dz * dz;
            }

            #endregion
        }
    }
}
