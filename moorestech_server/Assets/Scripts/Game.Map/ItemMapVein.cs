using System;
using Core.Master;
using Game.Map.Interface.Vein;
using UnityEngine;

namespace Game.Map
{
    public class ItemMapVein : IItemMapVein
    {
        public Guid VeinGuid { get; }
        public ItemId VeinItemId { get; }
        public Vector3Int VeinRangeMin { get; }
        public Vector3Int VeinRangeMax { get; }

        public ItemMapVein(Guid veinGuid, ItemId veinItemId, Vector3Int veinRangeMin, Vector3Int veinRangeMax)
        {
            VeinGuid = veinGuid;
            VeinItemId = veinItemId;
            VeinRangeMin = veinRangeMin;
            VeinRangeMax = veinRangeMax;
        }
    }
}