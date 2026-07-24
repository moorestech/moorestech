using System.Collections.Generic;
using Core.Master;
using Game.Map.Interface.Json;
using Game.Map.Interface.Vein;
using Mooresmaster.Model.MapModule;
using UnityEngine;

namespace Game.Map
{
    public class FluidMapVeinDatastore : IFluidMapVeinDatastore
    {
        private readonly List<IFluidMapVein> _fluidVeins = new();

        public FluidMapVeinDatastore(MapInfoJson mapInfoJson)
        {
            // mapVeins配列を走査し、マスタでfluid種別の鉱脈だけを対象fluidGuidを導出して生成する
            // Iterate mapVeins; build veins only for fluid-type entries, deriving target fluidGuid from master
            foreach (var veinJson in mapInfoJson.MapVeins)
            {
                var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinJson.VeinGuid);
                if (element == null)
                {
                    Debug.LogError($"veinGuid:{veinJson.VeinGuid}に対応するMapVeinマスタが存在しません。液体鉱脈の生成をスキップします。");
                    continue;
                }

                // fluid鉱脈以外はこのDatastoreの対象外なので静かにスキップ
                // Non-fluid veins belong to the item datastore; skip silently
                if (element.VeinParam is not FluidVeinParam fluidVeinParam) continue;

                var fluidId = MasterHolder.FluidMaster.GetFluidIdOrNull(fluidVeinParam.FluidGuid);
                if (fluidId == null)
                {
                    Debug.LogError($"FluidGuid:{fluidVeinParam.FluidGuid}に対応するFluidIdが存在しません。液体鉱脈の生成をスキップします。");
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
