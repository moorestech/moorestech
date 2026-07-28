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
