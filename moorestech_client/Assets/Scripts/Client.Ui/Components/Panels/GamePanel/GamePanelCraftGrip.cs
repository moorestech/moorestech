using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// craft の右下グリップ。右下の直角三角を単色で塗り、つまみの分だけ右下へずらす（Web の .craft::after）
    /// Craft bottom-right grip; fills one right-angle triangle in a single color, shifted down-right by the knob (the Web .craft::after)
    /// </summary>
    internal sealed class GamePanelCraftGrip : VisualElement
    {
        private Color _gripColor;
        private float _offset;
        private bool _hasTokens;

        public GamePanelCraftGrip()
        {
            AddToClassList("game-panel__craft-grip");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawGrip;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-craft-grip-face", out _gripColor);
            var hasOffset = UssCustomStyleReader.TryReadPixels(customStyle, "--game-panel-craft-grip-offset", out _offset);
            _hasTokens = hasColor && hasOffset;
            MarkDirtyRepaint();
        }

        private void DrawGrip(MeshGenerationContext context)
        {
            // silent-ok: トークンが読めない間は描かない / UssCustomStyleReader が読めなかったトークン名を LogError で出す
            if (!_hasTokens) return;

            // clip-path: polygon(100% 0, 100% 100%, 0 100%) を translate(offset, offset) した 3 点を時計回りに張る
            // Wind the three points of clip-path: polygon(100% 0, 100% 100%, 0 100%) shifted by translate(offset, offset), clockwise
            var size = contentRect.size;
            var mesh = context.Allocate(3, 3);
            mesh.SetNextVertex(new Vertex { position = new Vector3(size.x + _offset, _offset, Vertex.nearZ), tint = _gripColor });
            mesh.SetNextVertex(new Vertex { position = new Vector3(size.x + _offset, size.y + _offset, Vertex.nearZ), tint = _gripColor });
            mesh.SetNextVertex(new Vertex { position = new Vector3(_offset, size.y + _offset, Vertex.nearZ), tint = _gripColor });
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(1);
            mesh.SetNextIndex(2);
        }
    }
}
