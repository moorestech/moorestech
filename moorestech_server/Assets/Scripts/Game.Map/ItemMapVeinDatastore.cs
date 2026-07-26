using System.Collections.Generic;
using Core.Master;
using Game.Map.Interface.Json;
using Game.Map.Interface.Vein;
using Mooresmaster.Model.MapModule;
using UnityEngine;

namespace Game.Map
{
    public class ItemMapVeinDatastore : IItemMapVeinDatastore
    {
        private readonly List<IItemMapVein> _mapVeins = new();

        public ItemMapVeinDatastore(MapInfoJson mapInfoJson)
        {
            // mapVeins配列を走査し、マスタでitem種別の鉱脈だけを対象itemGuidを導出して生成する
            // Iterate mapVeins; build veins only for item-type entries, deriving target itemGuid from master
            foreach (var veinJson in mapInfoJson.MapVeins)
            {
                var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinJson.VeinGuid);
                if (element == null)
                {
                    Debug.LogError($"veinGuid:{veinJson.VeinGuid}に対応するMapVeinマスタが存在しません。鉱脈の生成をスキップします。");
                    continue;
                }

                // item鉱脈以外はこのDatastoreの対象外なので静かにスキップ
                // Non-item veins belong to the fluid datastore; skip silently
                if (element.VeinParam is not ItemVeinParam itemVeinParam) continue;

                if (!MasterHolder.ItemMaster.ExistItemId(itemVeinParam.ItemGuid))
                {
                    Debug.LogError($"ItemGuid:{itemVeinParam.ItemGuid}に対応するItemIdが存在しません。鉱脈の生成をスキップします。");
                    continue;
                }

                var itemId = MasterHolder.ItemMaster.GetItemId(itemVeinParam.ItemGuid);
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
