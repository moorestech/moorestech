using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.State.Routing;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究画面の舞台の性質（左の余白を畳む・常駐 HUD を出さない）。持ち主は C4-e と各 HUD で、ここは研究画面の値を固定する（L1）
    /// Research screen stage traits (left padding collapsed, no always-on HUD); owned by C4-e and each HUD, this pins the research values (L1)
    /// </summary>
    public class ResearchStageLayoutTest : UiTestHost
    {
        // 舞台の左の余白（Web .stage の padding-left。StageColumnsLayoutTest と同じ値）
        // Stage left padding (Web .stage padding-left; same value as StageColumnsLayoutTest)
        private const float StagePaddingLeft = 40f;

        /// <web-test file="e2e/tests/research/research.spec.ts">研究画面では持ち物がstage左paddingぶん左へ寄る</web-test>
        [Test]
        public void 研究画面では持ち物の列が舞台の左の余白ぶん左へ寄り縦は動かない()
        {
            var layout = new StageColumnsLayout();
            UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Screen, UiFamily.Stage).Add(layout);
            var inventory = new VisualElement();
            inventory.style.width = 473.891247f;
            inventory.style.height = 100f;
            layout.Get(StageColumn.Inv).Add(inventory);

            // 本番の GameUiStageColumnsInstaller と同じく、画面の性質の表の値で畳む
            // Collapse by the screen-trait value, as the production GameUiStageColumnsInstaller does
            layout.SetLeftPaddingCollapsed(UiScreenTraitsTable.Get(UiScreen.PlayerInventory).CollapsesStageLeftPadding);
            simulate.FrameUpdate();
            var onInventoryScreen = inventory.worldBound;
            layout.SetLeftPaddingCollapsed(UiScreenTraitsTable.Get(UiScreen.ResearchTree).CollapsesStageLeftPadding);
            simulate.FrameUpdate();
            var onResearchScreen = inventory.worldBound;

            Assert.AreEqual(StagePaddingLeft, onInventoryScreen.xMin - onResearchScreen.xMin, 1f);
            Assert.AreEqual(onInventoryScreen.yMin, onResearchScreen.yMin, 1.5f);
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">研究画面ではホットバーと装備HUDを描画しない</web-test>
        [Test]
        public void 研究画面は常駐HUDを出さない画面で持ち物画面では出す()
        {
            // ホットバーと装備 HUD はこの値を読んで消える（持ち主 S3-hotbar・S4-inventory）
            // The hotbar and equipment HUD read this value to hide (owners S3-hotbar, S4-inventory)
            Assert.IsFalse(UiScreenTraitsTable.Get(UiScreen.ResearchTree).AlwaysOnHud);
            Assert.IsTrue(UiScreenTraitsTable.Get(UiScreen.PlayerInventory).AlwaysOnHud);
        }
    }
}
