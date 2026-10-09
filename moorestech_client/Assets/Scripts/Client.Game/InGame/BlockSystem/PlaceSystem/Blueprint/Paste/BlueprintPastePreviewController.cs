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

        public BlueprintPastePreviewController(Transform parentTransform)
        {
            _pool = new BlockPlacePreviewObjectPool(parentTransform);
        }

        public IReadOnlyList<IReadOnlyList<BlockPreviewObject>> UpdatePreview(BlueprintPastePlan plan)
        {
            _pool.AllUnUse();
            var ghosts = new List<IReadOnlyList<BlockPreviewObject>>();
            foreach (var copy in plan.Copies)
            {
                var copyGhosts = new List<BlockPreviewObject>();
                for (var i = 0; i < copy.Draft.Elements.Count; i++)
                {
                    var placement = copy.Draft.Elements[i];
                    // 実設置の座標変換で配置しBP全体の判定を反映する
                    // Use the real placement transform and the whole-copy judgement
                    var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placement.Position, placement.Direction, placement.BlockId);
                    var previewObject = _pool.GetObject(placement.BlockId);
                    previewObject.SetTransform(pos, placement.Direction.GetRotation());
                    previewObject.SetPlaceableColor(copy.IsPlaced && copy.Draft.NonOverlapFlags[i]);
                    previewObject.SetActive(true);
                    copyGhosts.Add(previewObject);
                }
                ghosts.Add(copyGhosts);
            }
            return ghosts;
        }

        public void Hide()
        {
            _pool.AllUnUse();
        }
    }
}
