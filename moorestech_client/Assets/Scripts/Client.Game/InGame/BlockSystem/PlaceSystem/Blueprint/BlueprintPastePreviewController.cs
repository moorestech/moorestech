using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Game.Block.Interface;
using Game.Blueprint;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     BP貼り付けのゴースト一括表示。複数ブロック種を同時にプールから取得する
    ///     Batch ghost display for paste; pulls multiple block kinds from the pool
    /// </summary>
    public class BlueprintPastePreviewController
    {
        private readonly BlockPlacePreviewObjectPool _pool;

        public BlueprintPastePreviewController(Transform parentTransform)
        {
            _pool = new BlockPlacePreviewObjectPool(parentTransform);
        }

        public void UpdatePreview(List<BlueprintPlacementElement> placements, List<bool> placeableFlags)
        {
            _pool.AllUnUse();
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];

                // 実設置と同じ座標変換（グリッド原点→モデル原点）でゴーストを配置する
                // Position ghosts with the same grid-to-model-origin conversion as real placement
                var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placement.Position, placement.Direction, placement.BlockId);
                var rot = placement.Direction.GetRotation();

                var previewObject = _pool.GetObject(placement.BlockId);
                previewObject.SetTransform(pos, rot);
                previewObject.SetPlaceableColor(placeableFlags[i]);
                previewObject.SetActive(true);
            }
        }

        public void Hide()
        {
            _pool.AllUnUse();
        }
    }
}
