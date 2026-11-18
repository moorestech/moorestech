using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// 部分木の要素を全部拾わなくする（Web の pointer-events: none は子へ継承されるが UITK の pickingMode は継承されないため）
    /// Makes every element in a subtree unpickable (Web pointer-events: none inherits, UITK pickingMode does not)
    /// </summary>
    /// <remarks>
    /// 子を足すたびに掛け直す（後から足した子は拾える既定のまま）。常駐 HUD は組み終えた後と行を作り直した後に呼ぶ
    /// Reapply whenever children are added (later children keep the pickable default); resident HUDs call it after building and after rebuilding rows
    /// </remarks>
    public static class UiPickingIgnore
    {
        public static void ApplyToSubtree(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }
    }
}
