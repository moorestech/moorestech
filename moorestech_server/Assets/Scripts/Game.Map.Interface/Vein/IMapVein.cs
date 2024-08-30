using Core.Master;
using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IMapVein
    {
        public ItemId VeinItemId { get; }
        
        public Vector3Int VeinRangeMin { get; }
        public Vector3Int VeinRangeMax { get; }
    }
}