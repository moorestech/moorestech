using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;

namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// - 表示モードと半径で描画制御
    /// - 部品は描画する時だけ有効にする（ADR 0101）
    /// - Control rendering by display mode and radius
    /// - Parts are active only while rendered (ADR 0101)
    /// </summary>
    public class BlockPreviewOnlyObjectsView
    {
        private readonly List<IPreviewOnlyObject> _previewOnlyObjects;
        private PreviewOnlyObjectsDisplay _display = PreviewOnlyObjectsDisplay.Hidden;
        // 初回評価前は半径内扱い（距離カリングの既定onに揃える）
        // Treated as within range before the first evaluation (matches the culling table's default-on)
        private bool _isWithinPlayerRadius = true;
        // 部品へ最後に指示した描画状態。同じなら指示を省き、半径外で隠れたままのブロックには何も呼ばない
        // The rendered state last applied to the parts; skip when unchanged so hidden out-of-radius blocks get no calls
        private bool _appliedRendered;

        public BlockPreviewOnlyObjectsView(List<IPreviewOnlyObject> previewOnlyObjects)
        {
            _previewOnlyObjects = previewOnlyObjects;
            _appliedRendered = IsRendered();
            foreach (var previewOnlyObject in _previewOnlyObjects) Apply(previewOnlyObject, _appliedRendered);
        }

        // 遅れてロードされた部品も現在の状態で表示を揃える
        // A late-loaded object is aligned to the current state right away
        public void Add(IPreviewOnlyObject previewOnlyObject)
        {
            _previewOnlyObjects.Add(previewOnlyObject);
            Apply(previewOnlyObject, _appliedRendered);
        }

        public void SetDisplay(PreviewOnlyObjectsDisplay display)
        {
            _display = display;
            ApplyIfChanged();
        }

        // 半径判定が効くのは設置モード表示の時だけなので、それ以外は記録のみ
        // The radius only matters in build-mode display, so otherwise just record it
        public void SetWithinPlayerRadius(bool withinPlayerRadius)
        {
            _isWithinPlayerRadius = withinPlayerRadius;
            // silent-ok: 半径非依存モードは値だけ記録する / 次回の表示モード切替で反映される
            // Radius-independent modes only store the value until the next display-mode change
            if (_display != PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius) return;
            ApplyIfChanged();
        }

        private void ApplyIfChanged()
        {
            var isRendered = IsRendered();
            if (isRendered == _appliedRendered) return;
            _appliedRendered = isRendered;
            foreach (var previewOnlyObject in _previewOnlyObjects) Apply(previewOnlyObject, isRendered);
        }

        private bool IsRendered()
        {
            return _display switch
            {
                PreviewOnlyObjectsDisplay.Rendered => true,
                PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius => _isWithinPlayerRadius,
                _ => false,
            };
        }

        private static void Apply(IPreviewOnlyObject previewOnlyObject, bool isRendered)
        {
            // 照準は当たり判定を使わないので、描画しない部品は有効にもしない
            // The aim uses no colliders, so a part that is not rendered is not activated either
            previewOnlyObject.SetActive(isRendered);
            previewOnlyObject.SetEnableRenderers(isRendered);
        }
    }
}
