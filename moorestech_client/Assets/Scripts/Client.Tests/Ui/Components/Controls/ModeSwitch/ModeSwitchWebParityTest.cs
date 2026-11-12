using System;
using System.Collections.Generic;
using System.Linq;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.State.Text;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;
using ModeSwitchString = Client.Ui.Components.Controls.ModeSwitch<string>;

namespace Client.Tests.Ui.Components.Controls.ModeSwitch
{
    /// <summary>
    /// Web ModeSwitch のテスト 6 件の写し（L1）
    /// Copy of the six Web ModeSwitch tests (L1)
    /// </summary>
    public class ModeSwitchWebParityTest : UiTestHost
    {
        private readonly List<string> _selected = new();
        private IDisposable _subscription;

        [SetUp]
        public void LoadDictionary()
        {
            Localize.Initialize();
            _selected.Clear();
        }

        [TearDown]
        public void DisposeSubscription()
        {
            _subscription?.Dispose();
        }

        /// <web-test file="src/shared/ui/ModeSwitch/index.test.ts">選択中のoptionと縦向きをdata属性で公開する</web-test>
        [Test]
        public void 選択中の選択肢と縦向きを状態のクラスで示す()
        {
            var modeSwitch = Mount(ModeSwitchOrientation.Vertical, ModeSwitchRole.Group);
            modeSwitch.SetSelected("b");
            Assert.IsTrue(modeSwitch.ClassListContains("mode-switch--vertical"));
            Assert.IsFalse(Option(modeSwitch, "mode-a").ClassListContains("mode-switch__option--selected"));
            Assert.IsTrue(Option(modeSwitch, "mode-b").ClassListContains("mode-switch__option--selected"));
        }

        /// <web-test file="src/shared/ui/ModeSwitch/index.test.ts">disabled指定時はdata-disabledと各buttonのdisabledを公開する</web-test>
        [Test]
        public void 全体の無効は根の状態と各選択肢の無効で示す()
        {
            var modeSwitch = Mount(ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group);
            modeSwitch.SetEnabledAll(false);
            Assert.IsTrue(modeSwitch.ClassListContains("mode-switch--disabled"));
            Assert.IsTrue(modeSwitch.Query<Button>().ToList().All(button => !button.enabledSelf));
        }

        /// <web-test file="src/shared/ui/ModeSwitch/index.test.ts">クリックしたoptionのvalueを通知する</web-test>
        [Test]
        public void 押した選択肢の値を知らせる()
        {
            var modeSwitch = Mount(ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group);
            simulate.Click(Option(modeSwitch, "mode-b").worldBound.center);
            CollectionAssert.AreEqual(new[] { "b" }, _selected);
        }

        /// <web-test file="src/shared/ui/ModeSwitch/index.test.ts">option単位のdisabledはそのボタンだけを無効化しrootにdata-disabledを付けない</web-test>
        [Test]
        public void 選択肢ごとの無効はその選択肢だけを止め根は無効にしない()
        {
            var modeSwitch = Mount(ModeSwitchOrientation.Vertical, ModeSwitchRole.Group);
            modeSwitch.SetOptionEnabled("a", false);
            simulate.FrameUpdate();
            Assert.IsFalse(modeSwitch.ClassListContains("mode-switch--disabled"));
            Assert.IsTrue(Option(modeSwitch, "mode-a").ClassListContains("mode-switch__option--option-disabled"));
            Assert.IsFalse(Option(modeSwitch, "mode-a").enabledSelf);
            Assert.IsTrue(Option(modeSwitch, "mode-b").enabledSelf);

            // 止めた選択肢は押しても知らせず、文字は従属色、半透明にしない
            // A disabled option raises nothing, takes the muted text color and stays opaque
            simulate.Click(Option(modeSwitch, "mode-a").worldBound.center);
            simulate.Click(Option(modeSwitch, "mode-b").worldBound.center);
            CollectionAssert.AreEqual(new[] { "b" }, _selected);
            Assert.AreEqual(1f, Option(modeSwitch, "mode-a").resolvedStyle.opacity);

            // 全体の無効を解いても、選択肢の無効は残る
            // Re-enabling the whole switch keeps the per-option disable
            modeSwitch.SetEnabledAll(false);
            modeSwitch.SetEnabledAll(true);
            Assert.IsFalse(Option(modeSwitch, "mode-a").enabledSelf);
            Assert.IsTrue(Option(modeSwitch, "mode-b").enabledSelf);
        }

        /// <web-test file="src/shared/ui/ModeSwitch/index.test.ts">role="tablist"指定ではtab/aria-selectedを公開しクリックで値を返す</web-test>
        [Test]
        public void タブの役ではタブのクラスを付け本文との間を空け押した値を返す()
        {
            var modeSwitch = Mount(ModeSwitchOrientation.Horizontal, ModeSwitchRole.Tablist);
            modeSwitch.SetSelected("a");
            Assert.IsTrue(modeSwitch.ClassListContains("mode-switch--tablist"));
            Assert.AreEqual(5f, modeSwitch.resolvedStyle.paddingBottom, 0.01f, "--mode-switch-padding-block below the tabs");
            simulate.Click(Option(modeSwitch, "mode-b").worldBound.center);
            CollectionAssert.AreEqual(new[] { "b" }, _selected);
        }

