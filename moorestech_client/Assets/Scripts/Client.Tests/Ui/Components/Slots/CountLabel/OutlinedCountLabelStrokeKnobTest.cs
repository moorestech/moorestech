using Client.Tests.Ui.Components;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots.CountLabel
{
    /// <summary>
    /// OutlinedCountLabel の縁の色のつまみを既定と祖先の上書きで確かめる（L1）
    /// Verifies the OutlinedCountLabel outline-color knob with its default and an ancestor override (L1)
    /// </summary>
    public class OutlinedCountLabelStrokeKnobTest : UiTestHost
    {
        private const string KnobPath = "Assets/Scripts/Client.Tests/Ui/Components/Slots/SlotKnobOverrides.uss";

        [TearDown]
        public void NoUnexpectedLogs()
        {
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 縁の色は既定で白い縁のトークンになる()
        {
            var stroke = MountStroke(null);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-light"), stroke.resolvedStyle.color, "stroke");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-light"), stroke.resolvedStyle.unityTextOutlineColor, "outline");
        }

        [Test]
        public void 祖先で縁の色のつまみを上書きすると暗い縁になり字の色は変わらない()
        {
            var stroke = MountStroke("slot-knob-dark-outline");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-dark"), stroke.resolvedStyle.color, "stroke");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-dark"), stroke.resolvedStyle.unityTextOutlineColor, "outline");
            var glyph = stroke.parent.Q<Label>(className: "outlined-count-label--glyph");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--count-text"), glyph.resolvedStyle.color, "glyph");
        }

        private Label MountStroke(string knobClass)
        {
            // 祖先のつまみを当て数字を足す
            // Apply the ancestor knob and add the number
            var screen = new VisualElement();
            if (knobClass != null)
            {
                var knobs = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobPath);
                Assert.IsNotNull(knobs, KnobPath);
                screen.styleSheets.Add(knobs);
                screen.AddToClassList(knobClass);
            }
            var label = new OutlinedCountLabel(screen, "slot-content__count");
            label.SetText(UiText.FromVerbatim("1"));
            rootVisualElement.Add(screen);
            simulate.FrameUpdate();
            return screen.Q<Label>(className: "outlined-count-label--stroke");
        }
    }
}
