using System.Collections.Generic;
using Core.Master;
using Game.Map.Interface.Json;
using Game.Map.Interface.Vein;
using UnityEngine;

namespace Game.Map
{
    public class ItemMapVeinDatastore : IItemMapVeinDatastore
    {
        private readonly List<IItemMapVein> _mapVeins = new();

        public ItemMapVeinDatastore(MapInfoJson mapInfoJson)
        {
            // configからアイテム鉱脈を生成。GUIDが解決できない場合はスキップ
            // Generate item veins from config; skip entries whose GUID cannot be resolved
            foreach (var veinJson in mapInfoJson.ItemMapVeins)
            {
                if (!MasterHolder.ItemMaster.ExistItemId(veinJson.VeinItemGuid))
                {
                    Debug.LogError($"GUID:{veinJson.VeinItemGuid}に対応するItemIdが存在しません。鉱脈の生成をスキップします。");
                    continue;
                }

                var itemId = MasterHolder.ItemMaster.GetItemId(veinJson.VeinItemGuid);
                var vein = new ItemMapVein(itemId, veinJson.MinPosition, veinJson.MaxPosition);
                _mapVeins.Add(vein);
            }
        }

        public List<IItemMapVein> GetOverVeins(Vector3Int pos)
        {
            var veins = new List<IItemMapVein>();
            foreach (var vein in _mapVeins)
                if (vein.VeinRangeMin.x <= pos.x && pos.x <= vein.VeinRangeMax.x &&
                    vein.VeinRangeMin.y <= pos.y && pos.y <= vein.VeinRangeMax.y &&
                    vein.VeinRangeMin.z <= pos.z && pos.z <= vein.VeinRangeMax.z)
                    veins.Add(vein);

            return veins;
        }
    }
}
