using System;
using System.Linq;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Game.Blueprint;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.TestTools;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.PlacementTarget;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem
{
    public class PlacementTargetFactoryTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 本体未同期のBPはターゲットを生成しない()
        {
            var id = Guid.NewGuid();
            var entry = new PlacementTargetEntry(id, PlacementTargetKind.Blueprint, "missing", id);
            LogAssert.Expect(LogType.Warning, $"[PlacementTargetFactory] blueprint {id} has no synchronized body; target omitted");
            Assert.IsFalse(PlacementTargetFactory.TryCreate(entry, new ClientBlueprintLibrary(), out var target));
            Assert.IsNull(target);
        }

        [Test]
        public void TryCreateはKindごとに対応する型とIdを持つターゲットを生成する()
        {
            var blockGuid = MasterHolder.BlockMaster.Blocks.Data.First().BlockGuid;
            AssertCreated(PlacementTargetKind.Block, blockGuid, typeof(BlockPlacementTarget));
            AssertCreated(PlacementTargetKind.TrainCar, Guid.NewGuid(), typeof(TrainCarPlacementTarget));
            AssertCreated(PlacementTargetKind.ConnectTool, Guid.NewGuid(), typeof(ConnectToolPlacementTarget));
            AssertCreated(PlacementTargetKind.BlueprintCopy, Guid.NewGuid(), typeof(BlueprintCopyPlacementTarget));
            AssertCreated(PlacementTargetKind.Blueprint, Guid.NewGuid(), typeof(BlueprintPlacementTarget));

            #region Internal

            void AssertCreated(PlacementTargetKind kind, Guid id, Type expectedTargetType)
            {
                var entry = new PlacementTargetEntry(id, kind, "placement-target-factory-test", id);
                var library = new ClientBlueprintLibrary();
                var blueprint = new BlueprintJsonObject("placement-target-factory-test", new(), new(), new(), id);
                ((IList<BlueprintMessagePack>)library.Blueprints).Add(new BlueprintMessagePack(blueprint));
                Assert.IsTrue(PlacementTargetFactory.TryCreate(entry, library, out var target));
                Assert.IsInstanceOf(expectedTargetType, target, $"{kind} should resolve to {expectedTargetType.Name}");
                Assert.AreEqual(id, target.Id, $"{kind} target id should round-trip from entry.Id");
                Assert.AreEqual(kind, target.Kind, $"{kind} target should carry its catalog kind");
                if (target is BlueprintPlacementTarget saved) Assert.AreEqual(id, saved.Blueprint.BlueprintGuid);
            }

            #endregion
        }
    }
}
