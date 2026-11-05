using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    // USS に gap と :first-child が無いので、2 つ目以降の子に間隔のクラスを付けて Web の Stack gap=md を写す
    // USS lacks gap and :first-child, so children after the first get a spacing class to mirror the Web Stack gap=md
    public static class PauseMenuStack
    {
        public static void Add(VisualElement stack, VisualElement child)
        {
            if (stack.childCount > 0) child.AddToClassList("pause-menu__stack-item--following");
            stack.Add(child);
        }
    }
}
