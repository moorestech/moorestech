using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 枠の左上に重なる番号タブ（Web .num。暗い縁取りの 13px の数字）
    /// The number tab overlapping the slot's top-left (Web .num; a 13px digit with a dark outline)
    /// </summary>
    /// <remarks>
    /// 暗い縁はタブで部品のつまみ --outlined-count-label-stroke を上書きして出す（HotbarHud.uss。計画 D-7）。タブ自身は拾う（Web の pointer-events 既定。タブの上で離しても枠の落とし先になる）
    /// The dark outline comes from overriding the part knob --outlined-count-label-stroke on the tab (HotbarHud.uss; plan D-7). The tab itself is pickable (Web default pointer-events; releasing on it still hits the slot)
    /// </remarks>
    public sealed class HotbarNumberTab : VisualElement
    {
        public const string ClassName = "hotbar-hud__number-tab";

        public HotbarNumberTab(int slotIndex)
        {
            AddToClassList(ClassName);
            var number = new OutlinedCountLabel(this, "hotbar-hud__number");
            number.SetText(UiText.FromVerbatim((slotIndex + 1).ToString()));
        }
    }
}
