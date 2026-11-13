using System.Collections.Generic;
using System.Reflection;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Filter;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;

namespace Client.Tests.Ui.Filter
{
    /// <summary>
    /// 実行時パネルの PanelSettings の倍率だけを変えると、倍率を読む部品が新しい倍率で組み直されるか（L1・C6/D2）
    /// Checks that changing only the runtime panel's PanelSettings scale rebuilds the scale-reading parts at the new scale (L1, C6/D2)
    /// </summary>
    /// <remarks>
    /// 本番のパネルは画面合わせなので、ウィンドウの大きさの変化は PanelSettings の倍率の変化として届く。Editor パネルの pixelsPerPoint ではなく、この経路で確かめる。
    /// The production panel scales with the screen, so a window size change arrives as a PanelSettings scale change; this checks that path rather than the Editor panel's pixelsPerPoint.
    /// </remarks>
    public class PanelSettingsScaleRebuildTest : RuntimeUITestFixture
    {
        private const string GameUiPanelSettingsPath = "Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset";
        private const float ChangedScale = 1.5f;

        private PanelSettings _panelSettings;
        private GameObject _documentObject;

        [OneTimeSetUp]
        public void AttachGameUiRuntimePanel()
        {
            // 本番のテーマを持つ実行時パネルを倍率 1 の固定倍率で立てる
            // Raise a runtime panel with the production theme at a constant scale of 1
            var production = AssetDatabase.LoadAssetAtPath<PanelSettings>(GameUiPanelSettingsPath);
            if (production == null) Assert.Fail($"PanelSettings not found: {GameUiPanelSettingsPath}");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.themeStyleSheet = production.themeStyleSheet;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.scale = 1f;
            _documentObject = new GameObject("panel-settings-scale-rebuild-test");
            var document = _documentObject.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            SetUIContent(document);
        }

        [OneTimeTearDown]
        public void DestroyGameUiRuntimePanel()
        {
            Object.DestroyImmediate(_documentObject);
            Object.DestroyImmediate(_panelSettings);
        }

        [SetUp]
        public void ResetScale()
        {
            _panelSettings.scale = 1f;
            rootVisualElement.Clear();
            RunRuntimePanelFrame();
        }

        [Test]
        public void 倍率だけを変えると倍率を読む部品が新しい倍率で組み直される()
        {
            var group = MountPixelFilterGroup();
            var label = MountLocalizedLabel();
            var ring = MountModeSwitchRing();
            Assert.AreEqual(1f, group.scaledPixelsPerPoint, 0.0001f, "panel scale before");
            Assert.AreEqual(1f, group.resolvedStyle.scale.value.x, 0.0001f, "group before");
            StringAssert.DoesNotContain("<cspace=", label.text, "15px does not round at scale 1");

            // 倍率だけを変え、部品の作り直しなしで読み直されるか
            // Change only the scale and check the parts re-read it without being rebuilt
            _panelSettings.scale = ChangedScale;
            RunRuntimePanelFrame();
            RunRuntimePanelFrame();
            Assert.AreEqual(ChangedScale, group.scaledPixelsPerPoint, 0.0001f, "panel scale after");

            Assert.AreEqual(1f / ChangedScale, group.resolvedStyle.scale.value.x, 0.0001f, "group outer layer");
            Assert.AreEqual(ChangedScale, group.contentContainer.resolvedStyle.scale.value.x, 0.0001f, "group inner layer");
            StringAssert.Contains("<cspace=", label.text, "15px rounds to 23 physical px at scale 1.5");
            Assert.AreEqual(MountLocalizedLabel().text, label.text, "same tag as a label mounted at the new scale");
            AssertRingSnapped(ring, ChangedScale);
        }

        // 本番の毎フレームの入口（RuntimePanel.Update）を通し、PanelSettings の倍率をパネルへ当てる。試験枠の FrameUpdate はこれを呼ばない
        // Go through the production per-frame entry (RuntimePanel.Update), which applies the PanelSettings scale to the panel; the fixture FrameUpdate skips it
        private void RunRuntimePanelFrame()
        {
            var update = rootVisualElement.panel.GetType().GetMethod("Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
            if (update == null) Assert.Fail($"{rootVisualElement.panel.GetType().Name}.Update() not found; the runtime panel frame entry moved");
            update.Invoke(rootVisualElement.panel, null);
            simulate.FrameUpdate();
        }

        private PixelFilterGroup MountPixelFilterGroup()
        {
            var host = new VisualElement();
            host.style.position = Position.Absolute;
            host.style.width = 40f;
            host.style.height = 30f;
            var group = new PixelFilterGroup(PixelFilterGroupFlow.Fill);
            host.Add(group);
            rootVisualElement.Add(host);
            simulate.FrameUpdate();
            return group;
        }

        // 15px は倍率 1 で丸まらず、倍率 1.5 で 22.5→23 に丸まって字間の印が付く
        // 15px does not round at scale 1 and rounds 22.5 -> 23 at scale 1.5, which adds the spacing tag
        private LocalizedLabel MountLocalizedLabel()
        {
            var label = new LocalizedLabel();
            label.AddToClassList(UiTypographyClass.TextSm);
            label.style.fontSize = 15f;
            label.style.position = Position.Absolute;
            label.SetText(UiText.FromVerbatim("Research cannot be completed"));
            rootVisualElement.Add(label);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return label;
        }

        private VisualElement MountModeSwitchRing()
        {
            var options = new List<ModeSwitchOption<int>> { new("option-a", 1, UiText.FromVerbatim("A")), new("option-b", 2, UiText.FromVerbatim("B")) };
            var modeSwitch = new ModeSwitch<int>("mode-switch-scale-test", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, options);
            rootVisualElement.Add(modeSwitch);
            simulate.FrameUpdate();
            return modeSwitch.Q(className: "mode-switch__ring");
        }

        // 輪の幅は新しい倍率の物理画素の整数（ModeSwitchRing と同じ式）
        // The ring width is a whole number of physical pixels at the new scale (same formula as ModeSwitchRing)
        private static void AssertRingSnapped(VisualElement ring, float scale)
        {
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(ring.customStyle, "--mode-switch-ring-width", out var bevel), "ring width declared");
            var snappedPixels = Mathf.Max(1f, Mathf.Round(bevel * scale));
            Assert.AreEqual(snappedPixels / scale, ring.style.borderTopWidth.value, 0.0001f, "ring width at the new scale");
            Assert.AreEqual(Mathf.Min(1f, bevel * scale / snappedPixels), ring.style.opacity.value, 0.0001f, "ring coverage at the new scale");
        }
    }
}
