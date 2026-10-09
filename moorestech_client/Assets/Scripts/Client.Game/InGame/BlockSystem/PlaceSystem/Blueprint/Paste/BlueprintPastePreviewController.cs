using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Game.Block.Interface;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     BP貼り付けゴーストの一括表示・プール取得
    ///     Batch ghost display for paste; pulls multiple block kinds from the pool
    /// </summary>
    public class BlueprintPastePreviewController
    {
        private readonly BlockPlacePreviewObjectPool _pool;
        private readonly BlueprintPasteLinePreview _lines = new();
        private BlueprintPastePlan _lastPlan;

        public BlueprintPastePreviewController(Transform parentTransform)
        {
            _pool = new BlockPlacePreviewObjectPool(parentTransform);
        }

        public void UpdatePreview(BlueprintPastePlan plan)
        {
            // 同じ表示指示ではゴースト再配置と線描画を省く
            // Skip ghost placement and line rendering for identical visual commands
            if (BlueprintPasteVisualState.Matches(_lastPlan, plan)) return;
            _lastPlan = plan;
            _pool.AllUnUse();
            var ghosts = new List<IReadOnlyList<BlockPreviewObject>>();
            foreach (var copy in plan.Copies)
            {
                var copyGhosts = new List<BlockPreviewObject>();
                for (var i = 0; i < copy.Draft.Elements.Count; i++)
                {
                    var placement = copy.Draft.Elements[i];
                    // 実設置座標にBP全体の可否を表示
                    // Show whole-copy judgement at real placement positions.
                    var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placement.Position, placement.Direction, placement.BlockId);
                    var previewObject = _pool.GetObject(placement.BlockId);
                    previewObject.SetTransform(pos, placement.Direction.GetRotation());
                    previewObject.SetPlaceableColor(copy.IsPlaced && copy.Draft.NonOverlapFlags[i]);
                    previewObject.SetActive(true);
                    copyGhosts.Add(previewObject);
                }
                ghosts.Add(copyGhosts);
            }
            _lines.Show(plan, ghosts);
        }

        public void Hide()
        {
            _lastPlan = null;
            _lines.Hide();
            _pool.AllUnUse();
        }
    }
}
