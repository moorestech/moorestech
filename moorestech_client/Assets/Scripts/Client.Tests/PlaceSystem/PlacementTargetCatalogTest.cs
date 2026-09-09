using System;
using System.Collections.Generic;
using System.Linq;
using Game.PlacementTarget;
using Core.Master;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem
{
    public class PlacementTargetCatalogTest
    {
        // BPを1件も持たない状態
        // The state with no blueprints at all
        private static readonly (Guid id, string name)[] NoBlueprints = Array.Empty<(Guid, string)>();

        [SetUp]
        public void SetUp()
        {
            // MasterHolderの静的初期化だけが目的で戻り値は使わない
            // Only used to initialize the static MasterHolder; the return values are unused
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void マスタ由来の設置対象がGuidで列挙される()
        {
            var catalog = new PlacementTargetCatalog();
            var entries = catalog.CreateEntries(NoBlueprints);

            // 主要設置種を検証
            // Verify the main target kinds
            Assert.IsTrue(entries.Any(e => e.Kind == PlacementTargetKind.Block));
            Assert.IsTrue(entries.Any(e => e.Kind == PlacementTargetKind.TrainCar));
            Assert.IsTrue(entries.Any(e => e.Kind == PlacementTargetKind.ConnectTool));
            Assert.IsTrue(entries.Any(e => e.Kind == PlacementTargetKind.BlueprintCopy));
        }

        [Test]
        public void Kind群の連続性と登場順が保たれる()
        {
            var catalog = new PlacementTargetCatalog();

            // Kind群はBlock→TrainCar→ConnectTool→BlueprintCopy→Blueprintの順で連続していること
            // Kind groups appear contiguously in Block→TrainCar→ConnectTool→BlueprintCopy→Blueprint order
            AssertKindGroupsContiguousAndOrdered(catalog.CreateEntries(NoBlueprints));
        }

        [Test]
        public void Blockの並び順がSortPriorityと名前で固定される()
        {
            var catalog = new PlacementTargetCatalog();

            // 実装式の複製で現在の並び順をピン留めする（並べ替え規則自体の正しさは検証しない）
            // This duplicates the implementation's expression to pin the current order (does not validate the ordering rule itself)
            var expected = MasterHolder.BlockMaster.Blocks.Data
                .Where(block => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(block.BlockGuid))
                .OrderBy(block => block.SortPriority ?? 0)
                .ThenBy(block => block.Name)
                .Select(block => block.BlockGuid)
                .ToList();

            var actual = catalog.CreateEntries(NoBlueprints)
                .Where(e => e.Kind == PlacementTargetKind.Block)
                .Select(e => e.Id)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void ConnectToolの並び順がSortPriorityで固定される()
        {
            // forUnitTestのconnectTools配列は意図的に非SortPriority順（120→100→110）。昇順に整えるとこのテストが同語反復化する
            // The forUnitTest connectTools array is deliberately not in SortPriority order; sorting it would make this test tautological
            var catalog = new PlacementTargetCatalog();

            // 実装式の複製で現在の並び順をピン留めする（並べ替え規則自体の正しさは検証しない）
            // This duplicates the implementation's expression to pin the current order (does not validate the ordering rule itself)
            var expected = MasterHolder.ConnectToolMaster.All
                .OrderBy(connectTool => connectTool.SortPriority)
                .Select(connectTool => connectTool.ConnectToolGuid)
                .ToList();

            var actual = catalog.CreateEntries(NoBlueprints)
                .Where(e => e.Kind == PlacementTargetKind.ConnectTool)
                .Select(e => e.Id)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void Blueprint群はBPコピーより後ろの末尾に来る()
        {
            var blueprints = new[] { (id: Guid.NewGuid(), name: "スタブBP1"), (id: Guid.NewGuid(), name: "スタブBP2") };
            var entries = new PlacementTargetCatalog().CreateEntries(blueprints);

            // BPを2件渡しても群の登場順（BPコピーの後）が保たれること
            // Group order (Blueprint after BlueprintCopy) holds even with real blueprint entries present
            AssertKindGroupsContiguousAndOrdered(entries);

            // 末尾N件がBlueprintで、渡した(id, name)と同じ順で並ぶこと
            // The trailing N entries are Blueprint kind, matching the passed (id, name) order
            var tail = entries.Skip(entries.Count - blueprints.Length).ToList();
            CollectionAssert.AreEqual(blueprints.Select(b => b.id).ToList(), tail.Select(e => e.Id).ToList());
            Assert.IsTrue(tail.All(e => e.Kind == PlacementTargetKind.Blueprint));
        }

        [Test]
        public void 種別横断のGuid衝突は例外になる()
        {
            // マスタのブロックGuidをBPにも混入させ、Kindが違っても救済されないことを確認する
            // Inject a master block guid into the blueprint source: a differing Kind must not rescue the collision
            var blockGuid = MasterHolder.BlockMaster.Blocks.Data.First().BlockGuid;
            AssertEntriesThrowContaining(new[] { (blockGuid, "衝突BP") }, blockGuid.ToString());
        }

        [Test]
        public void BP同士のGuid重複は例外になる()
        {
            var duplicatedGuid = Guid.NewGuid();
            AssertEntriesThrowContaining(new[] { (duplicatedGuid, "BP1"), (duplicatedGuid, "BP2") }, duplicatedGuid.ToString());
        }

        [Test]
        public void GuidEmptyのエントリは例外になる()
        {
            AssertEntriesThrowContaining(new[] { (Guid.Empty, "空GuidBP") }, "空GuidBP");
        }

        // 例外メッセージが違反エントリを名指ししていることまで確認する
        // Also verifies the exception message names the offending entry
        private static void AssertEntriesThrowContaining((Guid id, string name)[] blueprintEntries, string expectedInMessage)
        {
            var catalog = new PlacementTargetCatalog();
            var exception = Assert.Throws<InvalidOperationException>(() => catalog.CreateEntries(blueprintEntries));
            Assert.That(exception.Message, Does.Contain(expectedInMessage));
        }

        private static void AssertKindGroupsContiguousAndOrdered(IReadOnlyList<PlacementTargetEntry> targetEntries)
        {
            // 実データに存在しないKindがあっても壊れないよう、登場した群だけを期待順と突き合わせる
            // Only compare groups that actually appear, so a missing Kind in real data never breaks this
            var expectedOrder = new[] { PlacementTargetKind.Block, PlacementTargetKind.TrainCar, PlacementTargetKind.ConnectTool, PlacementTargetKind.BlueprintCopy, PlacementTargetKind.Blueprint };
            var groupOrder = new List<PlacementTargetKind>();
            foreach (var entry in targetEntries)
            {
                if (0 < groupOrder.Count && groupOrder[^1] == entry.Kind) continue;
                Assert.IsFalse(groupOrder.Contains(entry.Kind), $"Kind {entry.Kind} appears in more than one group");
                groupOrder.Add(entry.Kind);
            }

            var lastIndex = -1;
            foreach (var kind in groupOrder)
            {
                var index = Array.IndexOf(expectedOrder, kind);
                Assert.Greater(index, lastIndex, $"Kind {kind} is out of the expected order");
                lastIndex = index;
            }
        }
    }
}
