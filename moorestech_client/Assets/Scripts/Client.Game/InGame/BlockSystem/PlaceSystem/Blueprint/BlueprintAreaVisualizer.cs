using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     ドラッグ中の選択バウンディングボックスを半透明表示する
    ///     Shows the drag-selection bounding box as a translucent cube
    /// </summary>
    public class BlueprintAreaVisualizer
    {
        private readonly GameObject _cube;

        public BlueprintAreaVisualizer()
        {
            _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube.name = "BlueprintAreaVisualizer";
            Object.Destroy(_cube.GetComponent<Collider>());

            // 既存の設置プレビュー材質を複製し選択色を適用する
            // Clone the shared placement preview material and tint it with the selection color
            var renderer = _cube.GetComponent<MeshRenderer>();
            var material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
            material.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.PlaceableColor);
            renderer.sharedMaterial = material;
            _cube.SetActive(false);
        }

        public void Show(Vector3Int min, Vector3Int max)
        {
            // セル境界に合わせて中心とサイズを算出する（各セルは1x1x1）
            // Center and scale from cell bounds; each cell is 1x1x1
            var size = new Vector3(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1);
            _cube.transform.position = new Vector3(min.x, min.y, min.z) + size * 0.5f;
            _cube.transform.localScale = size;
            _cube.SetActive(true);
        }

        public void Hide()
        {
            _cube.SetActive(false);
        }
    }
}
