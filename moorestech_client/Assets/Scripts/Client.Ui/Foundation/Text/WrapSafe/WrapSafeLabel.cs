using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text.WrapSafe
{
    /// <summary>
    /// 狭い幅で折り返して UITK TextCore が固まるのを避けるラベル。本番の Label はすべてこれ（か派生の LocalizedLabel）にする
    /// Label that avoids UITK TextCore freezing when wrapped at narrow widths; every production Label is this (or the derived LocalizedLabel)
    /// </summary>
    /// <remarks>
    /// UITK にはパネルの全 TextElement の測りに割り込む口が無いので、測りを差し替えられる派生で受ける。素の Label を本番で作らないことは WrapSafeLabelConventionTest が見張る。設計書 A-224
    /// UITK offers no hook into every TextElement's measure on a panel, so a subclass that can replace measuring carries it; WrapSafeLabelConventionTest keeps production from creating plain Labels. Design A-224
    /// </remarks>
    public class WrapSafeLabel : Label
    {
        private readonly NarrowWrapGuard _narrowWrapGuard;

        public WrapSafeLabel() : this(string.Empty)
        {
        }

        public WrapSafeLabel(string text) : base(text)
        {
            WhiteSpaceHolds = new InlineWhiteSpaceHolds(this);
            _narrowWrapGuard = new NarrowWrapGuard(this, WhiteSpaceHolds);
        }

        // inline の white-space の持ち主。折り返しを止める派生（行末の約物の詰め）はここを通して止める
        // Owner of the inline white-space; subclasses that stop wrapping (the line-end trim) go through it
        public InlineWhiteSpaceHolds WhiteSpaceHolds { get; }

        protected override Vector2 DoMeasure(float desiredWidth, MeasureMode widthMode, float desiredHeight, MeasureMode heightMode)
        {
            _narrowWrapGuard.EvaluateBeforeMeasure(desiredWidth, widthMode);
            if (!_narrowWrapGuard.FreezesWhenMeasuredAt(desiredWidth, widthMode)) return base.DoMeasure(desiredWidth, widthMode, desiredHeight, heightMode);

            // 折り返すと固まる幅なので 1 行で測り、受けた幅へ収める（nowrap で測ったのと同じ大きさ）
            // Wrapping would freeze at this width, so measure one line and fit it to the constraint (the size nowrap would give)
            var singleLine = base.DoMeasure(float.NaN, MeasureMode.Undefined, desiredHeight, heightMode);
            var width = widthMode == MeasureMode.Exactly ? desiredWidth : Mathf.Min(singleLine.x, desiredWidth);
            return new Vector2(width, singleLine.y);
        }
    }
}
