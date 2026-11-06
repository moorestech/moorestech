using System;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 送信・OK・キャンセル用の押しボタン（Mantine Button の写し）。文言・色の系統・大きさを Set で受け、押下を OnClicked で出す
    /// Push button for send / OK / cancel (copy of Mantine Button); takes label, colour family and size via Set and emits presses via OnClicked
    /// </summary>
    /// <remarks>
    /// 拡張点: Web で新しい variant・size を使い始めたら、列挙とクラスを 1 組ずつ足す。外側の余白・幅（fullWidth）は置く側の画面 USS が付ける
    /// Extension: when the Web starts using a new variant or size, add one enum value and class pair; outer margin and width (fullWidth) belong to the placing screen's USS
    /// </remarks>
    public sealed class FormButton : Button
    {
        public const string UssClassName = "form-button";
        public const string LabelUssClassName = "form-button__label";
        public const string FilledUssClassName = "form-button--filled";
        public const string DangerUssClassName = "form-button--danger";
        public const string SubtleUssClassName = "form-button--subtle";
        public const string ExtraLargeUssClassName = "form-button--xl";

        private readonly Subject<Unit> _clicked = new();
        private readonly LocalizedLabel _label;

        public IObservable<Unit> OnClicked => _clicked;

        public FormButton(string testid, UiText label, FormButtonVariant variant)
        {
            name = testid;
            AddToClassList(UssClassName);

            // GameUi は入力欄以外にフォーカスを持たせない
            // GameUi never lets anything but text fields hold focus
            focusable = false;

            // 文言は言語変更で解決し直すラベルに持たせ、押下を横取りさせない
            // The label re-resolves on language change and never steals the press
            _label = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(LabelUssClassName);
            // Web は 600。600 の字形は無く、実測で 600 に近い 500 の字形を当てる
            // The Web uses 600; no 600 face exists, so the 500 face, measured closer to 600, is used
            _label.AddToClassList(UiTypographyClass.TextMedium); // look-exception: LX-10
            Add(_label);
            SetLabel(label);
            SetVariant(variant);

            // 押下は UniRx で外へ出す
            // Presses leave through UniRx
            clicked += () => _clicked.OnNext(Unit.Default);
        }

        public void SetLabel(UiText label)
        {
            _label.SetText(label);
        }

        public void SetVariant(FormButtonVariant variant)
        {
            // 色の系統はいつも 1 つだけ付ける（Web の variant と color の組と同じ）
            // Exactly one colour family at a time, matching the Web variant/color pair
            EnableInClassList(FilledUssClassName, variant == FormButtonVariant.Filled);
            EnableInClassList(DangerUssClassName, variant == FormButtonVariant.Danger);
            EnableInClassList(SubtleUssClassName, variant == FormButtonVariant.Subtle);
        }

        public void SetSize(FormButtonSize size)
        {
            EnableInClassList(ExtraLargeUssClassName, size == FormButtonSize.ExtraLarge);
        }
    }
}
