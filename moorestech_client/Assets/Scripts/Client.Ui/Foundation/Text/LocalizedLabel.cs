using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// UiTextを持つ言語追従ラベル
    /// Label holding UiText that follows language
    /// </summary>
    /// <remarks>
    /// 言語購読と文が無いときの報告は LocalizedTextBinding に任せる
    /// Language subscription and missing-text reporting are delegated to LocalizedTextBinding
    /// </remarks>
    public class LocalizedLabel : Label
    {
        private readonly LocalizedTextBinding _binding;

        public LocalizedLabel()
        {
            _binding = new LocalizedTextBinding(this);
            _binding.OnResolved.Subscribe(resolved => text = resolved);
        }

        public void SetKey(LocalizationKey key)
        {
            SetText(new UiText(key));
        }

        public void SetText(UiText text)
        {
            _binding.SetText(text);
        }
    }
}
