using Core.Master;
using Game.Map.Interface.Vein;
using UnityEngine;

namespace Game.Map
{
    public class FluidMapVein : IFluidMapVein
    {
        public FluidId VeinFluidId { get; }
        public Vector3Int VeinRangeMin { get; }
        public Vector3Int VeinRangeMax { get; }

        public FluidMapVein(FluidId veinFluidId, Vector3Int veinRangeMin, Vector3Int veinRangeMax)
        {
            VeinFluidId = veinFluidId;
            VeinRangeMin = veinRangeMin;
            VeinRangeMax = veinRangeMax;
        }
    }
}
