using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox
{
    /// <summary>
    ///     始点（赤）・終点（緑）のセルと半透明の範囲箱を表示する。名前接頭辞と範囲色は利用側が決める
    ///     Shows red start and green end cells with a translucent range box; the caller picks the name prefix and range color
    /// </summary>
    public class CellRangeBoxVisualizer
    {
        private static readonly Color EndMarkerColor = new(0.3f, 0.85f, 0.35f, 1f);
        private readonly GameObject _startMarker;
        private readonly GameObject _endMarker;
        private readonly GameObject _rangeBox;

        public CellRangeBoxVisualizer(string objectNamePrefix, Color rangeColor)
        {
            _startMarker = CreateCube(objectNamePrefix + "StartMarker", MaterialConst.NotPlaceableColor);
            _endMarker = CreateCube(objectNamePrefix + "EndMarker", EndMarkerColor);
            _rangeBox = CreateCube(objectNamePrefix + "RangeBox", rangeColor);

            #region Internal

            GameObject CreateCube(string name, Color color)
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

            #endregion
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
            // セル境界から中心とサイズを計算する
            // Calculate center and size from cell boundaries
            var bounds = CellRangeBox.ToWorldBounds(min, max);
            _rangeBox.transform.position = bounds.center;
            _rangeBox.transform.localScale = bounds.size;
            _rangeBox.SetActive(true);
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
    }
}
