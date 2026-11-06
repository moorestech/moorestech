using System.Globalization;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// UiTextを持つ言語追従ラベル
    /// Label holding UiText that follows language
    /// </summary>
    /// <remarks>
    /// 言語購読と文が無いときの報告は LocalizedTextBinding に任せる。
    /// USS で --localized-label-line-height を宣言したラベルは、複数行の行送りをその px にする（USS に line-height が無いため <line-height> を前に付ける）
    /// Language subscription and missing-text reporting are delegated to LocalizedTextBinding.
    /// A label whose USS declares --localized-label-line-height spaces its lines by that px (USS has no line-height, so a <line-height> tag is prefixed)
    /// </remarks>
    public class LocalizedLabel : Label
    {
        private const string LineHeightProperty = "--localized-label-line-height";
        private static readonly CustomStyleProperty<string> LineHeightStyle = new(LineHeightProperty);

        private readonly LocalizedTextBinding _binding;
        private string _resolvedText;
        private string _lineHeightTag = string.Empty;

        public LocalizedLabel()
        {
            _binding = new LocalizedTextBinding(this);
            _binding.OnResolved.Subscribe(resolved =>
            {
                _resolvedText = resolved;
                ShowResolvedText();
            });
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        public void SetKey(LocalizationKey key)
        {
            SetText(new UiText(key));
        }

        public void SetText(UiText text)
        {
            _binding.SetText(text);
        }

        private void OnCustomStyleResolved()
        {
            _lineHeightTag = ReadLineHeightTag();
            ShowResolvedText();

            #region Internal

            string ReadLineHeightTag()
            {
                // silent-ok: 行送りを指定しないラベルが大半 / 指定した部品はその部品のテスト（TooltipFaceTest）が行送りを確かめる
                if (!customStyle.TryGetValue(LineHeightStyle, out var value)) return string.Empty;

                // 宣言したのに長さとして読めなければ開発者に知らせ、行送りは既定に戻す
                // A declared but unreadable value is reported and the pitch falls back to the default
                if (!UssTokenValue.TryParsePixels(value, out var pixels))
                {
                    Debug.LogError($"[LocalizedLabel] {LineHeightProperty} is not a resolved length (got '{value}'): {name}");
                    return string.Empty;
                }
                return $"<line-height={pixels.ToString(CultureInfo.InvariantCulture)}px>";
            }

            #endregion
        }

        private void ShowResolvedText()
        {
            // silent-ok: 文が来るまで出さない / 載って 1 フレーム後も無ければ LocalizedTextBinding が LogError を出す
            if (_resolvedText == null) return;
            text = _lineHeightTag + _resolvedText;
        }
    }
}
