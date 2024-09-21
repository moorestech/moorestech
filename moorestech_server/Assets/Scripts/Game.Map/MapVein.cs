using Core.Master;
using Game.Map.Interface.Vein;
using UnityEngine;

namespace Game.Map
{
    public class MapVein : IMapVein
    {
        public ItemId VeinItemId { get; }
        public Vector3Int VeinRangeMin { get; }
        public Vector3Int VeinRangeMax { get; }
        
        public MapVein(ItemId veinItemId, Vector3Int veinRangeMin, Vector3Int veinRangeMax)
        {
            VeinItemId = veinItemId;
            VeinRangeMin = veinRangeMin;
            VeinRangeMax = veinRangeMax;
        }
    }
}