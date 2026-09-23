using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar.Cost;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.TrainCar
{
    /// <summary>
    /// 車両コスト判定を検証する
    /// - RequiredItems 1セット
    /// - 財布を使わない
    /// Verifies the train-car cost gate:
    /// - One RequiredItems set
    /// - No wallet
    /// </summary>
    public class TrainCarConstructionCostShortageTest
    {
        // 費用: Test3×3、Test4×2
        // Cost: Test3 x3; Test4 x2.
        private static readonly Guid TestTrainCarGuid = Guid.Parse("dc82cf3f-709d-49eb-bdb2-67ffcaff561b");
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [SetUp]
        public void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 片方の素材だけ足りなければその素材だけが不足になる()
        {
            var shortages = TrainCarConstructionCostShortage.Calculate(TestTrainCarGuid, BuildInventory(3, 1));

            Assert.AreEqual(1, shortages.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Material2Guid), shortages[0].ItemId);
            Assert.AreEqual(1, shortages[0].Held);
            Assert.AreEqual(2, shortages[0].Required);
        }

        [Test]
        public void 全素材が足りていれば不足は空()
        {
            Assert.IsEmpty(TrainCarConstructionCostShortage.Calculate(TestTrainCarGuid, BuildInventory(3, 2)));
        }

        [Test]
        public void 何も持っていなければ全素材が不足になる()
        {
            Assert.AreEqual(2, TrainCarConstructionCostShortage.Calculate(TestTrainCarGuid, new List<IItemStack>()).Count);
        }

        private static List<IItemStack> BuildInventory(int material1Count, int material2Count)
        {
            return new List<IItemStack>
            {
                ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), material1Count),
                ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), material2Count),
            };
        }
    }
}
