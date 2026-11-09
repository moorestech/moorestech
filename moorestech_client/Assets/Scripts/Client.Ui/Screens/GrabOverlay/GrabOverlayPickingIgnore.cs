using UnityEngine.UIElements;

namespace Client.Ui.Screens.GrabOverlay
{
    /// <summary>
    /// 部分木の要素を全部拾わなくする（ModeHudPickingIgnore と同じ。2 本目なので Foundation への昇格を integrator へ依頼済み）
    /// Makes every element in a subtree unpickable (same as ModeHudPickingIgnore; second use, promotion to Foundation requested from the integrator)
    /// </summary>
    public static class GrabOverlayPickingIgnore
    {
        public static void Apply(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }
    }
}
