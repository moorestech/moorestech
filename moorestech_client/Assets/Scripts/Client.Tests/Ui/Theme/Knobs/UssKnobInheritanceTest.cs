using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Theme.Knobs
{
    /// <summary>
    /// 画面のルートでつまみ（USS カスタムプロパティ）を上書きすると、子孫の部品にだけ効くことを実パネルで確かめる（L1・設計書 §3.2-3）
    /// Verifies on a real panel that overriding a knob (USS custom property) at a screen root affects only descendant parts (L1, design §3.2-3)
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

            // 画面の中の子・孫と、画面の外の兄弟の 3 つの部品を置く
            // Place three parts: a child and a grandchild inside the screen, and a sibling outside it
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
