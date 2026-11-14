using System.Collections.Generic;
using Client.Localization;
using Client.Tests.Ui.TestDoubles;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls.PointerCursor
{
    /// <summary>
    /// Web で cursor: pointer を持つ共通部品（IconButton・PanelActionButton・ModeSwitch）が、乗ると自分で指を出し外すと既定へ戻すことを固定する（L1。A-247）
    /// Pins that the shared controls carrying Web cursor: pointer (IconButton, PanelActionButton, ModeSwitch) request the pointer themselves on hover and return to default on leave (L1; A-247)
    /// </summary>
    public class CommonButtonPointerCursorTest : UiTestHost
    {
        private static readonly Vector2 Outside = new(2f, 2f);

        private FakeUiOsCursorApplier _applier;
        private UiOsCursorRequestStack _osCursor;
        private VisualElement _host;

        [SetUp]
        public void SetUpCursorStack()
        {
            // 要求スタックは本物、当て先だけ記録する偽物。LocalizedLabel は載せた時点で辞書を引くので先に読む
            // Real request stack with a recording applier; LocalizedLabel reads the dictionary on attach, so load it first
            Localize.Initialize();
            _applier = new FakeUiOsCursorApplier();
            _osCursor = new UiOsCursorRequestStack(_applier);
            _host = new VisualElement { name = "pointer-cursor-host" };
            _host.style.position = Position.Absolute;
            _host.style.left = 40;
            _host.style.top = 40;
            rootVisualElement.Add(_host);
        }

        [TearDown]
        public void RemoveHost()
        {
            _host.RemoveFromHierarchy();
        }

        [Test]
        public void IconButtonに乗ると指で外すと既定()
        {
            var button = Mount(new IconButton("pointer-icon-button", new IconButtonCloseGlyph(), _osCursor));
            AssertPointerWhileHovered(button);
        }

        [Test]
        public void PanelActionButtonに乗ると指で外すと既定()
        {
            var button = Mount(new PanelActionButton("pointer-panel-action-button", new UiText(LocalizationKeys.Ui.PauseMenu.Settings), _osCursor));
            AssertPointerWhileHovered(button);
        }

        [Test]
        public void ModeSwitchの2つ目の選択肢に乗ると指で外すと既定()
        {
            var options = new List<ModeSwitchOption<int>>
            {
                new("pointer-mode-a", 1, new UiText(LocalizationKeys.Ui.PauseMenu.Settings)),
                new("pointer-mode-b", 2, new UiText(LocalizationKeys.Ui.PauseMenu.Back)),
            };
            var modeSwitch = Mount(new ModeSwitch<int>("pointer-mode-switch", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, options, _osCursor));
            AssertPointerWhileHovered(modeSwitch.Q<Button>("pointer-mode-b"));
        }

        [Test]
        public void 無効のIconButtonもWebと同じく指を出す()
        {
            // Web の IconButton は :disabled でも cursor: pointer のまま。UITK の無効の要素もポインタの事象を受けるので同じになる
            // The Web IconButton keeps cursor: pointer when :disabled; disabled UITK elements still get pointer events, so it matches
            var button = Mount(new IconButton("pointer-icon-button-disabled", new IconButtonCloseGlyph(), _osCursor));
            button.SetEnabled(false);
            simulate.FrameUpdate();
            AssertPointerWhileHovered(button);
        }

        [Test]
        public void ModeSwitchの無効の選択肢は指を出さず乗ったまま無効にすると外す()
        {
            // Web の無効の選択肢は pointer-events: none で cursor: default（ModeSwitch/style.module.css:50-53）
            // Web disabled options are pointer-events: none with cursor: default (ModeSwitch/style.module.css:50-53)
            var options = new List<ModeSwitchOption<int>>
            {
                new("pointer-mode-a", 1, new UiText(LocalizationKeys.Ui.PauseMenu.Settings)),
                new("pointer-mode-b", 2, new UiText(LocalizationKeys.Ui.PauseMenu.Back)),
            };
            var modeSwitch = Mount(new ModeSwitch<int>("pointer-mode-switch", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, options, _osCursor));
            var second = modeSwitch.Q<Button>("pointer-mode-b");
            simulate.MouseMove(Outside);
            simulate.MouseMove(second.worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Pointer, _applier.Current, "pointer before disabling");

            // 乗ったまま無効にすると外れ、出入りし直しても既定のまま
            // Disabling while hovered withdraws it, and re-entering keeps the default
            modeSwitch.SetOptionEnabled(2, false);
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Default, _applier.Current, "withdrawn on disable");
            simulate.MouseMove(Outside);
            simulate.MouseMove(second.worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Default, _applier.Current, "no pointer on a disabled option");

            // 全体の無効でも同じ
            // Same for whole-switch disabling
            modeSwitch.SetEnabledAll(false);
            simulate.MouseMove(Outside);
            simulate.MouseMove(modeSwitch.Q<Button>("pointer-mode-a").worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Default, _applier.Current, "no pointer while the whole switch is disabled");
        }


        private T Mount<T>(T element) where T : VisualElement
        {
            _host.Add(element);
            simulate.FrameUpdate();
            return element;
        }

        private void AssertPointerWhileHovered(VisualElement target)
        {
            // 外から乗せて指、外へ出して既定
            // Move in from outside for the pointer, then out again for the default
            Assert.Greater(target.worldBound.width, 0f, $"{target.name} has a laid-out width");
            simulate.MouseMove(Outside);
            simulate.MouseMove(target.worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Pointer, _applier.Current, $"pointer over {target.name}");
            simulate.MouseMove(Outside);
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Default, _applier.Current, $"default after leaving {target.name}");
        }
    }
}
