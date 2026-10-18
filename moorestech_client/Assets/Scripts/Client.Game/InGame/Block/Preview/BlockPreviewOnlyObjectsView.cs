using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;

namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// 表示モードと半径内フラグから各プレビュー専用部品の有効・描画を決める
    /// Decides each preview-only object's active/render state from the display mode and the within-radius flag
    /// </summary>
    public class BlockPreviewOnlyObjectsView
    {
        private readonly List<IPreviewOnlyObject> _previewOnlyObjects;
        private PreviewOnlyObjectsDisplay _display = PreviewOnlyObjectsDisplay.Hidden;
        // 初回評価前は半径内扱い（距離カリングの既定onに揃える）
        // Treated as within range before the first evaluation (matches the culling table's default-on)
        private bool _isWithinPlayerRadius = true;

        public BlockPreviewOnlyObjectsView(List<IPreviewOnlyObject> previewOnlyObjects)
        {
            _previewOnlyObjects = previewOnlyObjects;
            foreach (var previewOnlyObject in _previewOnlyObjects) Apply(previewOnlyObject);
        }

        // 遅れてロードされた部品も現在の状態で表示を揃える
        // A late-loaded object is aligned to the current state right away
        public void Add(IPreviewOnlyObject previewOnlyObject)
        {
            _previewOnlyObjects.Add(previewOnlyObject);
            Apply(previewOnlyObject);
        }

        public void SetDisplay(PreviewOnlyObjectsDisplay display)
        {
            _display = display;
            foreach (var previewOnlyObject in _previewOnlyObjects) Apply(previewOnlyObject);
        }

        // 半径判定が効くのは設置モード表示の時だけなので、それ以外は記録のみ
        // The radius only matters in build-mode display, so otherwise just record it
        public void SetWithinPlayerRadius(bool withinPlayerRadius)
        {
            _isWithinPlayerRadius = withinPlayerRadius;
            // silent-ok: 半径非依存モードは値だけ記録する / 次回の表示モード切替で反映される
            // Radius-independent modes only store the value until the next display-mode change
            if (_display != PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius) return;
            foreach (var previewOnlyObject in _previewOnlyObjects) Apply(previewOnlyObject);
        }

        private void Apply(IPreviewOnlyObject previewOnlyObject)
        {
            var isActive = _display != PreviewOnlyObjectsDisplay.Hidden;
            var isRendered = _display switch
            {
                PreviewOnlyObjectsDisplay.Rendered => true,
                PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius => _isWithinPlayerRadius,
                _ => false,
            };
            previewOnlyObject.SetActive(isActive);
            previewOnlyObject.SetEnableRenderers(isRendered);
        }
    }
}
