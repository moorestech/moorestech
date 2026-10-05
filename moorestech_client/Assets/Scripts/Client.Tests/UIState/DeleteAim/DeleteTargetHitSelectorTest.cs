using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;
using Mooresmaster.Localization.Generated;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     照準ヒット列から削除対象を選ぶ規則（最前面／カテゴリー指定時はその中の最前面）と外れた理由を検証する
    ///     Verifies picking a delete target from aim hits (frontmost / frontmost within a required category) and the miss reasons
    /// </summary>
    public class DeleteTargetHitSelectorTest
    {
        [Test]
        public void FrontmostPicksNearest()
        {
            // 電線がブロックの手前なら電線を取る
            // A wire in front of a block wins
            var wire = new FakeDeleteTarget { Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var block = new FakeDeleteTarget { Category = "default" };
            var hits = new List<DeleteTargetHit> { new(5f, block), new(2f, wire) };

            var result = DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Frontmost);
            Assert.AreEqual(DeleteAimOutcome.Found, result.Outcome);
            Assert.AreSame(wire, result.Target);
        }

        [Test]
        public void CategorySkipsNearerOtherCategory()
        {
            // ブロックのドラッグ中は手前の電線を飛ばして奥のブロックを取る
            // During a block drag, skip the nearer wire and take the block behind it
            var wire = new FakeDeleteTarget { Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var block = new FakeDeleteTarget { Category = "default" };
            var hits = new List<DeleteTargetHit> { new(2f, wire), new(5f, block) };

            var result = DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Category("default"));
            Assert.AreEqual(DeleteAimOutcome.Found, result.Outcome);
            Assert.AreSame(block, result.Target);
        }

        [Test]
        public void FrontmostNonTargetOccludes()
        {
            // 最前面が削除対象でない物体なら奥は拾わない（従来の遮蔽規則）
            // A non-target frontmost hit occludes what is behind (existing occlusion rule)
            var block = new FakeDeleteTarget { Category = "default" };
            var hits = new List<DeleteTargetHit> { new(1f, null), new(5f, block) };

            Assert.AreEqual(DeleteAimOutcome.OccludedByNonTarget, DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Frontmost).Outcome);
        }

        [Test]
        public void CategoryWithoutMatchReportsNoTargetOfCategory()
        {
            // 指定カテゴリーの対象が1件も無ければ理由付きで外れる
            // With no target of the required category, it misses with a reason
            var wire = new FakeDeleteTarget { Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var hits = new List<DeleteTargetHit> { new(2f, wire) };

            Assert.AreEqual(DeleteAimOutcome.NoTargetOfCategory, DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Category("default")).Outcome);
        }

        [Test]
        public void CategoryPicksNearestMatchingTargetPastNonTarget()
        {
            // 遮蔽物と別カテゴリーを飛ばし、一致する対象の最前面を選ぶ
            // Pass non-targets and other categories, then choose the nearest matching target
            var near = new FakeDeleteTarget { Category = "default" };
            var far = new FakeDeleteTarget { Category = "default" };
            var wire = new FakeDeleteTarget { Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var hits = new List<DeleteTargetHit> { new(9f, far), new(1f, null), new(2f, wire), new(5f, near) };

            var result = DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Category("default"));
            Assert.AreEqual(DeleteAimOutcome.Found, result.Outcome);
            Assert.AreSame(near, result.Target);
        }

        [Test]
        public void FrontmostAcceptsNonTargetWhileCategoryOnlyAcceptsMatchingTarget()
        {
            // 未固定は全ヒットを受け、固定時は一致するカテゴリーだけ受ける
            // Unfixed aim accepts every hit; fixed aim accepts only matching categories
            Assert.IsTrue(DeleteAimFilter.Frontmost.Accepts(null));
            Assert.IsTrue(DeleteAimFilter.Category("default").Accepts(new FakeDeleteTarget { Category = "default" }));
            Assert.IsFalse(DeleteAimFilter.Category("default").Accepts(new FakeDeleteTarget { Category = "foundation" }));
        }

        [Test]
        public void CategoryWithOnlyNonTargetHitsHasNoDragDenial()
        {
            // 対象でない遮蔽物だけなら異カテゴリーの拒否表示を出さない
            // Non-target occluders alone must not show an off-category drag denial
            var hits = new List<DeleteTargetHit> { new(1f, null), new(3f, null) };
            var result = DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Category("default"));

            Assert.AreEqual(DeleteAimOutcome.OccludedByNonTarget, result.Outcome);
            Assert.IsNull(result.Target);
            Assert.IsFalse(result.GetDragDenyReason().HasValue);
        }

        [Test]
        public void OffCategoryTargetBehindNonTargetReportsDragDenial()
        {
            // 遮蔽物の奥にも別カテゴリーしか無ければ異カテゴリー理由を返す
            // An off-category target behind an occluder still returns the category denial
            var wire = new FakeDeleteTarget { Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var hits = new List<DeleteTargetHit> { new(1f, null), new(3f, wire) };
            var result = DeleteTargetHitSelector.Select(hits, DeleteAimFilter.Category("default"));

            Assert.AreEqual(DeleteAimOutcome.NoTargetOfCategory, result.Outcome);
            Assert.IsNull(result.Target);
            Assert.AreEqual(LocalizationKeys.Ui.Delete.DifferentCategorySelection, result.GetDragDenyReason().Value);
        }

        [TestCase(DeleteAimOutcome.Found)]
        [TestCase(DeleteAimOutcome.NothingHit)]
        [TestCase(DeleteAimOutcome.OccludedByNonTarget)]
        public void OtherAimOutcomesDoNotShowCategoryDenial(DeleteAimOutcome outcome)
        {
            // 成功・空間・非対象のヒットには異カテゴリー拒否を付けない
            // Found, empty and non-target aim results do not carry the category denial
            var result = outcome == DeleteAimOutcome.Found
                ? DeleteAimResult.Found(new FakeDeleteTarget())
                : DeleteAimResult.Missed(outcome);
            Assert.IsFalse(result.GetDragDenyReason().HasValue);
        }

        [Test]
        public void EmptyHitsReportNothingHit()
        {
            // ヒット0件はどちらの条件でも NothingHit
            // Zero hits are NothingHit under either filter
            Assert.AreEqual(DeleteAimOutcome.NothingHit, DeleteTargetHitSelector.Select(new List<DeleteTargetHit>(), DeleteAimFilter.Frontmost).Outcome);
            Assert.AreEqual(DeleteAimOutcome.NothingHit, DeleteTargetHitSelector.Select(new List<DeleteTargetHit>(), DeleteAimFilter.Category("default")).Outcome);
        }
    }
}
