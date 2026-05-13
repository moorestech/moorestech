using System.Collections.Generic;
using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IFluidMapVeinDatastore
    {
        public List<IFluidMapVein> GetOverVeins(Vector3Int pos);
    }
}
