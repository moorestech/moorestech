using System.Collections.Generic;
using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IItemMapVeinDatastore
    {
        public List<IItemMapVein> GetOverVeins(Vector3Int pos);
    }
}