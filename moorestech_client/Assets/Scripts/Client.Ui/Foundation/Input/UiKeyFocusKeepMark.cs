using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// 入力欄でなくてもフォーカスを保ってよい要素の印（キーで操作するボタンだけに付ける。GameUiInputRules が外さない）
    /// Mark for non-text-field elements allowed to keep focus (key-operated buttons only; GameUiInputRules leaves them focused)
    /// </summary>
    public static class UiKeyFocusKeepMark
    {
        private const string ClassName = "ui-key-focus-keep";

        public static void Mark(VisualElement element)
        {
            element.AddToClassList(ClassName);
        }

        public static bool IsMarked(VisualElement element)
        {
            return element != null && element.ClassListContains(ClassName);
        }
    }
}
