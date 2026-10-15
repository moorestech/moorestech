using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using UnityEngine;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    ///     通常設置系（単体・ドラッグ・ベルト・橋脚）のプレビューの位置と可否色を録画用に読む
    ///     Reads positions and placeable colors of the normal placement previews (single, drag, belt, pier) for recordings
    /// </summary>
    public static class PlacementPreviewProbe
    {
        public static List<PlacementPreviewSnapshot> ActiveBlockPreviews()
        {
            var result = new List<PlacementPreviewSnapshot>();
            var controller = Object.FindFirstObjectByType<PlacementPreviewBlockGameObjectController>(FindObjectsInactive.Include);
            if (controller == null || !controller.gameObject.activeInHierarchy) return result;

            // 使用中のプールだけがアクティブなので、アクティブな子だけを数える
            // Only pooled objects in use are active, so count active children only
            foreach (Transform child in controller.transform)
            {
                if (child.gameObject.activeSelf) result.Add(new PlacementPreviewSnapshot(child.position, BlueprintPasteGhostProbe.ReadPreviewColor(child.gameObject)));
            }
            return result;
        }
    }

    public readonly struct PlacementPreviewSnapshot
    {
        public readonly Vector3 Position;
        public readonly Color Color;

        public PlacementPreviewSnapshot(Vector3 position, Color color)
        {
            Position = position;
            Color = color;
        }

        public bool IsPlaceable => BlueprintPasteGhostProbe.IsPlaceableColor(Color);
        public bool IsNotPlaceable => BlueprintPasteGhostProbe.IsNotPlaceableColor(Color);
    }
}
