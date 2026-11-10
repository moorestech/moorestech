using System.Collections.Generic;
using System.Linq;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Screens.Research.Parts;
using Client.Ui.State.Research.Logic;
using Game.Research;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 詳細ペインの高さの上限・スクロール・本文の幅（L1）
    /// Detail pane height cap, scrolling and body width (L1)
    /// </summary>
    public class ResearchDetailPaneLayoutTest : ScreenTestHost
    {
        private const float AreaWidth = 900f;
        private const float AreaHeight = 400f;

        // ペイン 300 − craft の左右 padding 10+15 − 枠 1+1 − 本文の左右 padding 10+10（Web の研究ボタン幅 253）
        // Pane 300 - craft padding 10+15 - border 1+1 - body padding 10+10 (the Web research button width 253)
        private const float BodyContentWidth = 253f;

        private Texture2D _texture;
        private GameContentPresentation _content;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _content = Kit.LoadMasterAndContent(_texture);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void 解放物の多いノードはエリアの高さ引く24で止まり中身がスクロールする()
        {
            var pane = MountInArea(ManyItemUnlocks(60));
            var scroll = Find<GameScrollView>(pane, "research-detail-scroll");

            // ペインは置き場の上下 12 の内側で止まり、溢れた分は説明の領域が持つ
            // The pane stops inside the host's 12px top/bottom and the overflow belongs to the scroll
            Assert.AreEqual(AreaHeight - 24f, pane.layout.height, 0.5f);
            Assert.Greater(scroll.GetContentHeight(), scroll.GetViewportHeight() + 1f, "content overflows the viewport");
            Assert.Greater(scroll.GetViewportHeight(), 0f);
            Assert.AreEqual(DisplayStyle.Flex, scroll.verticalScroller.resolvedStyle.display, "vertical bar shown");

            // 研究ボタンはペインの内側に見えたまま
            // The research button stays visible inside the pane
            var button = Find<RecipeActionButton>(pane, $"research-button-{Kit.Researchable}");
            Assert.LessOrEqual(button.worldBound.yMax, pane.worldBound.yMax + 0.5f);
            Assert.Greater(button.worldBound.height, 0f);

            // スクロールが効く
            // Scrolling works
            scroll.SetScrollOffset(100f);
            Assert.AreEqual(100f, scroll.scrollOffset.y, 0.5f);
        }

        [Test]
        public void 解放物の少ないノードは中身の高さで止まる()
        {
            var pane = MountInArea(Kit.NoUnlocks());
            Assert.Less(pane.layout.height, AreaHeight - 24f - 1f);
            Assert.Greater(pane.layout.height, 0f);
        }

        [Test]
        public void 縦のバーが出ても本文の幅は253で横には流れない()
        {
            var pane = MountInArea(ManyItemUnlocks(60));
            var scroll = Find<GameScrollView>(pane, "research-detail-scroll");
            Assert.AreEqual(DisplayStyle.Flex, scroll.verticalScroller.resolvedStyle.display, "vertical bar shown");

            // バーは本文に重なり、逃げの padding の内側が 253 のまま
            // The bar overlays the content and the inside of the bleed padding stays 253
            var scrollContent = scroll.contentContainer.Children().Single();
            Assert.AreEqual(BodyContentWidth, scrollContent.contentRect.width, 0.5f);
            var button = Find<RecipeActionButton>(pane, $"research-button-{Kit.Researchable}");
            Assert.AreEqual(BodyContentWidth, button.layout.width, 0.5f);

            // 横のバーは出ず、中身は視口の幅を越えない
            // No horizontal bar, and the content never exceeds the viewport width
            Assert.AreEqual(DisplayStyle.None, scroll.horizontalScroller.resolvedStyle.display);
            Assert.LessOrEqual(scroll.contentContainer.layout.width, scroll.contentViewport.layout.width + 0.5f);
        }

        #region Helpers

        private ResearchDetailPane MountInArea(ResearchUnlockSet unlocks)
        {
            // 研究エリアと置き場を画面と同じクラスで組む
            // Build the research area and the host with the screen's classes
            var area = new VisualElement { style = { position = Position.Absolute, left = 0f, top = 0f, width = AreaWidth, height = AreaHeight } };
            var host = new VisualElement();
            host.AddToClassList("research-tree__detail-host");
            var pane = new ResearchDetailPane(new RecordingResearchTreeCommands(), _content, Toaster);
            pane.Render(Kit.Detail(ResearchNodeState.Researchable, 3, 0, unlocks));
            host.Add(pane);
            area.Add(host);
            Mount(area);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return pane;
        }

        private static ResearchUnlockSet ManyItemUnlocks(int count)
        {
            var items = Enumerable.Repeat(ForUnitTestItemId.ItemId1, count).ToList();
            return Kit.Unlocks(new List<UnlockBlock>(), new List<UnlockMachineRecipe>(), items, new List<RewardItem>(), new List<System.Guid>(), new List<System.Guid>(), new List<int>(), new List<LevelInheritanceCraftCap>());
        }

        #endregion
    }
}
