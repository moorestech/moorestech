using System;
using Core.Master;
using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IItemMapVein
    {
        public Guid VeinGuid { get; }
        public ItemId VeinItemId { get; }

        public Vector3Int VeinRangeMin { get; }
        public Vector3Int VeinRangeMax { get; }
    }
}