using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.PlacementTarget;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class PlacementTargetCatalogTest
    {
        // 任意のBP群を供給する唯一のスタブ（空・複数件・Guid衝突・Empty混入をすべて表現する）
        // The single blueprint stub, expressing empty / multi-entry / guid-collision / empty-guid cases alike
        private class ConfigurableBlueprintSource : IBlueprintCatalogSource
        {
            public IReadOnlyList<(Guid id, string name)> BlueprintEntries { get; }
            public ConfigurableBlueprintSource(params (Guid id, string name)[] entries) { BlueprintEntries = entries; }
        }

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
            var catalog = new PlacementTargetCatalog(new ConfigurableBlueprintSource());
            var entries = catalog.CreateEntries();

            // ブロック・車両・接続ツール・ビルドツールが全部エントリに入っている
            // Blocks, train cars, connect tools, and build tools are all present
            Assert.IsTrue(entries.Any(e => e.Kind == PlacementTargetKind.Block));
            Assert.IsTrue(entries.Any(e => e.Kind == PlacementTargetKind.ConnectTool));
            Assert.IsTrue(entries.Any(e => e.Kind == PlacementTargetKind.BuildTool));
        }

        [Test]
        public void Kind群の連続性と登場順が保たれる()
        {
            var catalog = new PlacementTargetCatalog(new ConfigurableBlueprintSource());

            // Kind群はBlock→TrainCar→ConnectTool→BuildTool→Blueprintの順で連続していること
            // Kind groups appear contiguously in Block→TrainCar→ConnectTool→BuildTool→Blueprint order
            AssertKindGroupsContiguousAndOrdered(catalog.CreateEntries());
        }

        [Test]
        public void Blockの並び順がSortPriorityと名前で固定される()
        {
            var catalog = new PlacementTargetCatalog(new ConfigurableBlueprintSource());

            // 実装式の複製で現在の並び順をピン留めする（並べ替え規則自体の正しさは検証しない）
            // This duplicates the implementation's expression to pin the current order (does not validate the ordering rule itself)
            var expected = MasterHolder.BlockMaster.Blocks.Data
                .Where(block => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(block.BlockGuid))
                .OrderBy(block => block.SortPriority ?? 0)
                .ThenBy(block => block.Name)
                .Select(block => block.BlockGuid)
                .ToList();

            var actual = catalog.CreateEntries()
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
            var catalog = new PlacementTargetCatalog(new ConfigurableBlueprintSource());

            // 実装式の複製で現在の並び順をピン留めする（並べ替え規則自体の正しさは検証しない）
            // This duplicates the implementation's expression to pin the current order (does not validate the ordering rule itself)
            var expected = MasterHolder.ConnectToolMaster.All
                .OrderBy(connectTool => connectTool.SortPriority)
                .Select(connectTool => connectTool.ConnectToolGuid)
                .ToList();

            var actual = catalog.CreateEntries()
                .Where(e => e.Kind == PlacementTargetKind.ConnectTool)
                .Select(e => e.Id)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void Blueprint群はBuildToolより後ろの末尾に来る()
        {
            var blueprintSource = new ConfigurableBlueprintSource((Guid.NewGuid(), "スタブBP1"), (Guid.NewGuid(), "スタブBP2"));
            var catalog = new PlacementTargetCatalog(blueprintSource);
            var entries = catalog.CreateEntries();

            // BPを2件持つ供給元でも群の登場順（BuildToolの後）が保たれること
            // Group order (Blueprint after BuildTool) holds even with real blueprint entries present
            AssertKindGroupsContiguousAndOrdered(entries);

            // 末尾N件がBlueprintで、供給元の(id, name)と同じ順で並ぶこと
            // The trailing N entries are Blueprint kind, matching the source's (id, name) order
            var tail = entries.Skip(entries.Count - blueprintSource.BlueprintEntries.Count).ToList();
            CollectionAssert.AreEqual(blueprintSource.BlueprintEntries.Select(b => b.id).ToList(), tail.Select(e => e.Id).ToList());
            Assert.IsTrue(tail.All(e => e.Kind == PlacementTargetKind.Blueprint));
        }

        [Test]
        public void 種別横断のGuid衝突は例外になる()
        {
            // マスタのブロックGuidをBPにも混入させ、Kindが違っても救済されないことを確認する
            // Inject a master block guid into the blueprint source: a differing Kind must not rescue the collision
            var blockGuid = MasterHolder.BlockMaster.Blocks.Data.First().BlockGuid;
            AssertEntriesThrowContaining(new ConfigurableBlueprintSource((blockGuid, "衝突BP")), blockGuid.ToString());
        }

        [Test]
        public void BP同士のGuid重複は例外になる()
        {
            var duplicatedGuid = Guid.NewGuid();
            AssertEntriesThrowContaining(new ConfigurableBlueprintSource((duplicatedGuid, "BP1"), (duplicatedGuid, "BP2")), duplicatedGuid.ToString());
        }

        [Test]
        public void GuidEmptyのエントリは例外になる()
        {
            AssertEntriesThrowContaining(new ConfigurableBlueprintSource((Guid.Empty, "空GuidBP")), "空GuidBP");
        }

        // 例外メッセージが違反エントリを名指ししていることまで確認する
        // Also verifies the exception message names the offending entry
        private static void AssertEntriesThrowContaining(IBlueprintCatalogSource blueprintSource, string expectedInMessage)
        {
            var catalog = new PlacementTargetCatalog(blueprintSource);
            var exception = Assert.Throws<InvalidOperationException>(() => catalog.CreateEntries());
            Assert.That(exception.Message, Does.Contain(expectedInMessage));
        }

        private static void AssertKindGroupsContiguousAndOrdered(IReadOnlyList<PlacementTargetEntry> targetEntries)
        {
            // 実データに存在しないKindがあっても壊れないよう、登場した群だけを期待順と突き合わせる
            // Only compare groups that actually appear, so a missing Kind in real data never breaks this
            var expectedOrder = new[] { PlacementTargetKind.Block, PlacementTargetKind.TrainCar, PlacementTargetKind.ConnectTool, PlacementTargetKind.BuildTool, PlacementTargetKind.Blueprint };
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
