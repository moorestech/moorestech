using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// トースト 1 件の小窓。Web の Mantine Notification の暗色に種別の色帯を付ける。文は呼び出し側で解決済み
    /// One toast window: the Web Mantine Notification dark look with a tone-colored strip; the text arrives already resolved
    /// </summary>
    public sealed class ToastItem : VisualElement
    {
        public ToastItem(string text, ToastItemTone tone)
        {
            AddToClassList("toast-item");
            AddToClassList(tone == ToastItemTone.Error ? "toast-item--error" : "toast-item--info");

            // 押せる物を持たないので、下のゲーム画面への入力を遮らない
            // It has nothing clickable, so it never blocks input to the game below
            pickingMode = PickingMode.Ignore;

            // 種別の色帯は Mantine の ::before にあたる飾り
            // The tone strip is the decoration standing in for Mantine's ::before
            var strip = new VisualElement { pickingMode = PickingMode.Ignore };
            strip.AddToClassList("toast-item__strip");
            Add(strip);

            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("toast-item__text");
            Add(label);
        }
    }
}
