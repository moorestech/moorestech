using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.BlockInventory;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 外殻と項目グリッドの見た目の取り決め（L1。Web blockInventoryDesign.test.ts の alpha 分の写し。寸法は 1280×720 の舞台）
    /// Look contracts of the shell and item grid (L1; alpha's share of Web blockInventoryDesign.test.ts, measured on the 1280x720 stage)
    /// </summary>
    public class BlockInventoryDesignTest : BlockInventoryScreenTestHost
    {
        private const string ScreensUssRoot = "Scripts/Client.Ui/Screens";
        private const float Band = 482f;
        private const float LargeWidth = 740.108753f;
        private const float PixelSnap = 0.5f;

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [TestCase("BlockInventory/BlockInventory.uss")]
        [TestCase("BlockInventory/SectionStack/SectionStack.uss")]
        [TestCase("BlockInventory/Parts/Parts.uss")]
        public void 画面のUSSは色を直書きせずトークンだけを使う(string relativePath)
        {
            var uss = File.ReadAllText(Path.Combine(Application.dataPath, ScreensUssRoot, relativePath));
            StringAssert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|rgba?\(", uss);
            foreach (Match match in Regex.Matches(uss, @"^\s*(?:-unity-)?[a-z-]*color\s*:\s*([^;]+);", RegexOptions.Multiline))
            {
                StringAssert.StartsWith("var(--", match.Groups[1].Value.Trim(), match.Value);
            }
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [Test]
        public void 本文の木の操作の要素は共通部品だけで素の操作部品を含まない()
        {
            var shell = Open(BlockInventoryTestPanels.ChestWithSlots(new SlotItem(new ItemId(1), 1)));
            Assert.IsNotNull(shell.View.Q("chest-grid"));
            CollectionAssert.IsEmpty(BareControls(shell.View).Select(e => $"{e.GetType().Name}:{e.name}").ToArray());

            #region Internal

            // 共通部品は内部へ降りず、利用側が contentContainer に足した子だけ続けてたどる（Web が ScrollArea の中を見ないのと同じ境界。D-46）
            // Shared components are not entered; only children the screen added to their contentContainer are walked (same boundary as the Web skipping ScrollArea internals; D-46)
            System.Collections.Generic.IEnumerable<VisualElement> BareControls(VisualElement element)
            {
                var ns = element.GetType().Namespace ?? string.Empty;
                var shared = ns == "Client.Ui.Components" || ns.StartsWith("Client.Ui.Components.");
                if (!shared && (element is Button || element is Toggle || element is RadioButton || element is Slider || element is ProgressBar)) yield return element;
                var children = shared ? (element.contentContainer == element ? Enumerable.Empty<VisualElement>() : element.contentContainer.Children()) : element.hierarchy.Children();
                foreach (var child in children)
                {
                    foreach (var bare in BareControls(child)) yield return bare;
                }
            }

            #endregion
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">パネル面と閉じる操作をshared uiへ集約する</web-test>
        [Test]
        public void パネル面はGamePanelで閉じるはIconButtonでチュートリアルの錨が付く()
        {
            var shell = Open(BlockInventoryTestPanels.Chest());
            Assert.IsTrue(shell.Panel().ClassListContains("game-panel--default"));
            var close = shell.View.Q<IconButton>("block-inventory-close");
            Assert.IsNotNull(close);
            Assert.IsNotNull(close.Q<IconButtonCloseGlyph>());
            Assert.IsTrue(close.ClassListContains(TutorialAnchor.MarkerClassName));
            Assert.IsTrue(close.ClassListContains(AnchorIds.InventoryCloseButton));
            // 配置は 1280×720 の倍率 1 で物理画素へ丸まるので、トークンの値から半画素以内を見る
            // Layout snaps to device pixels at scale 1 on 1280x720, so check within half a pixel of the token value
            Assert.AreEqual(1.565749f, close.worldBound.yMin - shell.View.worldBound.yMin, PixelSnap, "top");     // --bevel-3
            Assert.AreEqual(12f, shell.View.worldBound.xMax - close.worldBound.xMax, PixelSnap, "right");        // --panel-edge-fade
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ブロックパネルをviewer領域へ上端揃えで配置する</web-test>
        [Test]
        public void パネルはViewer列に載り上端が帯の上端に揃う()
        {
            var shell = Open(BlockInventoryTestPanels.Chest());
            Assert.AreSame(shell.Stage.Get(StageColumn.Viewer), shell.View.parent);
            Assert.AreEqual(168f, shell.View.worldBound.yMin, 0.01f);     // --menu-upper-safe-area
            Assert.AreEqual(Band, shell.View.resolvedStyle.maxHeight.value, 0.01f);
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ブロックパネルだけに下部装飾の安全帯を確保する</web-test>
        [Test]
        public void 下の余白は安全帯で小型だけ右の安全帯を持つ()
        {
            var rule = new FakeLargePanelRule(false);
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest());
            var shell = BlockInventoryTestShell.Create(this, state, rule);
            simulate.FrameUpdate();
            Assert.AreEqual(58f, shell.Panel().resolvedStyle.paddingBottom, 0.01f);
            Assert.AreEqual(28f, shell.Panel().resolvedStyle.paddingRight, 0.01f);

            // 大型は右を上書きせず GamePanel 既定の 10px（Web の大型は右を足さない）
            // Large keeps the GamePanel default 10px right (the Web large panel adds no right inset)
            rule.Large = true;
            rule.Changed.OnNext(Unit.Default);
            simulate.FrameUpdate();
            Assert.AreEqual(58f, shell.Panel().resolvedStyle.paddingBottom, 0.01f);
            Assert.AreEqual(10f, shell.Panel().resolvedStyle.paddingRight, 0.01f);
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ブロックスロットの列数を1列から持ち物と同じ列数の範囲で内容に追従させる</web-test>
        [TestCase(1, 1)]
        [TestCase(7, 7)]
        [TestCase(10, 10)]
        [TestCase(27, 10)]
        public void 項目グリッドの列数は枠数に追従し持ち物の列数で止まる(int slotCount, int expectedColumns)
        {
            var slots = Enumerable.Repeat(new SlotItem(new ItemId(1), 1), slotCount).ToArray();
            var shell = Open(BlockInventoryTestPanels.ChestWithSlots(slots));
            simulate.FrameUpdate();
            var items = shell.View.Q("chest-grid").Query<ItemSlot>().ToList();
            Assert.AreEqual(slotCount, items.Count);
            var firstRowTop = items[0].worldBound.yMin;
            Assert.AreEqual(expectedColumns, items.Count(slot => Mathf.Approximately(slot.worldBound.yMin, firstRowTop)));

            // 受け直したトークンが読めていれば UssCustomStyleReader の LogError は出ない
            // With the re-declared token readable, UssCustomStyleReader logs no error
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">レシピ有り機械だけviewer〜items占有の大型パネルへ広げる</web-test>
        [Test]
        public void 寄与が真のときだけ大型の寸法になる()
        {
            var rule = new FakeLargePanelRule(false);
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest());
            var shell = BlockInventoryTestShell.Create(this, state, rule);
            simulate.FrameUpdate();
            Assert.IsFalse(shell.View.ClassListContains(BlockInventoryView.LargeClassName));
            Assert.Less(shell.View.layout.width, LargeWidth - 1f);
            Assert.LessOrEqual(shell.View.layout.height, Band + 0.01f);

            rule.Large = true;
            rule.Changed.OnNext(Unit.Default);
            simulate.FrameUpdate();
            Assert.IsTrue(shell.View.ClassListContains(BlockInventoryView.LargeClassName));
            Assert.AreEqual(LargeWidth, shell.View.style.width.value.value, 0.01f);
            Assert.AreEqual(LargeWidth, shell.View.layout.width, PixelSnap);
            Assert.AreEqual(Band, shell.View.layout.height, 0.01f);
        }

        private BlockInventoryTestShell Open(BlockInventoryPanelState panel)
        {
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(panel));
            simulate.FrameUpdate();
            return shell;
        }
    }
}
