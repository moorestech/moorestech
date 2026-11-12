using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Screens.Research.Parts;
using Client.Ui.State.Research.Tree;
using Game.Research;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 詳細ペインの研究ボタン・理由・閉じる・不足（L1）
    /// Detail pane button, reason, close, shortage (L1)
    /// </summary>
    public class ResearchDetailPaneTest : ScreenTestHost
    {
        private Texture2D _texture;
        private GameContentPresentation _content;
        private RecordingResearchTreeCommands _commands;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _content = Kit.LoadMasterAndContent(_texture);
            _commands = new RecordingResearchTreeCommands();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_texture);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">研究可能ノードでボタン活性・クリックでresearch.completeを送る</web-test>
        /// <web-test file="e2e/tests/research/research.spec.ts">research button sends research.complete and node becomes completed</web-test>
        [Test]
        public void 研究可能なら押せて押すと研究を実行し完了の表示に替わる()
        {
            var pane = MountPane(Kit.Detail(ResearchNodeState.Researchable, 3, 0, Kit.NoUnlocks()));
            var button = ResearchButton();
            Assert.IsTrue(button.enabledSelf);
            Assert.AreEqual("Research", LabelOf(button));

            // 押すと研究を1回実行する
            // Pressing runs research once
            Click(button);
            CollectionAssert.AreEqual(new[] { $"complete:{Kit.Researchable}" }, _commands.Issued);
            Assert.IsEmpty(ShownToasts);

            // 完了へ替わると押せない
            // Disabled once completed
            pane.Render(Kit.Detail(ResearchNodeState.Completed, 3, 0, Kit.NoUnlocks()));
            simulate.FrameUpdate();
            Assert.IsFalse(button.enabledSelf);
            Assert.AreEqual("Completed", LabelOf(button));
            Assert.IsFalse(IsShown(Find<Label>(rootVisualElement, "research-detail-reason")));
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">サーバーstateがアイテム不足ならボタン非活性で理由を表示し、閉じるでonCloseが呼ばれる</web-test>
        [Test]
        public void アイテム不足なら押せず理由を出し閉じるで閉じる操作を呼ぶ()
        {
            MountPane(Kit.Detail(ResearchNodeState.UnresearchableNotEnoughItem, 1, 0, Kit.NoUnlocks()));

            Assert.IsFalse(ResearchButton().enabledSelf);
            var reason = Find<Label>(rootVisualElement, "research-detail-reason");
            Assert.IsTrue(IsShown(reason));
            Assert.AreEqual("Not enough research items.", Kit.PlainText(reason));
            Click(Find<VisualElement>(rootVisualElement, "research-detail-close"));
            CollectionAssert.AreEqual(new[] { "close" }, _commands.Issued);
        }

        [Test]
        public void サーバー状態の受信前は既定の状態でも理由を出さない()
        {
            var lacking = Kit.Detail(ResearchNodeState.UnresearchableAllReasons, 0, 0, Kit.NoUnlocks());
            MountPane(new ResearchDetailViewModel(lacking.Guid, lacking.Button, false, lacking.ConsumeSlots, lacking.Unlocks));

            Assert.IsFalse(ResearchButton().enabledSelf);
            Assert.IsFalse(IsShown(Find<Label>(rootVisualElement, "research-detail-reason")));
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">所持数不足の消費アイテムはinsufficient=trueで所持/必要をスロットへ渡す</web-test>
        [Test]
        public void 所持が足りない消費アイテムは不足の枠と所持と必要を出す()
        {
            MountPane(Kit.Detail(ResearchNodeState.UnresearchableNotEnoughItem, 1, 0, Kit.NoUnlocks()));
            AssertConsumeSlot(true, true);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">所持数が足りていればinsufficient=falseで所持/必要をスロットへ渡す</web-test>
        [Test]
        public void 所持が足りていれば不足の枠にせず所持と必要は出す()
        {
            MountPane(Kit.Detail(ResearchNodeState.Researchable, 3, 0, Kit.NoUnlocks()));
            AssertConsumeSlot(false, true);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">完了ノードは所持不足でも消費アイテムを不足強調しない</web-test>
        [Test]
        public void 完了ノードは所持が足りなくても不足の枠にしない()
        {
            MountPane(Kit.Detail(ResearchNodeState.Completed, 0, 0, Kit.NoUnlocks()));
            AssertConsumeSlot(false, true);
        }

        /// <web-test file="src/features/research/ResearchDetailPane.test.ts">上位レベルの所持も消費アイテムの充足に数える</web-test>
        [Test]
        public void 上位レベルの所持も充足に数える()
        {
            MountPane(Kit.Detail(ResearchNodeState.Researchable, 0, 3, Kit.NoUnlocks()));
            AssertConsumeSlot(false, true);
        }

        [Test]
        public void 閉じて同じノードを開き直すと説明のスクロールは頭へ戻る()
        {
            // 開き直しは頭から、再描画では保つ
            // Reopen starts at top; re-render keeps
            var detail = Kit.Detail(ResearchNodeState.Researchable, 3, 0, Kit.NoUnlocks());
            var pane = MountPane(detail);
            var scroll = Find<ScrollView>(rootVisualElement, "research-detail-scroll");
            scroll.Add(new VisualElement { style = { height = 2000f } });
            simulate.FrameUpdate();
            scroll.scrollOffset = new Vector2(0f, 100f);
            Assert.Greater(scroll.scrollOffset.y, 0f, "scrolled");

            pane.Render(detail);
            Assert.Greater(scroll.scrollOffset.y, 0f, "same node keeps the scroll while shown");

            pane.Hide();
            pane.Render(detail);
            simulate.FrameUpdate();
            Assert.AreEqual(0f, scroll.scrollOffset.y, 0.5f);
        }

        #region Helpers

        private ResearchDetailPane MountPane(ResearchDetailViewModel detail)
        {
            // キーの無いラベルを載せないよう、描いてから載せる
            // Render before mounting so no keyless label is attached
            var pane = new ResearchDetailPane(_commands, _content, Toaster);
            pane.Render(detail);
            Mount(pane);
            simulate.FrameUpdate();
            return pane;
        }

        private RecipeActionButton ResearchButton()
        {
            return Find<RecipeActionButton>(rootVisualElement, $"research-button-{Kit.Researchable}");
        }

        private static string LabelOf(RecipeActionButton button)
        {
            return button.Q<Label>(className: RecipeActionButton.LabelUssClassName).text;
        }

        private void Click(VisualElement target)
        {
            var center = target.worldBound.center;
            simulate.MouseDown(center, MouseButton.LeftMouse);
            simulate.MouseUp(center, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }

        private void AssertConsumeSlot(bool insufficient, bool showsShortage)
        {
            var frame = Find<VisualElement>(rootVisualElement, "research-consume-slot-0");
            var slot = frame.parent;
            Assert.AreEqual(insufficient, frame.ClassListContains("slot-frame--insufficient"));
            Assert.AreEqual(insufficient, slot.ClassListContains("item-slot--lack"));
            Assert.AreEqual(showsShortage, slot.ClassListContains("item-slot--shortage"));
        }

        #endregion
    }
}
