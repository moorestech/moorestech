using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Theme.Knobs
{
    /// <summary>
    /// つまみの上書きが子孫だけに効く（L1）
    /// Knob override affects descendants only (L1)
    /// </summary>
    public class UssKnobInheritanceTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Theme/Knobs/UssKnobInheritanceProbe.uss";
        private const float DefaultSlotSize = 48f;
        private const float OverriddenSlotSize = 64f;

        [Test]
        public void ScreenRootOverrideReachesChildAndGrandchildButNotSibling()
        {
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            rootVisualElement.styleSheets.Add(probe);

            // 子・孫・外の兄弟を置く
            // Place child, grandchild and outside sibling
            var screen = new VisualElement();
            screen.AddToClassList("knob-probe-screen");
            var child = Part();
            var wrapper = new VisualElement();
            var grandchild = Part();
            wrapper.Add(grandchild);
            screen.Add(child);
            screen.Add(wrapper);
            var sibling = Part();
            rootVisualElement.Add(screen);
            rootVisualElement.Add(sibling);
            simulate.FrameUpdate();

            Assert.AreEqual(OverriddenSlotSize, child.resolvedStyle.width, 0.01f);
            Assert.AreEqual(OverriddenSlotSize, grandchild.resolvedStyle.height, 0.01f);
            Assert.AreEqual(DefaultSlotSize, sibling.resolvedStyle.width, 0.01f);

            #region Internal

            VisualElement Part()
            {
                var part = new VisualElement();
                part.AddToClassList("knob-probe-part");
                return part;
            }

            #endregion
        }
    }
}
