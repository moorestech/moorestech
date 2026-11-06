using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// テクスチャ表示と欠け時の代わりの文字（Web の GameIcon）
    /// Shows a texture, or fallback text when missing (the Web GameIcon)
    /// </summary>
    public sealed class GameIcon : VisualElement
    {
        private const string ImageStateClass = "game-icon--image";
        private const string FallbackStateClass = "game-icon--fallback";

        private readonly VisualElement _image;
        private LocalizedLabel _fallbackLabel;

        public GameIcon()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("game-icon");
            _image = new VisualElement { pickingMode = PickingMode.Ignore };
            _image.AddToClassList("game-icon__image");
            Add(_image);
        }

        public void SetIcon(UiIcon icon, GameIconFallback fallback)
        {
            // テクスチャがあれば絵だけを出す
            // With a texture, show only the picture
            if (icon.Texture != null)
            {
                _image.style.backgroundImage = new StyleBackground(icon.Texture);
                EnableInClassList(ImageStateClass, true);
                EnableInClassList(FallbackStateClass, false);
                return;
            }

            // 欠けたら Web の onError と同じく fallback の種類で代わりを決める
            // When missing, pick the substitute by fallback kind as the Web onError does
            _image.style.backgroundImage = StyleKeyword.Null;
            EnableInClassList(ImageStateClass, false);
            switch (fallback)
            {
                case GameIconFallback.IdText:
                    ShowFallback(SlotTexts.IconIdFallback(icon.IdText));
                    break;
                case GameIconFallback.Label:
                    ShowFallback(icon.Label);
                    break;
                case GameIconFallback.None:
                    EnableInClassList(FallbackStateClass, false);
                    break;
            }

            #region Internal

            void ShowFallback(UiText text)
            {
                // キーの無い LocalizedLabel をパネルに載せない（LogError になる）ので初回に作る
                // Create on first use so a key-less LocalizedLabel never sits on the panel (it logs an error)
                if (_fallbackLabel == null)
                {
                    _fallbackLabel = new LocalizedLabel { pickingMode = PickingMode.Ignore };
                    _fallbackLabel.AddToClassList("game-icon__fallback");
                    Add(_fallbackLabel);
                }
                _fallbackLabel.SetText(text);
                EnableInClassList(FallbackStateClass, true);
            }

            #endregion
        }

        public void ClearIcon()
        {
            _image.style.backgroundImage = StyleKeyword.Null;
            EnableInClassList(ImageStateClass, false);
            EnableInClassList(FallbackStateClass, false);
        }
    }
}