        /// <web-test file="src/shared/ui/ModeSwitch/index.test.ts">既定ではrole未指定でaria-pressedを保つ</web-test>
        [Test]
        public void 既定の役ではタブのクラスを付けず本文との間も空けない()
        {
            var modeSwitch = Mount(ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group);
            Assert.IsFalse(modeSwitch.ClassListContains("mode-switch--tablist"));
            Assert.AreEqual(0f, modeSwitch.resolvedStyle.paddingBottom, 0.01f);
        }

        [Test]
        public void 縦の選択肢の高さはつまみで決まり画面が祖先で上書きできる()
        {
            var modeSwitch = Mount(ModeSwitchOrientation.Vertical, ModeSwitchRole.Group);
            // つまみの既定 34.8px は、UITK のレイアウトが画素へ丸めて 35 に解ける（GameTextField の余白 9.6px→10 と同じ）
            // The knob default 34.8px resolves to 35 because UITK layout snaps to whole pixels (same as the 9.6px to 10 padding in GameTextField)
            Assert.AreEqual(35f, Option(modeSwitch, "mode-a").resolvedStyle.height, 0.05f);

            // ビルドメニューは --build-menu-category-height（33px）へ上書きする（Web features/buildMenu/style.module.css）
            // The build menu overrides it to --build-menu-category-height (33px) (Web features/buildMenu/style.module.css)
            var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Client.Tests/Ui/Components/Controls/ModeSwitch/ModeSwitchKnobOverride.uss");
            Assert.IsNotNull(sheet, "ModeSwitchKnobOverride.uss");
            rootVisualElement.AddToClassList("mode-switch-knob-override");
            rootVisualElement.styleSheets.Add(sheet);
            simulate.FrameUpdate();
            Assert.AreEqual(33f, Option(modeSwitch, "mode-a").resolvedStyle.height, 0.05f);
        }

        // Web の輪は box-shadow で面の外へ --bevel-1 広がり、面の箱と選択肢の送りには入らない
        // The Web ring is a box-shadow spreading --bevel-1 outside the face, outside the face box and the option pitch
        [Test]
        public void 輪は面の外へbevel1だけ広がり面の箱と送りを変えない()
        {
            var modeSwitch = Mount(ModeSwitchOrientation.Vertical, ModeSwitchRole.Group);
            modeSwitch.SetSelected("b");
            simulate.FrameUpdate();
            var face = Option(modeSwitch, "mode-a");
            var ring = face.Q(className: "mode-switch__ring");
            Assert.AreEqual(0f, face.resolvedStyle.borderTopWidth, 0.001f, "the face draws no border");
            Assert.AreEqual(PickingMode.Ignore, ring.pickingMode);
            // 位置と枠線の幅は画素へ丸まる（±0.78px は ±1px に解ける）
            // Positions and border widths snap to pixels (±0.78px resolves to ±1px)
            Assert.AreEqual(-0.782875f, ring.resolvedStyle.top, 0.25f);
            Assert.AreEqual(-0.782875f, ring.resolvedStyle.left, 0.25f);
            Assert.AreEqual(0.782875f, ring.resolvedStyle.borderBottomWidth, 0.25f);
            Assert.AreEqual(4f, Option(modeSwitch, "mode-b").layout.yMin - face.layout.yMax, 0.6f, "--mode-switch-gap between faces");

            // 選んでいない輪は --bevel-c1、選んだ輪は --bevel-c2
            // An unselected ring is --bevel-c1 and the selected one --bevel-c2
            Assert.AreEqual(new UnityEngine.Color32(56, 58, 72, 255), (UnityEngine.Color32)ring.resolvedStyle.borderTopColor);
            Assert.AreEqual(new UnityEngine.Color32(104, 106, 120, 255), (UnityEngine.Color32)Option(modeSwitch, "mode-b").Q(className: "mode-switch__ring").resolvedStyle.borderTopColor);
        }

        private ModeSwitchString Mount(ModeSwitchOrientation orientation, ModeSwitchRole role)
        {
            var options = new List<ModeSwitchOption<string>>
            {
                new("mode-a", "a", UiText.FromVerbatim("Mode A")),
                new("mode-b", "b", UiText.FromVerbatim("Mode B")),
            };
            var modeSwitch = new ModeSwitchString("mode-switch-parity", orientation, role, options);
            _subscription = modeSwitch.OnOptionSelected.Subscribe(_selected.Add);
            rootVisualElement.Add(modeSwitch);
            simulate.FrameUpdate();
            return modeSwitch;
        }

        private static Button Option(VisualElement modeSwitch, string testid)
        {
            return modeSwitch.Q<Button>(testid);
        }
    }
}
