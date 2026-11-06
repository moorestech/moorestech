using UnityEngine;

namespace Client.Ui.Foundation.Tooltip
{
    /// <summary>
    /// ツールチップの置き場所。Mantine Tooltip の既定（position top・offset 5・flip・shift の padding 8）を写す
    /// Tooltip placement copying the Mantine Tooltip defaults (position top, offset 5, flip, shift padding 8)
    /// </summary>
    public static class HoverTooltipPlacement
    {
        // Mantine の offset と shift の padding は Portal の中の実画面 px（--ui-scale を掛けない。padding は use-tooltip.mjs の shift({ padding: 8 })）
        // The Mantine offset and shift padding are real-screen px inside the portal (no --ui-scale; padding is shift({ padding: 8 }) in use-tooltip.mjs)
        public const float OffsetScreenPx = 5f;
        public const float ShiftPaddingScreenPx = 8f;

        public static Vector2 Place(Rect target, Vector2 faceSize, Rect bounds, float offset, float shiftPadding)
        {
            // 対象の上に中央をそろえ、入らなければ下へ反転する
            // Center above the target and flip below when it does not fit
            var x = target.center.x - faceSize.x * 0.5f;
            var y = target.yMin - offset - faceSize.y;
            if (y < bounds.yMin) y = target.yMax + offset;

            // 横は枠から余白の分だけ内へ押し戻す（余白を除いた枠より広い顔は左の余白にそろえる。floating-ui の clamp と同じ）
            // Shift horizontally to the padding inside the bounds (a face wider than the padded bounds aligns to the left padding, as floating-ui clamps)
            var minX = bounds.xMin + shiftPadding;
            x = Mathf.Clamp(x, minX, Mathf.Max(minX, bounds.xMax - shiftPadding - faceSize.x));
            return new Vector2(x, y);
        }
    }
}
