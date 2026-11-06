using Client.Ui.Foundation.Icons;
using Client.Ui.State.Text;
using UnityEngine;

namespace Client.Tests.Ui.Components.Slots
{
    /// <summary>
    /// スロット族の L1 が使う表示素材。テクスチャは 1 枚を使い回す
    /// Display material for the slot-family L1 tests; one texture is shared
    /// </summary>
    internal static class SlotTestIcons
    {
        private static Texture2D _sharedTexture;

        public static Texture2D SharedTexture => _sharedTexture ??= new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };

        public static UiIcon WithTexture(string idText, string name)
        {
            return new UiIcon(SharedTexture, idText, UiText.FromVerbatim(name));
        }

        public static UiIcon Missing(string idText, string name)
        {
            return new UiIcon(null, idText, UiText.FromVerbatim(name));
        }
    }
}
