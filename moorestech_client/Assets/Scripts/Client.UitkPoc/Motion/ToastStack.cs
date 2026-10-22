using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Motion
{
    // 右から滑り込み、一定時間後に消えて高さも畳む通知の積み重ね。enter/exitはUSSのtransitionだけで書く
    // Stack of toasts sliding in from the right, then fading out and collapsing height; enter/exit via USS transitions only
    public class ToastStack : VisualElement
    {
        private const long VisibleMilliseconds = 2600;
        private int _serial;

        public ToastStack()
        {
            AddToClassList("mo-toast-stack");
            pickingMode = PickingMode.Ignore;
        }

        public void Push(string message, Texture2D icon)
        {
            var toast = new VisualElement();
            toast.AddToClassList("mo-toast");
            toast.AddToClassList("mo-toast--entering");
            var iconElement = new VisualElement();
            iconElement.AddToClassList("mo-toast__icon");
            iconElement.style.backgroundImage = icon;
            toast.Add(iconElement);
            toast.Add(new Label($"{message} #{++_serial}"));
            Insert(0, toast);

            // 1フレーム後にenteringを外すと、transitionが初期位置から走る
            // Removing "entering" a frame later lets the transition run from the start pose
            toast.schedule.Execute(() => toast.RemoveFromClassList("mo-toast--entering")).ExecuteLater(16);
            toast.schedule.Execute(() => Leave(toast)).ExecuteLater(VisibleMilliseconds);
        }

        private static void Leave(VisualElement toast)
        {
            // auto高さはtransitionできないので、いまの高さを数値で固定してから0へ縮める
            // auto height cannot transition, so pin the current height in px before shrinking to zero
            toast.style.height = toast.resolvedStyle.height;
            toast.AddToClassList("mo-toast--leaving");
            toast.schedule.Execute(() => toast.style.height = 0).ExecuteLater(16);
            toast.schedule.Execute(toast.RemoveFromHierarchy).ExecuteLater(450);
        }
    }
}
