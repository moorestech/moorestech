using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IMapVein
    {
        public int VeinItemId { get;  }
        
        public Vector2Int VeinRangeMin { get; }
        public Vector2Int VeinRangeMax { get; }
        
    }
}