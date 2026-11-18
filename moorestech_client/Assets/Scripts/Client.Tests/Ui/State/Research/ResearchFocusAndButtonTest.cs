using System;
using Client.Ui.State.Research.Logic;
using Game.Research;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Research
{
    /// <summary>
    /// Web の researchLogic.test.ts のうち findInitialFocusNode・deriveResearchButton・deriveNodeStateLabelKey の写し（L0）
    /// Port of the findInitialFocusNode, deriveResearchButton and deriveNodeStateLabelKey parts of Web researchLogic.test.ts (L0)
    /// </summary>
    public class ResearchFocusAndButtonTest
    {
        /// <web-test file="src/features/research/researchLogic.test.ts">researchableを最優先で選ぶ</web-test>
        [Test]
        public void 研究可能を最優先で選ぶ()
        {
            var nodes = new[] { Node(1, ResearchNodeState.Completed), Node(2, ResearchNodeState.UnresearchableNotEnoughItem), Node(3, ResearchNodeState.Researchable) };
            Assert.IsTrue(ResearchLogic.TryFindInitialFocusNode(nodes, out var focus));
            Assert.AreEqual(Id(3), focus.Guid);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">researchable不在なら素材待ちの最前線へフォールバックする</web-test>
        [Test]
        public void 研究可能が無ければ素材待ちの最前線へ落とす()
        {
            var nodes = new[] { Node(1, ResearchNodeState.Completed), Node(2, ResearchNodeState.UnresearchableNotEnoughPreNode), Node(3, ResearchNodeState.UnresearchableNotEnoughItem) };
            Assert.IsTrue(ResearchLogic.TryFindInitialFocusNode(nodes, out var focus));
            Assert.AreEqual(Id(3), focus.Guid);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">対象が無ければnullを返す</web-test>
        [Test]
        public void 対象が無ければ選ばない()
        {
            Assert.IsFalse(ResearchLogic.TryFindInitialFocusNode(new[] { Node(1, ResearchNodeState.Completed) }, out _));
            Assert.IsFalse(ResearchLogic.TryFindInitialFocusNode(new ResearchNodeSummary[0], out _));
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">disables completed nodes and never highlights completed consume items</web-test>
        [Test]
        public void 完了ノードはボタンを無効にし消費アイテムの不足も出さない()
        {
            AssertButton(ResearchNodeState.Completed, true, false, LocalizationKeys.Ui.Research.Completed);
            Assert.IsFalse(ResearchLogic.IsConsumeItemLacking(ResearchNodeState.Completed, 1, 0));
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">researchableは所持数を問わず活性（充足の正本はサーバーstate）</web-test>
        [Test]
        public void 研究可能は所持数を問わず活性()
        {
            AssertButton(ResearchNodeState.Researchable, false, true, LocalizationKeys.Ui.Research.ClickToResearch);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">reports missing items when prerequisites are met but consume items are short</web-test>
        [Test]
        public void 前提を満たし素材が足りなければ素材不足()
        {
            AssertButton(ResearchNodeState.UnresearchableNotEnoughItem, false, false, LocalizationKeys.Ui.Research.MissingItems);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">reports missing prerequisites when items are sufficient</web-test>
        [Test]
        public void 素材が足りて前提が未達なら前提不足()
        {
            AssertButton(ResearchNodeState.UnresearchableNotEnoughPreNode, false, false, LocalizationKeys.Ui.Research.MissingPrerequisites);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">reports both missing items and prerequisites</web-test>
        [Test]
        public void 両方足りなければ両方の不足()
        {
            AssertButton(ResearchNodeState.UnresearchableAllReasons, false, false, LocalizationKeys.Ui.Research.MissingItemsAndPrerequisites);
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">completedは完了済みラベル</web-test>
        [Test]
        public void 完了は完了済みラベル()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Research.Completed.Key, LabelKeyOf(ResearchNodeState.Completed));
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">researchableは研究可能ラベル</web-test>
        [Test]
        public void 研究可能は研究可能ラベル()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Research.StateAvailable.Key, LabelKeyOf(ResearchNodeState.Researchable));
        }

        /// <web-test file="src/features/research/researchLogic.test.ts">不可3状態はすべて研究不可ラベルへ畳む</web-test>
        [Test]
        public void 不可の3状態はすべて研究不可ラベルへ畳む()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Research.StateUnavailable.Key, LabelKeyOf(ResearchNodeState.UnresearchableNotEnoughItem));
            Assert.AreEqual(LocalizationKeys.Ui.Research.StateUnavailable.Key, LabelKeyOf(ResearchNodeState.UnresearchableNotEnoughPreNode));
            Assert.AreEqual(LocalizationKeys.Ui.Research.StateUnavailable.Key, LabelKeyOf(ResearchNodeState.UnresearchableAllReasons));
        }

        private static Guid Id(int id) => new Guid(id, 0, 0, new byte[8]);

        private static ResearchNodeSummary Node(int id, ResearchNodeState state) => new ResearchNodeSummary(Id(id), state);

        private static string LabelKeyOf(ResearchNodeState state) => ResearchLogic.DeriveNodeStateLabelKey(ResearchLogic.DeriveNodeCardState(state)).Key;

        private static void AssertButton(ResearchNodeState state, bool completed, bool interactable, LocalizationKey tooltipKey)
        {
            var button = ResearchLogic.DeriveResearchButton(state);
            Assert.AreEqual(completed, button.Completed);
            Assert.AreEqual(interactable, button.Interactable);
            Assert.AreEqual(tooltipKey.Key, button.TooltipKey.Key);
        }
    }
}
