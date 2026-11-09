using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// 部分木の要素を全部拾わなくする（Web の pointer-events: none は子へ継承されるが UITK の pickingMode は継承されないため）
    /// Makes every element in a subtree unpickable (Web pointer-events: none inherits, UITK pickingMode does not)
    /// </summary>
    public static class UiSubtreePickingIgnore
    {
        public static void Apply(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }
    }
}
