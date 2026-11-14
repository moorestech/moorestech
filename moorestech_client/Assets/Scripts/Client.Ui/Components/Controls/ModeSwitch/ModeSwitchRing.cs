using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 選択肢の面の外に Web の box-shadow 0 0 0 var(--bevel-1) の輪を描く（幅は USS が --mode-switch-ring-width で受ける）絡まない子（寸法に効かない）
    /// Non-picking child drawing the Web box-shadow 0 0 0 var(--bevel-1) ring outside an option face, width received in USS as --mode-switch-ring-width (no layout effect)
    /// </summary>
    internal sealed class ModeSwitchRing : VisualElement
    {
        private const string BevelProperty = "--mode-switch-ring-width";

        public ModeSwitchRing()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("mode-switch__ring");

            // 倍率だけが変わってもスタイルが解決し直されるので、この経路で物理画素へ組み直る（前例 SlotFrameRing）
            // A scale-only change re-resolves styles, so this path rebuilds against physical pixels (precedent SlotFrameRing)
            RegisterCallback<CustomStyleResolvedEvent>(_ => ApplyRingGeometry());

            #region Internal

            void ApplyRingGeometry()
            {
                // 読めないときは前の形のまま（LogError は Reader が出す）
                // Keep the previous shape when unreadable (the Reader logs the error)
                if (!UssCustomStyleReader.TryReadPixels(customStyle, BevelProperty, out var bevel)) return;

                // UITK の枠線は物理画素の整数に丸まり全面で塗られる。Web は端数の幅を部分被覆で描くので、幅を物理画素へ揃え、足りない被覆を不透明度で写す
                // UITK borders round to whole physical pixels and paint fully; the Web paints the fractional width as partial coverage, so snap the width and carry the missing coverage as opacity
                var pixelsPerPoint = scaledPixelsPerPoint;
                var physicalWidth = bevel * pixelsPerPoint;
                var snappedPixels = Mathf.Max(1f, Mathf.Round(physicalWidth));
                var width = snappedPixels / pixelsPerPoint;
                style.opacity = Mathf.Min(1f, physicalWidth / snappedPixels); // look-exception: LX-3

                // 面の外へ幅だけ出し、角丸は面の角丸（--bevel-1）に広がり（--bevel-1）を足した値
                // Push out by the width; the radius is the face radius (--bevel-1) plus the spread (--bevel-1)
                style.left = -width;
                style.top = -width;
                style.right = -width;
                style.bottom = -width;
                style.borderLeftWidth = width;
                style.borderTopWidth = width;
                style.borderRightWidth = width;
                style.borderBottomWidth = width;
                var radius = bevel * 2f;
                style.borderTopLeftRadius = radius;
                style.borderTopRightRadius = radius;
                style.borderBottomLeftRadius = radius;
                style.borderBottomRightRadius = radius;
            }

            #endregion
        }
    }
}
