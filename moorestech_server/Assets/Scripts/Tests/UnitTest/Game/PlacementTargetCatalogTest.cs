using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.PlacementTarget;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class PlacementTargetCatalogTest
    {
        private class EmptyBlueprintSource : IBlueprintCatalogSource
        {
            public IReadOnlyList<(Guid id, string name)> BlueprintEntries => new List<(Guid, string)>();
        }

        private class StubBlueprintSource : IBlueprintCatalogSource
        {
            public IReadOnlyList<(Guid id, string name)> BlueprintEntries { get; } = new List<(Guid, string)>
            {
                (Guid.NewGuid(), "スタブBP1"),
                (Guid.NewGuid(), "スタブBP2"),
            };
        }

        [Test]
        public void マスタ由来の設置対象がGuidで解決できる()
        {
            // MasterHolderの静的初期化だけが目的で戻り値は使わない
            // Only used to initialize the static MasterHolder; the return values are unused
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = new PlacementTargetCatalog(new EmptyBlueprintSource());

            // ブロック・車両・接続ツール・ビルドツールが全部エントリに入っている
            // Blocks, train cars, connect tools, and build tools are all present
            Assert.IsTrue(catalog.Entries.Any(e => e.Kind == PlacementTargetKind.Block));
            Assert.IsTrue(catalog.Entries.Any(e => e.Kind == PlacementTargetKind.ConnectTool));
            Assert.IsTrue(catalog.Entries.Any(e => e.Kind == PlacementTargetKind.BuildTool));

            // 任意のエントリはTryGetEntryで往復できる
            // Every entry round-trips through TryGetEntry
            foreach (var entry in catalog.Entries)
            {
                Assert.IsTrue(catalog.TryGetEntry(entry.Id, out var resolved));
                Assert.AreEqual(entry.Kind, resolved.Kind);
            }

            // 未知のGuidは解決できない
            // Unknown GUIDs do not resolve
            Assert.IsFalse(catalog.TryGetEntry(Guid.NewGuid(), out _));
        }

        [Test]
        public void Kind群の連続性と登場順が保たれる()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = new PlacementTargetCatalog(new EmptyBlueprintSource());

            // Kind群はBlock→TrainCar→ConnectTool→BuildTool→Blueprintの順で連続していること
            // Kind groups appear contiguously in Block→TrainCar→ConnectTool→BuildTool→Blueprint order
            AssertKindGroupsContiguousAndOrdered(catalog.Entries);
        }

        [Test]
        public void Blockの並び順がSortPriorityと名前で固定される()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = new PlacementTargetCatalog(new EmptyBlueprintSource());

            // 実装式の複製で現在の並び順をピン留めする（並べ替え規則自体の正しさは検証しない）
            // This duplicates the implementation's expression to pin the current order (does not validate the ordering rule itself)
            var expected = MasterHolder.BlockMaster.Blocks.Data
                .Where(block => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(block.BlockGuid))
                .OrderBy(block => block.SortPriority ?? 0)
                .ThenBy(block => block.Name)
                .Select(block => block.BlockGuid)
                .ToList();

            var actual = catalog.Entries
                .Where(e => e.Kind == PlacementTargetKind.Block)
                .Select(e => e.Id)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void ConnectToolの並び順がSortPriorityで固定される()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var catalog = new PlacementTargetCatalog(new EmptyBlueprintSource());

            // 実装式の複製で現在の並び順をピン留めする（並べ替え規則自体の正しさは検証しない）
            // This duplicates the implementation's expression to pin the current order (does not validate the ordering rule itself)
            var expected = MasterHolder.ConnectToolMaster.All
                .OrderBy(connectTool => connectTool.SortPriority)
                .Select(connectTool => connectTool.ConnectToolGuid)
                .ToList();

            var actual = catalog.Entries
                .Where(e => e.Kind == PlacementTargetKind.ConnectTool)
                .Select(e => e.Id)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void Blueprint群はBuildToolより後ろの末尾に来る()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blueprintSource = new StubBlueprintSource();
            var catalog = new PlacementTargetCatalog(blueprintSource);
            var entries = catalog.Entries;

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
        public void サーバDIのカタログはBlueprintDatastoreのBPを含む()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();
            var catalog = serviceProvider.GetService<PlacementTargetCatalog>();

            var guid = datastore.Register(new BlueprintJsonObject("カタログ確認用", new List<BlueprintBlockJsonObject>()));

            Assert.IsTrue(catalog.TryGetEntry(guid, out var entry));
            Assert.AreEqual(PlacementTargetKind.Blueprint, entry.Kind);
        }

        private static void AssertKindGroupsContiguousAndOrdered(IReadOnlyList<PlacementTargetEntry> targetEntries)
        {
            // 実データに存在しないKindがあっても壊れないよう、登場した群だけを期待順と突き合わせる
            // Only compare groups that actually appear, so a missing Kind in real data never breaks this
            var expectedOrder = new[] { PlacementTargetKind.Block, PlacementTargetKind.TrainCar, PlacementTargetKind.ConnectTool, PlacementTargetKind.BuildTool, PlacementTargetKind.Blueprint };
            var groupOrder = new List<PlacementTargetKind>();
            foreach (var entry in targetEntries)
            {
                if (groupOrder.Count > 0 && groupOrder[^1] == entry.Kind) continue;
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
