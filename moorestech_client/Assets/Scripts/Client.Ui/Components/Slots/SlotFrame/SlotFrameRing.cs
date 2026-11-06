using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// SlotFrame の box-shadow 4 層（面際 1・ベベル 3）を border の帯で描く装飾子。寸法に効かない
    /// Decorator drawing the SlotFrame four box-shadow layers (face inset and three bevels) as border bands; it never affects layout
    /// </summary>
    public sealed class SlotFrameRing : VisualElement
    {
        private const string BevelInsetProperty = "--slot-frame-bevel-inset";
        private static readonly string[] BevelWidthProperties = { "--slot-frame-bevel-1", "--slot-frame-bevel-2", "--slot-frame-bevel-3" };

        private readonly VisualElement[] _bands = new VisualElement[3];

        public SlotFrameRing()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("slot-frame__ring");

            // box-shadow は先に書いた層が上に描かれる。c3 → c2 → c1 → 面際の順に足し、後の子を上にする
            // box-shadow paints earlier layers on top; add c3, c2, c1 then the face inset so later children draw above
            for (var layer = 3; layer >= 1; layer--)
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

            // 帯の幅は物理画素へ揃えるので、パネルの倍率が変わり得る配置の変化でも組み直す
            // Band widths snap to physical pixels, so rebuild on layout changes too, where the panel scale may have changed
            RegisterCallback<CustomStyleResolvedEvent>(_ => ApplyBevelGeometry());
            RegisterCallback<GeometryChangedEvent>(_ => ApplyBevelGeometry());
        }

        private void ApplyBevelGeometry()
        {
            // 読めないときは前の形のまま（LogError で開発者が気づく）
            // When unreadable keep the previous shape (the LogError tells the developer)
            if (!TryReadGrowsInward(out var growsInward)) return;
            var pixelsPerPoint = scaledPixelsPerPoint;
            var rawCovered = 0f;
            var snappedCovered = 0f;
            for (var i = 0; i < _bands.Length; i++)
            {
                if (!UssCustomStyleReader.TryReadPixels(customStyle, BevelWidthProperties[i], out var width)) return;

                // Web は先の層が上に描かれ後の層は見える輪だけが残る。UITK の opacity は要素ごとに掛かり重なりが濃く残るので、各帯は前の層の外（inset は内）の見える輪だけを描く
                // On the Web earlier layers paint on top, leaving only each later layer's visible ring; UITK opacity applies per element and stacks, so each band draws only the ring beyond earlier layers
                var snappedOuter = width > rawCovered ? Mathf.Max(SnapToPhysicalPixels(width, pixelsPerPoint), snappedCovered + 1f / pixelsPerPoint) : snappedCovered;
                var visibleWidth = snappedOuter - snappedCovered;
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

        private static float SnapToPhysicalPixels(float width, float pixelsPerPoint)
        {
            // 端数の帯は辺ごとに別々に丸まり、面への食い込み・帯の消失・隙間が混ざる。物理画素の整数倍（最低 1）に揃え、どの辺も同じ太さにする
            // Fractional bands round per edge and mix face intrusions, vanished bands and gaps; snap to whole physical pixels (at least 1) so every side matches
            return Mathf.Max(1f, Mathf.Round(width * pixelsPerPoint)) / pixelsPerPoint; // look-exception: LX-22
        }

        private bool TryReadGrowsInward(out bool growsInward)
        {
            // 読めないときの LogError は UssCustomStyleReader が出す
            // UssCustomStyleReader logs the error when it cannot read the value
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
    }
}
