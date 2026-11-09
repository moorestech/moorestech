using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.Columns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Layers
{
    /// <summary>
    /// 上段 3 列の左端・上端が Web の .stage の grid と同じ位置に来ることを確かめる（L1・A-45）
    /// Verifies the three top-row columns start where Web .stage's grid puts them (L1, A-45)
    /// </summary>
    [TestFixture(1280f, 720f, 1f)]
    [TestFixture(1706.6666f, 720f, 1.5f)]
    public class StageColumnsLayoutTest : UiTestHost
    {
        private const string KnobOverridesPath = "Assets/Scripts/Client.Tests/Ui/Layers/StageColumnsKnobOverrides.uss";

        // Web の各パネルの幅（持ち物 --inventory-panel-width・レシピ 337.2px・アイテム一覧 378px）
        // Web panel widths (inventory --inventory-panel-width, recipe 337.2px, item list 378px)
        private const float InventoryPanelWidth = 473.891247f;
        private const float RecipeViewerWidth = 337.2f;
        private const float ItemListWidth = 378f;
        private const float Tolerance = 2f;

        private readonly Vector2 _logicalPanelSize;
        private readonly float _pixelsPerPoint;

        public StageColumnsLayoutTest(float width, float height, float panelPixelsPerPoint)
        {
            _logicalPanelSize = new Vector2(width, height);
            _pixelsPerPoint = panelPixelsPerPoint;
        }

        [OneTimeSetUp]
        public void SetUpLogicalPanelSize()
        {
            panelSize = _logicalPanelSize;
        }

        [SetUp]
        public void SetUpPanelPixelsPerPoint()
        {
            pixelsPerPoint = _pixelsPerPoint;
        }

        [Test]
        public void 三列の左端と上端がWebのgridと同じ位置に来る()
        {
            var (stage, layout) = MountWithPanels(true);

            AssertColumnStart(stage, layout, StageColumn.Inv, 40f);
            AssertColumnStart(stage, layout, StageColumn.Viewer, 40f + InventoryPanelWidth + 16f);
            AssertColumnStart(stage, layout, StageColumn.Items, 40f + InventoryPanelWidth + 16f + RecipeViewerWidth + 16f);
            foreach (StageColumn column in System.Enum.GetValues(typeof(StageColumn)))
                Assert.AreEqual(168f, layout.Get(column).worldBound.yMin - stage.worldBound.yMin, Tolerance, $"{column} の上端");
        }

        [Test]
        public void 左の余白を畳むと持ち物の列が舞台の左端から始まる()
        {
            var (stage, layout) = MountWithPanels(true);

            layout.SetLeftPaddingCollapsed(true);
            simulate.FrameUpdate();

            AssertColumnStart(stage, layout, StageColumn.Inv, 0f);
            AssertColumnStart(stage, layout, StageColumn.Viewer, InventoryPanelWidth + 16f);
        }

        [Test]
        public void 畳みを戻すと左の余白が戻る()
        {
            var (stage, layout) = MountWithPanels(true);

            layout.SetLeftPaddingCollapsed(true);
            simulate.FrameUpdate();
            layout.SetLeftPaddingCollapsed(false);
            simulate.FrameUpdate();

            AssertColumnStart(stage, layout, StageColumn.Inv, 40f);
        }

        [Test]
        public void 持ち物の列が空でも列間は残る()
        {
            // Web の grid は空の auto の track にも column-gap を挟む
            // Web's grid keeps the column gap even around an empty auto track
            var (stage, layout) = MountWithPanels(false);

            AssertColumnStart(stage, layout, StageColumn.Viewer, 40f + 0f + 16f);
        }

        [Test]
        public void 祖先で上書きしたトークンに列が付いて来る()
        {
            var overrides = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobOverridesPath);
            Assert.IsNotNull(overrides, KnobOverridesPath);
            var (stage, layout) = MountWithPanels(true);
            stage.styleSheets.Add(overrides);
            stage.AddToClassList("stage-columns-test-override");
            simulate.FrameUpdate();

            AssertColumnStart(stage, layout, StageColumn.Inv, 24f);
            AssertColumnStart(stage, layout, StageColumn.Viewer, 24f + InventoryPanelWidth + 8f);
            Assert.AreEqual(100f, layout.Get(StageColumn.Inv).worldBound.yMin - stage.worldBound.yMin, Tolerance, "上端");
        }

        [Test]
        public void 列の入れ物は拾わない()
        {
            var (_, layout) = MountWithPanels(true);

            Assert.AreEqual(PickingMode.Ignore, layout.pickingMode, "入れ物");
            foreach (StageColumn column in System.Enum.GetValues(typeof(StageColumn)))
                Assert.AreEqual(PickingMode.Ignore, layout.Get(column).pickingMode, column.ToString());
        }

        private (VisualElement stage, StageColumnsLayout layout) MountWithPanels(bool withInventory)
        {
            // 本番と同じ層の舞台に載せ、各列へ Web の幅の仮置きを入れる
            // Mount on the production layer's stage and put Web-width placeholders in each column
            var stack = UiLayerStack.Build(rootVisualElement);
            var stage = stack.GetLayer(UiLayer.Screen, UiFamily.Stage);
            var layout = new StageColumnsLayout();
            stage.Add(layout);
            if (withInventory) layout.Get(StageColumn.Inv).Add(Placeholder(InventoryPanelWidth));
            layout.Get(StageColumn.Viewer).Add(Placeholder(RecipeViewerWidth));
            layout.Get(StageColumn.Items).Add(Placeholder(ItemListWidth));
            simulate.FrameUpdate();
            return (stage, layout);
        }

        private static VisualElement Placeholder(float width)
        {
            var element = new VisualElement();
            element.style.width = width;
            element.style.height = 100f;
            return element;
        }

        private static void AssertColumnStart(VisualElement stage, StageColumnsLayout layout, StageColumn column, float expectedX)
        {
            Assert.AreEqual(expectedX, layout.Get(column).worldBound.xMin - stage.worldBound.xMin, Tolerance, $"{column} の左端");
        }
    }
}
