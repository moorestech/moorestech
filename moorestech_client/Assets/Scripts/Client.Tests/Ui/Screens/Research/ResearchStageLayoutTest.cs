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
    /// 研究画面の舞台の値を固定（L1）
    /// Pins research stage values (L1)
    /// </summary>
    public class ResearchStageLayoutTest : UiTestHost
    {
        // 舞台の左余白（Web .stage）
        // Stage left padding (Web .stage)
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

            // 本番と同じく性質表の値で畳む
            // Collapse by the trait table value
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
            // HUDはこの値で消える
            // The HUD hides by this value
            Assert.IsFalse(UiScreenTraitsTable.Get(UiScreen.ResearchTree).AlwaysOnHud);
            Assert.IsTrue(UiScreenTraitsTable.Get(UiScreen.PlayerInventory).AlwaysOnHud);
        }
    }
}
