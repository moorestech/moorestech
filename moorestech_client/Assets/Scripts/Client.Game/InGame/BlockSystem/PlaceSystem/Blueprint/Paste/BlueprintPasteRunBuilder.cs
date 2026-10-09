using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Game.Blueprint;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     列の各アンカーにBPの配置要素を展開する
    ///     Expands blueprint placements at every anchor of a run
    /// </summary>
    public static class BlueprintPasteRunBuilder
    {
        public static List<BlueprintPlacementElement> Build(BlueprintJsonObject blueprint, Vector3Int startAnchor, Vector3Int cursorAnchor, Vector3Int footprintSize, int rotationStep)
        {
            var run = PlacementRunPositionCalculator.Calculate(startAnchor, cursorAnchor, footprintSize);
            var elements = new List<BlueprintPlacementElement>();
            foreach (var anchor in run.Positions)
            {
                elements.AddRange(BlueprintPasteCalculator.CalculatePlacements(blueprint, anchor, rotationStep));
            }

            return elements;
        }
    }
}
