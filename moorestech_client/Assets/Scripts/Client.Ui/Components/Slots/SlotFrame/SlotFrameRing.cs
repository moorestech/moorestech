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

            RegisterCallback<CustomStyleResolvedEvent>(_ => ApplyBevelGeometry());
        }

        private void ApplyBevelGeometry()
        {
            // 読めないときは前の形のまま（LogError で開発者が気づく）
            // When unreadable keep the previous shape (the LogError tells the developer)
            if (!TryReadGrowsInward(out var growsInward)) return;
            for (var i = 0; i < _bands.Length; i++)
            {
                if (!UssCustomStyleReader.TryReadPixels(customStyle, BevelWidthProperties[i], out var width)) return;

                // outset は幅ぶん箱の外へ、inset は箱の内側に帯を置く（Web の spread と同じ形）
                // outset puts the band one width outside the box, inset keeps it inside (the same shape as the Web spread)
                var offset = growsInward ? 0f : -width;
                var band = _bands[i];
                band.style.left = offset;
                band.style.top = offset;
                band.style.right = offset;
                band.style.bottom = offset;
                band.style.borderLeftWidth = width;
                band.style.borderTopWidth = width;
                band.style.borderRightWidth = width;
                band.style.borderBottomWidth = width;
            }
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
