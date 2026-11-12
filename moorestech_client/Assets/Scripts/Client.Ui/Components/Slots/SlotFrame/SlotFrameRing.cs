using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// box-shadow 4 層を border の帯で描く装飾子（寸法に効かない）
    /// Draws the four box-shadow layers as border bands (no layout effect)
    /// </summary>
    internal sealed class SlotFrameRing : VisualElement
    {
        private const string BevelInsetProperty = "--slot-frame-bevel-inset";
        private static readonly string[] BevelWidthProperties = { "--slot-frame-bevel-1", "--slot-frame-bevel-2", "--slot-frame-bevel-3" };

        private readonly VisualElement[] _bands = new VisualElement[BevelWidthProperties.Length];
        private readonly StageScaleReadGuard _scaleReadGuard = new();

        public SlotFrameRing()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("slot-frame__ring");

            // box-shadow は先に書いた層が上に描かれる。c3 → c2 → c1 → 面際の順に足し、後の子を上にする
            // box-shadow paints earlier layers on top; add c3, c2, c1 then the face inset so later children draw above
            for (var layer = BevelWidthProperties.Length; 1 <= layer; layer--)
            {
                var band = new VisualElement { pickingMode = PickingMode.Ignore };
                band.AddToClassList("slot-frame__band");
                band.AddToClassList($"slot-frame__band--{layer}");
                _bands[layer - 1] = band;
                Add(band);
            }
            var faceInset = new VisualElement { pickingMode = PickingMode.Ignore };
            faceInset.AddToClassList("slot-frame__face-inset");
            Add(faceInset);

            // 帯の幅は物理画素へ揃える。パネルの倍率だけが変わってもスタイルが解決し直され、この経路で組み直る（SlotFrameRingScaleTest で確認）
            // Band widths snap to physical pixels; a scale-only panel change re-resolves styles and rebuilds through this path (verified by SlotFrameRingScaleTest)
            RegisterCallback<CustomStyleResolvedEvent>(_ => ApplyBevelGeometry());

            #region Internal

            void ApplyBevelGeometry()
            {
                // 読めないときは前の形のまま（LogError は Reader が出す）
                // Keep the previous shape when unreadable (the Reader logs the error)
                // 倍率を先に読み、旗戻しを見逃さない
                // Read scale first so flag reset is not missed
                if (!TryReadScale(out var pixelsPerPoint)) return;
                if (!TryReadGrowsInward(out var growsInward)) return;
                // 全層を読み切ってから書く。途中で読めない層があっても帯が新旧混在で残らない
                // Read every layer before writing so an unreadable layer never leaves bands half old, half new
                if (!TryReadBevelWidths(out var widths)) return;
                var rawCovered = 0f;
                var snappedCovered = 0f;
                for (var i = 0; i < _bands.Length; i++)
                {
                    var width = widths[i];

                    // Web は先の層が上に描かれ後の層は見える輪だけが残る。UITK の opacity は要素ごとに掛かり重なりが濃く残るので、各帯は前の層の外（inset は内）の見える輪だけを描く
                    // On the Web earlier layers paint on top, leaving only each later layer's visible ring; UITK opacity applies per element and stacks, so each band draws only the ring beyond earlier layers
                    var visibleWidth = rawCovered < width ? SnapToPhysicalPixels(width - rawCovered, pixelsPerPoint) : 0f;
                    var snappedOuter = snappedCovered + visibleWidth;
                    var offset = growsInward ? snappedCovered : -snappedOuter;
                    var band = _bands[i];
                    band.style.left = offset;
                    band.style.top = offset;
                    band.style.right = offset;
                    band.style.bottom = offset;
                    band.style.borderLeftWidth = visibleWidth;
                    band.style.borderTopWidth = visibleWidth;
                    band.style.borderRightWidth = visibleWidth;
                    band.style.borderBottomWidth = visibleWidth;
                    rawCovered = Mathf.Max(rawCovered, width);
                    snappedCovered = snappedOuter;
                }
            }

            bool TryReadScale(out float scale)
            {
                // 倍率は Measure だけが読む
                // Only StageScaleMeasure reads the scale; warn once on entry
                return _scaleReadGuard.TryRead(panel, "SlotFrameRing", "the bevel bands keep their previous shape", out scale);
            }

            bool TryReadBevelWidths(out float[] widths)
            {
                widths = new float[BevelWidthProperties.Length];
                for (var i = 0; i < widths.Length; i++)
                {
                    if (!UssCustomStyleReader.TryReadPixels(customStyle, BevelWidthProperties[i], out widths[i])) return false;
                }
                return true;
            }

            float SnapToPhysicalPixels(float width, float pixelsPerPoint)
            {
                // 端数の帯は辺ごとに別々に丸まり、面への食い込み・帯の消失・隙間が混ざる。見える幅を物理画素の整数倍（最低 1）に揃え、どの辺も同じ太さにする
                // Fractional bands round per edge and mix face intrusions, vanished bands and gaps; snap each visible width to whole physical pixels (at least 1) so every side matches
                // 外縁でなく見える幅を丸めるので、同じ幅の帯は倍率 2 でも同じ物理画素数になる（外縁を丸めると 2+1 に崩れる）
                // Rounding visible widths rather than outer edges keeps equal bands equal at scale 2 (outer-edge rounding gives 2+1)
                return Mathf.Max(1f, Mathf.Round(width * pixelsPerPoint)) / pixelsPerPoint; // look-exception: LX-22
            }

            bool TryReadGrowsInward(out bool growsInward)
            {
                growsInward = false;
                if (!UssCustomStyleReader.TryReadKeyword(customStyle, BevelInsetProperty, out var keyword)) return false;
                switch (keyword)
                {
                    case "inset":
                        growsInward = true;
                        return true;
                    case "outset":
                        return true;
                }
                Debug.LogError($"[SlotFrameRing] '{BevelInsetProperty}' must be inset or outset but was '{keyword}'");
                return false;
            }

            #endregion
        }
    }
}
