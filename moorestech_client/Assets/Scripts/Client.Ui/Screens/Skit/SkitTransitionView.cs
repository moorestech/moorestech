using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit
{
    /// <summary>
    /// スキットの暗転。実画面いっぱいの黒をフェード無しで出し入れし、入力は奪わない（Web SkitTransition）
    /// The skit blackout; a full-screen black shown and hidden without fading that never takes input (Web SkitTransition)
    /// </summary>
    public sealed class SkitTransitionView : VisualElement
    {
        public SkitTransitionView()
        {
            name = "skit-transition";
            AddToClassList("skit-transition");
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;
        }

        public void SetShown(bool shown)
        {
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
