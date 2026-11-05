using Client.Ui.State.Toast;
using UnityEngine.UIElements;

namespace Client.Ui.Components
{
    /// <summary>
    /// トースト 1 件の小窓。GamePanel と同じ面に種別の左帯を付ける（D-17）。文は呼び出し側で解決済み
    /// One toast window: the GamePanel face with a variant-colored left strip (D-17); the text arrives already resolved
    /// </summary>
    public sealed class ToastItem : VisualElement
    {
        public ToastItem(GameUiToast toast, StyleSheet sheet)
        {
            styleSheets.Add(sheet);
            AddToClassList("toast-item");
            AddToClassList(toast.Variant == GameUiToastVariant.Error ? "toast-item--error" : "toast-item--info");

            // 押せる物を持たないので、下のゲーム画面への入力を遮らない
            // It has nothing clickable, so it never blocks input to the game below
            pickingMode = PickingMode.Ignore;

            var text = new Label(toast.Text) { pickingMode = PickingMode.Ignore };
            text.AddToClassList("toast-item__text");
            Add(text);
        }
    }
}
