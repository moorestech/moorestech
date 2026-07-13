using TMPro;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.UIState
{
    public static class TextInputFocusProvider
    {
        public static bool IsFocused()
        {
            // 選択入力欄だけ視点入力を抑止
            // Suppress view input only for the focused selected field
            var selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && selected.TryGetComponent<TMP_InputField>(out var inputField) && inputField.isFocused;
        }
    }
}
