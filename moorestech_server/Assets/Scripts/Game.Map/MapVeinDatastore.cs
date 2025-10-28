using System.Collections.Generic;
using Core.Master;
using Game.Map.Interface.Json;
using Game.Map.Interface.Vein;
using UnityEngine;

namespace Game.Map
{
    public class MapVeinDatastore : IMapVeinDatastore
    {
        private readonly List<IMapVein> _mapVeins = new();
        
        public MapVeinDatastore(MapInfoJson mapInfoJson)
        {
            //configからmap obejctを生成
            foreach (var veinJson in mapInfoJson.MapVeins)
            {
                if (!MasterHolder.ItemMaster.ExistItemId(veinJson.VeinItemGuid))
                {
                    Debug.LogError($"GUID:{veinJson.VeinItemGuid}に対応するItemIdが存在しません。鉱脈の生成をスキップします。");
                    continue;
                }
                
                var itemId = MasterHolder.ItemMaster.GetItemId(veinJson.VeinItemGuid);
                var vein = new MapVein(itemId, veinJson.MinPosition, veinJson.MaxPosition);
                _mapVeins.Add(vein);
            }
        }
        
        public List<IMapVein> GetOverVeins(Vector3Int pos)
        {
            var veins = new List<IMapVein>();
            foreach (var vein in _mapVeins)
                if (vein.VeinRangeMin.x <= pos.x && pos.x <= vein.VeinRangeMax.x &&
                    vein.VeinRangeMin.y <= pos.y && pos.y <= vein.VeinRangeMax.y &&
                    vein.VeinRangeMin.z <= pos.z && pos.z <= vein.VeinRangeMax.z)
                    veins.Add(vein);
            
            return veins;
        }
    }
}