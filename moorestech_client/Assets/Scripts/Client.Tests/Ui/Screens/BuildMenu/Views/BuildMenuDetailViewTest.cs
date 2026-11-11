using System;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Content;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Entries;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    /// <summary>
    /// 詳細の素材スロットの赤枠と所持/必要（Web BuildMenuDetailSidebar.test.ts の写し）
    /// Red ring and owned/required on the detail's material slots (port of the Web BuildMenuDetailSidebar.test.ts)
    /// </summary>
    public class BuildMenuDetailViewTest : ScreenTestHost
    {
        private GameContentPresentation _content;

        [SetUp]
        public void SetUpContent()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _content = new GameContentPresentation(new FakeContentTextureSource(Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture));
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuDetailSidebar.test.ts">不足素材は赤枠と所持/必要の数値をスロットへ渡す</web-test>
        [Test]
        public void 不足素材は赤枠と所持と必要の数値をスロットへ渡す()
        {
            var detail = ShowEntry(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 1), false);

            // 所持/必要は枠の外（ItemSlot 直下）。数量バッジでなくその字の層を見る
            // The owned/required label sits outside the frame (directly under ItemSlot); read its glyph layer, not the count badge
            var slot = Find<VisualElement>(detail, "build-menu-detail-material-0");
            Assert.IsTrue(slot.ClassListContains("slot-frame--insufficient"));
            StringAssert.Contains("1/3", slot.parent.Q<Label>(null, "item-slot__shortage", "outlined-count-label--glyph").text);
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuDetailSidebar.test.ts">素材が足りていれば赤くしない</web-test>
        [Test]
        public void 素材が足りていれば赤くしない()
        {
            var detail = ShowEntry(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 5), false);

            Assert.IsFalse(Find<VisualElement>(detail, "build-menu-detail-material-0").ClassListContains("slot-frame--insufficient"));
        }

        /// <web-test file="src/features/buildMenu/views/BuildMenuDetailSidebar.test.ts">支払い免除中は素材が足りなくても赤くしない</web-test>
        [Test]
        public void 支払い免除中は素材が足りなくても赤くしない()
        {
            var detail = ShowEntry(BuildMenuSamples.Material(ForUnitTestItemId.ItemId1, 3, 1), true);

            Assert.IsFalse(Find<VisualElement>(detail, "build-menu-detail-material-0").ClassListContains("slot-frame--insufficient"));
        }

        private BuildMenuDetailSidebar ShowEntry(BuildMenuMaterialRow material, bool paymentWaived)
        {
            var detail = Mount(new BuildMenuDetailSidebar(_content, BuildMenuSamples.Icons(_content)));
            detail.ShowEntry(BuildMenuSamples.Block(Guid.NewGuid(), BuildMenuSamples.Logistics, "木のチェスト", new[] { material }, paymentWaived, BuildMenuSetPlacementNone.Instance));
            simulate.FrameUpdate();
            return detail;
        }
    }
}
