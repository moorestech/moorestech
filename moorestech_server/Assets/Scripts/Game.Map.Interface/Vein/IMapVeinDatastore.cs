using System.Collections.Generic;
using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IMapVeinDatastore
    {
        public List<IMapVein> GetOverVeins(Vector3Int pos);
    }
}