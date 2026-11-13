using System;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// Mantine Button 相当の押しボタン。文言・系統・大きさを Set で受ける
    /// Mantine Button-like push button; takes label, variant and size via Set
    /// </summary>
    /// <remarks>
    /// 拡張点: 新 variant・size は列挙とクラスを足す。外側の余白は置く側が付ける
    /// Extension: add enum and class for a new variant or size; the placer adds outer margin
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
            // Web は 600 だが 1 字形しか読まないので、500 のクラスと同じく親の字形を継ぐ（設計書 A-220）
            // The Web asks for 600 but loads one face, so like the 500 class it keeps the inherited face (design A-220)
            _label.AddToClassList(UiTypographyClass.TextMedium);
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
