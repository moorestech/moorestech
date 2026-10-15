using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 設置不可セル列と地形乗り越え断念を運ぶ
    // Carries blocked cells and whether terrain climbing was abandoned
    public readonly struct ConveyorOverpassRaiseResult
    {
        // PlaceInfo列と同じ添字の立体交差不能セル
        // Overpass-blocked cells indexed like the PlaceInfo list
        public readonly IReadOnlyList<bool> OverpassBlocked;

        // 地形なしへ戻して設置可能になった列だけ真
        // True only when the terrain-free fallback is placeable
        public readonly bool TerrainClimbAbandoned;

        public ConveyorOverpassRaiseResult(IReadOnlyList<bool> overpassBlocked, bool terrainClimbAbandoned)
        {
            OverpassBlocked = overpassBlocked;
            TerrainClimbAbandoned = terrainClimbAbandoned;
        }
    }
}
