using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
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

        [Test]
        public void マスタ由来の設置対象がGuidで解決できる()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
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
        public void Kind群の連続性とSortPriorityの単調性が保たれる()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = new PlacementTargetCatalog(new EmptyBlueprintSource());
            var entries = catalog.Entries;

            // Kind群はBlock→TrainCar→ConnectTool→BuildTool→Blueprintの順で連続していること
            // Kind groups appear contiguously in Block→TrainCar→ConnectTool→BuildTool→Blueprint order
            AssertKindGroupsContiguousAndOrdered(entries);

            // Block部分列のSortPriorityが単調非減少であること
            // The Block subsequence's SortPriority is monotonically non-decreasing
            AssertMonotonicNonDecreasing(entries.Where(e => e.Kind == PlacementTargetKind.Block)
                .Select(e => MasterHolder.BlockMaster.GetBlockMaster(e.Id).SortPriority ?? 0));

            // ConnectTool部分列のSortPriorityが単調非減少であること
            // The ConnectTool subsequence's SortPriority is monotonically non-decreasing
            AssertMonotonicNonDecreasing(entries.Where(e => e.Kind == PlacementTargetKind.ConnectTool)
                .Select(e => MasterHolder.ConnectToolMaster.GetElementOrNull(e.Id).SortPriority));

            #region Internal

            void AssertKindGroupsContiguousAndOrdered(IReadOnlyList<PlacementTargetEntry> targetEntries)
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

            void AssertMonotonicNonDecreasing(IEnumerable<int> values)
            {
                var previous = int.MinValue;
                foreach (var value in values)
                {
                    Assert.GreaterOrEqual(value, previous, "SortPriority is not monotonically non-decreasing");
                    previous = value;
                }
            }

            #endregion
        }

        [Test]
        public void サーバDIのカタログはBlueprintDatastoreのBPを含む()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();
            var catalog = serviceProvider.GetService<PlacementTargetCatalog>();

            var guid = datastore.Register(new BlueprintJsonObject("カタログ確認用", new List<BlueprintBlockJsonObject>()));

            Assert.IsTrue(catalog.TryGetEntry(guid, out var entry));
            Assert.AreEqual(PlacementTargetKind.Blueprint, entry.Kind);
        }
    }
}
