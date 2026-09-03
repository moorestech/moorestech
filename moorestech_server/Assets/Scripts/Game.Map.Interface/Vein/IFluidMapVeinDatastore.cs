using System.Collections.Generic;
using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IFluidMapVeinDatastore
    {
        // 手掘り・露頭用のセル包含判定（Y込みinclusive）
        // Cell-containment check for hand mining / exposed veins (Y-inclusive)
        public List<IFluidMapVein> GetVeinsContainingCell(Vector3Int cell);
    }
}
