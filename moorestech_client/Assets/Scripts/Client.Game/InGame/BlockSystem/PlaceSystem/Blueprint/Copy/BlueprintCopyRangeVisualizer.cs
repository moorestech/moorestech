using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     始点（赤）・終点（緑）のセルと半透明の範囲を表示する
    ///     Shows red start and green end cells with a translucent range box
    /// </summary>
    public class BlueprintCopyRangeVisualizer
    {
        private static readonly Color EndMarkerColor = new(0.3f, 0.85f, 0.35f, 1f);
        private readonly GameObject _startMarker;
        private readonly GameObject _endMarker;
        private readonly GameObject _rangeBox;

        public BlueprintCopyRangeVisualizer()
        {
            _startMarker = CreateCube("BlueprintCopyStartMarker", MaterialConst.NotPlaceableColor);
            _endMarker = CreateCube("BlueprintCopyEndMarker", EndMarkerColor);
            _rangeBox = CreateCube("BlueprintCopyRangeBox", MaterialConst.PlaceableColor);
        }

        public void ShowSelectingStart(Vector3Int hoverCell)
        {
            PlaceCell(_startMarker, hoverCell);
            _endMarker.SetActive(false);
            _rangeBox.SetActive(false);
        }

        public void ShowSelectingEnd(Vector3Int startCell, Vector3Int hoverCell, Vector3Int min, Vector3Int max)
        {
            PlaceCell(_startMarker, startCell);
            PlaceCell(_endMarker, hoverCell);
            PlaceBox(min, max);
        }

        public void ShowAwaitingName(Vector3Int startCell, Vector3Int endCell, Vector3Int min, Vector3Int max)
        {
            ShowSelectingEnd(startCell, endCell, min, max);
        }

        public void ShowStartOnly(Vector3Int startCell)
        {
            PlaceCell(_startMarker, startCell);
            _endMarker.SetActive(false);
            _rangeBox.SetActive(false);
        }

        public void HideAll()
        {
            _startMarker.SetActive(false);
            _endMarker.SetActive(false);
            _rangeBox.SetActive(false);
        }

        private static void PlaceCell(GameObject cube, Vector3Int cell)
        {
            cube.transform.position = cell + new Vector3(0.5f, 0.5f, 0.5f);
            cube.transform.localScale = Vector3.one;
            cube.SetActive(true);
        }

        private void PlaceBox(Vector3Int min, Vector3Int max)
        {
            // セル境界から中心とサイズを計算する
            // Calculate center and size from cell boundaries
            var size = new Vector3(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1);
            _rangeBox.transform.position = new Vector3(min.x, min.y, min.z) + size * 0.5f;
            _rangeBox.transform.localScale = size;
            _rangeBox.SetActive(true);
        }

        private static GameObject CreateCube(string name, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Object.Destroy(cube.GetComponent<Collider>());

            // 設置プレビュー材質を複製して色を付ける
            // Clone the placement preview material and tint it
            var material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
            material.SetColor(MaterialConst.PreviewColorPropertyName, color);
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            cube.SetActive(false);
            return cube;
        }
    }
}
