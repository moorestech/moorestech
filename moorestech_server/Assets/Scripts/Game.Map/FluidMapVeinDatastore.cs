using System.Collections.Generic;
using Core.Master;
using Game.Map.Interface.Json;
using Game.Map.Interface.Vein;
using UnityEngine;

namespace Game.Map
{
    public class FluidMapVeinDatastore : IFluidMapVeinDatastore
    {
        private readonly List<IFluidMapVein> _fluidVeins = new();

        public FluidMapVeinDatastore(MapInfoJson mapInfoJson)
        {
            // 既存map.jsonとの互換のためnull許容
            // Allow null for backward compatibility with legacy map.json
            if (mapInfoJson.FluidVeins == null) return;

            foreach (var veinJson in mapInfoJson.FluidVeins)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidIdOrNull(veinJson.VeinFluidGuid);
                if (fluidId == null)
                {
                    Debug.LogError($"GUID:{veinJson.VeinFluidGuid}に対応するFluidIdが存在しません。液体鉱脈の生成をスキップします。");
                    continue;
                }

                var vein = new FluidMapVein(fluidId.Value, veinJson.MinPosition, veinJson.MaxPosition);
                _fluidVeins.Add(vein);
            }
        }

        public List<IFluidMapVein> GetOverVeins(Vector3Int pos)
        {
            var veins = new List<IFluidMapVein>();
            foreach (var vein in _fluidVeins)
                if (vein.VeinRangeMin.x <= pos.x && pos.x <= vein.VeinRangeMax.x &&
                    vein.VeinRangeMin.y <= pos.y && pos.y <= vein.VeinRangeMax.y &&
                    vein.VeinRangeMin.z <= pos.z && pos.z <= vein.VeinRangeMax.z)
                    veins.Add(vein);

            return veins;
        }
    }
}
