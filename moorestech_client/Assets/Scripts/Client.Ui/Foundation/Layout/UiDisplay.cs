using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// 要素の出し入れの唯一の口。隠すときはインラインの none、出すときはインラインを外して USS（状態クラスの none を含む）へ戻す（A-245）
    /// The single entry for showing and hiding; hiding sets inline none, showing removes the inline value so USS (including state-class none) applies again (A-245)
    /// </summary>
    public static class UiDisplay
    {
        public static void SetShown(VisualElement element, bool shown)
        {
            // 出すときに Flex を当てると部品の状態クラスの none に勝ってしまうので、Null で USS に任せる
            // Showing with inline Flex would beat a component's state-class none, so Null hands the value back to USS
            element.style.display = shown ? new StyleEnum<DisplayStyle>(StyleKeyword.Null) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        // インラインで隠していないか（配置前でも読める。USS の結果は resolvedStyle で読む）
        // Whether no inline hide is set (readable before layout; read USS results through resolvedStyle)
        public static bool IsShownInline(VisualElement element)
        {
            return element.style.display.keyword == StyleKeyword.Null || element.style.display.value != DisplayStyle.None;
        }
    }
}
