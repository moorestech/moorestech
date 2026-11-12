using System;
using Client.Ui.Foundation.Text.WrapSafe;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// トースト1件の小窓（種別の色帯）
    /// One toast window with a tone strip
    /// </summary>
    public sealed class ToastItem : VisualElement
    {
        public ToastItem(string text, ToastItemTone tone)
        {
            AddToClassList("toast-item");
            AddToClassList(ToneClassName(tone));

            // 入力を遮らない
            // Never blocks input
            pickingMode = PickingMode.Ignore;

            // 色帯はMantineの::before相当
            // Strip stands in for Mantine ::before
            var strip = new VisualElement { pickingMode = PickingMode.Ignore };
            strip.AddToClassList("toast-item__strip");
            Add(strip);

            var label = new WrapSafeLabel(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("toast-item__text");
            Add(label);
        }

        private static string ToneClassName(ToastItemTone tone)
        {
            // 全値を列挙・未知は停止
            // List every value; fail on unknown
            return tone switch
            {
                ToastItemTone.Info => "toast-item--info",
                ToastItemTone.Error => "toast-item--error",
                _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, null),
            };
        }
    }
}
