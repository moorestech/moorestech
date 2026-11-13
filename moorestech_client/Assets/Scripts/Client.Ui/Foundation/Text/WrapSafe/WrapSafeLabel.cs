using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text.WrapSafe
{
    /// <summary>
    /// 狭い幅で折り返して UITK TextCore が固まるのを避けるラベル。本番の Label はすべてこれ（か派生の LocalizedLabel）にする
    /// Label that avoids UITK TextCore freezing when wrapped at narrow widths; every production Label is this (or the derived LocalizedLabel)
    /// </summary>
    /// <remarks>
    /// UITK にはパネルの全 TextElement の測りに割り込む口が無いので、測りを差し替えられる派生で受ける。素の Label を本番で作らないことは WrapSafeLabelConventionTest が見張る。
    /// 測りの来ない固定の箱でも字の大きさ・字間の変化に気づけるよう、カスタムプロパティを持つクラスを付けて CustomStyleResolvedEvent を毎回の解決で受ける（Typography.uss）。設計書 A-224
    /// UITK offers no hook into every TextElement's measure on a panel, so a subclass that can replace measuring carries it; WrapSafeLabelConventionTest keeps production from creating plain Labels.
    /// A class carrying a custom property makes every style resolution send CustomStyleResolvedEvent, so a fixed box with no measure still notices font size and spacing changes (Typography.uss). Design A-224
    /// </remarks>
    public class WrapSafeLabel : Label
    {
        private const string StyleResolvedProbeClassName = "wrap-safe-label";

        private readonly NarrowWrapGuard _narrowWrapGuard;

        public WrapSafeLabel() : this(string.Empty)
        {
        }

        public WrapSafeLabel(string text) : base(text)
        {
            AddToClassList(StyleResolvedProbeClassName);
            WhiteSpaceHolds = new InlineWhiteSpaceHolds(this);
            _narrowWrapGuard = new NarrowWrapGuard(this, WhiteSpaceHolds);
        }

        // inline の white-space の持ち主。折り返しを止める派生（行末の約物の詰め）はここを通して止める
        // Owner of the inline white-space; subclasses that stop wrapping (the line-end trim) go through it
        internal InlineWhiteSpaceHolds WhiteSpaceHolds { get; }

        // 文の差し替えを測りの来ない固定の箱でも判定へ届ける（基底の構築中は回避がまだ無く、載る前なので判定は要らない）
        // Route text swaps to the guard even for a fixed box with no measure (during base construction the guard does not exist yet and nothing is attached)
        public override string text
        {
            get => base.text;
            set
            {
                if (value == base.text) return;
                base.text = value;
                // silent-ok: 基底の構築中の文の設定 / 載った後の最初の配置で GeometryChangedEvent か測りが判定する
                if (_narrowWrapGuard == null) return;
                _narrowWrapGuard.OnTextChanged();
            }
        }

        protected override Vector2 DoMeasure(float desiredWidth, MeasureMode widthMode, float desiredHeight, MeasureMode heightMode)
        {
            // 測りの中で inline の white-space を書き換えうる。同じ配置の中で測り直しが 1 回増えるが、狭い幅で折り返す測りを 1 度も走らせないためにここで決める
            // This may rewrite the inline white-space inside a measure; it costs one extra re-measure in the same layout, but deciding here keeps any wrap at a freezing width from ever running
            _narrowWrapGuard.EvaluateBeforeMeasure(desiredWidth, widthMode);
            var measureWidth = _narrowWrapGuard.SafeMeasureWidth(desiredWidth, widthMode);
            if (!(desiredWidth < measureWidth))
            {
                var measured = base.DoMeasure(desiredWidth, widthMode, desiredHeight, heightMode);
                _narrowWrapGuard.RecordMeasure(desiredWidth, widthMode, measured.x);
                return measured;
            }

            // 受けた幅で折り返すと固まるので必要な幅で折り返して測り、幅だけ受けた幅へ収める（箱が狭いままなら NarrowWrapGuard が nowrap にして測り直させる）
            // Wrapping at the constraint freezes, so wrap at the needed width and fit only the width (if the box stays narrow NarrowWrapGuard applies nowrap and it re-measures)
            var safeMeasured = base.DoMeasure(measureWidth, widthMode, desiredHeight, heightMode);
            _narrowWrapGuard.RecordMeasure(measureWidth, widthMode, safeMeasured.x);
            return new Vector2(Mathf.Min(safeMeasured.x, desiredWidth), safeMeasured.y);
        }
    }
}
