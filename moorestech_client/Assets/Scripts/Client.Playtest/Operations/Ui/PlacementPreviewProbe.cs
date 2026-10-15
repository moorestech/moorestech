using System.Collections.Generic;
using Client.Common;
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
                if (child.gameObject.activeSelf) result.Add(new PlacementPreviewSnapshot(child.position, ReadPreviewColor(child.gameObject)));
            }
            return result;
        }

        public static Color ReadPreviewColor(GameObject preview)
        {
            // 可否色を持つ描画材質だけを読む
            // Read only renderer materials carrying judgement colors
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null && material.HasProperty(MaterialConst.PreviewColorPropertyName)) return material.GetColor(MaterialConst.PreviewColorPropertyName);
            }
            return Color.clear;
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
        public Vector3Int Cell => Vector3Int.RoundToInt(Position);
    }
}
