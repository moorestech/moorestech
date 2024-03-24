using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IMapVein
    {
        public int VeinItemId { get; }

        public Vector3Int VeinRangeMin { get; }
        public Vector3Int VeinRangeMax { get; }
    }
}