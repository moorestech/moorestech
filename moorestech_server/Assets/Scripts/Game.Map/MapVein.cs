using Game.Map.Interface.Vein;
using UnityEngine;

namespace Game.Map
{
    public class MapVein : IMapVein
    {
        public int VeinItemId { get; }
        public Vector2Int VeinRangeMin { get; }
        public Vector2Int VeinRangeMax { get; }
        
        public MapVein(int veinItemId, Vector2Int veinRangeMin, Vector2Int veinRangeMax)
        {
            VeinItemId = veinItemId;
            VeinRangeMin = veinRangeMin;
            VeinRangeMax = veinRangeMax;
        }
    }
}