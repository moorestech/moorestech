using System;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Tests.Ui.Screens.Hotbar;
using Client.Ui.Content;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    /// <summary>
    /// エントリのマスの不足ツールチップと枠の状態（Web BuildMenuSlot.test.ts の写し）
    /// Shortage tooltip and frame state of an entry slot (port of the Web BuildMenuSlot.test.ts)
    /// </summary>
    public class BuildMenuSlotTooltipTest : ScreenTestHost
    {
        private GameContentPresentation _content;

        [SetUp]
        public void SetUpContent()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _content = new GameContentPresentation(new FakeContentTextureSource(Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture));
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuSlot.test.ts">不足時は見出しと不足行だけをツールチップに出す</web-test>
        [Test]
        public void 不足時は見出しと不足行だけをツールチップに出す()
        {
            var slot = MountSlot(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 1), BuildMenuSamples.Material(ForUnitTestItemId.ItemId2, 1, 5), false);

            Assert.IsTrue(HoverTooltipMark.TryFindMarked(slot.Frame, out _, out var text));
            var itemName = _content.ItemName(ForUnitTestItemId.ItemId1).Resolve();
            Assert.AreEqual($"{new UiText(LocalizationKeys.Ui.BuildMenu.MaterialShortageTitle).Resolve()}\n{itemName} 1/3", text.Resolve());
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuSlot.test.ts">支払い免除中は素材が足りなくてもツールチップを無効にする</web-test>
        [Test]
        public void 支払い免除中は素材が足りなくてもツールチップを出さない()
        {
            var slot = MountSlot(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 1), BuildMenuSamples.Material(ForUnitTestItemId.ItemId2, 1, 5), true);

            Assert.IsFalse(HoverTooltipMark.TryFindMarked(slot.Frame, out _, out _));
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuSlot.test.ts">充足時はツールチップを無効にする</web-test>
        [Test]
        public void 充足時はツールチップを出さない()
        {
            var slot = MountSlot(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 3), BuildMenuSamples.Material(ForUnitTestItemId.ItemId2, 1, 5), false);

            Assert.IsFalse(HoverTooltipMark.TryFindMarked(slot.Frame, out _, out _));
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuSlot.test.ts">不足していてもスロットに赤枠を付けない</web-test>
        [Test]
        public void 不足していてもスロットに赤枠を付けない()
        {
            var slot = MountSlot(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 1), BuildMenuSamples.Material(ForUnitTestItemId.ItemId2, 1, 5), false);

            Assert.IsFalse(slot.Frame.ClassListContains("slot-frame--insufficient"));
            Assert.IsTrue(slot.Frame.ClassListContains("slot-frame--filled"));
        }

        private BuildMenuEntrySlot MountSlot(BuildMenuMaterialRow first, BuildMenuMaterialRow second, bool paymentWaived)
        {
            var entry = BuildMenuSamples.Block(Guid.NewGuid(), BuildMenuSamples.Logistics, "木のチェスト", new[] { first, second }, paymentWaived, BuildMenuSetPlacementNone.Instance);
            var slot = new BuildMenuEntrySlot(entry, _content, BuildMenuSamples.Icons(_content), new RecordingBuildMenuEntryHandler(), new RecordingHotbarCommands());
            rootVisualElement.Add(slot.Frame);
            simulate.FrameUpdate();
            return slot;
        }
    }
}
