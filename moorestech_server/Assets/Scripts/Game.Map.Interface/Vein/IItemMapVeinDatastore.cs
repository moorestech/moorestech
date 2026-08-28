using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IItemMapVeinDatastore
    {
        public List<IItemMapVein> GetOverVeins(Vector3Int pos);

        /// <summary>
        ///     採掘機の底面フットプリントとXZで重なる鉱脈を全て返す（ADR 0039）
        ///     Returns every vein whose XZ range overlaps the miner footprint (ADR 0039)
        /// </summary>
        public List<IItemMapVein> GetVeinsOverlappingFootprint(BlockPositionInfo footprint);
    }
}