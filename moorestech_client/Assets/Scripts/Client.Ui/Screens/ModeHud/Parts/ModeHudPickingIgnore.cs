using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 部分木の要素を全部拾わなくする（Web の pointer-events: none は子へ継承されるが UITK の pickingMode は継承されないため）。2 機能目が出たら Foundation へ昇格を依頼する
    /// Makes every element in a subtree unpickable (Web pointer-events: none inherits, UITK pickingMode does not); request promotion to Foundation on a second use
    /// </summary>
    public static class ModeHudPickingIgnore
    {
        public static void Apply(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }
    }
}
