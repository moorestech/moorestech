using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController
{
    /// <summary>
    ///     接続予定チェーンの共通プレビュー
    ///     Renders shared previews of planned chains
    /// </summary>
    internal class GearChainPreviewLine
    {
        private readonly GameObject _root;
        private readonly LineRenderer _first;
        private readonly LineRenderer _second;

        public GearChainPreviewLine(Transform parent)
        {
            _root = new GameObject("GearChainPreviewLine");
            _root.transform.SetParent(parent, false);
            _first = CreateLine("First");
            _second = CreateLine("Second");

            #region Internal

            LineRenderer CreateLine(string name)
            {
                var lineObject = new GameObject(name);
                lineObject.transform.SetParent(_root.transform, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startWidth = 0.05f;
                line.endWidth = 0.05f;
                line.positionCount = 2;
                return line;
            }

            #endregion
        }

        public void Draw(Vector3 start, Vector3 end, bool placeable)
        {
            _root.SetActive(true);
            // 延長プレビューと同じ幅と間隔の二本線を使う
            // Match the extension preview's two-line width and spacing
            var right = Vector3.Cross(Vector3.up, (end - start).normalized).normalized;
            if (right == Vector3.zero) right = Vector3.right;
            var offset = right * 0.05f;
            var color = placeable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
            SetLine(_first, start + offset, end + offset);
            SetLine(_second, start - offset, end - offset);

            #region Internal

            void SetLine(LineRenderer line, Vector3 a, Vector3 b)
            {
                line.SetPosition(0, a);
                line.SetPosition(1, b);
                line.startColor = color;
                line.endColor = color;
            }

            #endregion
        }

        public void SetActive(bool active) => _root.SetActive(active);
    }
}
