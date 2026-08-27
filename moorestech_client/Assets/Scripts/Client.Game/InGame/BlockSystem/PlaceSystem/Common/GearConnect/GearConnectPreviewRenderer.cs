using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect
{
    /// <summary>
    ///     設置予定歯車の各コネクタから、噛み合う相手のコネクタセルへ半透明の直線を描く
    ///     Draws translucent straight lines from each connector of the gear to be placed to the connector cell it meshes with
    /// </summary>
    public class GearConnectPreviewRenderer
    {
        private const string RootName = "GearConnectPreview";
        private const string LineName = "GearConnectLine";
        private const float LineWidth = 0.08f;
        private static readonly Color LineColor = new(0.3f, 0.95f, 0.35f, 0.8f);

        // コネクタ位置はセル座標なのでセル中心へ寄せて線を張る
        // Connector positions are cell coords, so the line is drawn between cell centers
        private static readonly Vector3 CellCenter = new(0.5f, 0.5f, 0.5f);

        private readonly Transform _root;
        private readonly List<LineRenderer> _lines = new();
        private Material _lineMaterial;

        public GearConnectPreviewRenderer()
        {
            _root = new GameObject(RootName).transform;
            _root.gameObject.SetActive(false);
        }

        public void Show(IReadOnlyList<GearConnectPair> pairs)
        {
            _root.gameObject.SetActive(true);

            while (_lines.Count < pairs.Count) _lines.Add(CreateLine());
            for (var i = 0; i < _lines.Count; i++)
            {
                var visible = i < pairs.Count;
                _lines[i].gameObject.SetActive(visible);
                if (!visible) continue;
                _lines[i].SetPosition(0, pairs[i].SelfConnectorCell + CellCenter);
                _lines[i].SetPosition(1, pairs[i].TargetConnectorCell + CellCenter);
            }
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }

        private LineRenderer CreateLine()
        {
            // マテリアルは線ごとに作らず1枚を共有する。設置中は毎フレーム呼ばれるため作り捨ては溜まる
            // One shared material instead of one per line; this runs every frame while placing, so per-line creation would accumulate
            _lineMaterial ??= new Material(Shader.Find("Sprites/Default")) { color = LineColor };

            var line = new GameObject(LineName).AddComponent<LineRenderer>();
            line.transform.SetParent(_root, false);
            line.positionCount = 2;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.useWorldSpace = true;
            line.sharedMaterial = _lineMaterial;
            line.startColor = LineColor;
            line.endColor = LineColor;
            return line;
        }
    }
}
