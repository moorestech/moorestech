using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Text
{
    /// <summary>
    /// KeyCap の色のつまみの確認
    /// Checks the KeyCap color knob
    /// </summary>
    public class KeyCapTest : UiTestHost
    {
        [TearDown]
        public void ClearRoot()
        {
            rootVisualElement.Clear();
        }

        [Test]
        public void 祖先でkey_cap_colorを上書きするとKeyCapの色だけが変わる()
        {
            const string probePath = "Assets/Scripts/Client.Tests/Ui/Components/Text/KeyCapColorProbe.uss";
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(probePath);
            Assert.IsNotNull(probe, probePath);
            rootVisualElement.styleSheets.Add(probe);
            var screen = new VisualElement();
            screen.AddToClassList("key-cap-color-probe");
            rootVisualElement.Add(screen);
            var plain = new KeyCap("Esc");
            rootVisualElement.Add(plain);
            var overridden = new KeyCap("Tab");
            screen.Add(overridden);
            simulate.FrameUpdate();

            Assert.AreEqual(Color.white, plain.resolvedStyle.color);
            Assert.AreEqual(Color.red, overridden.resolvedStyle.color);
        }
    }
}
