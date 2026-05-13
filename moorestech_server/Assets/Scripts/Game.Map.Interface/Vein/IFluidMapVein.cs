using Core.Master;
using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IFluidMapVein
    {
        public FluidId VeinFluidId { get; }

        public Vector3Int VeinRangeMin { get; }
        public Vector3Int VeinRangeMax { get; }
    }
}
