using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface.Subscription;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Inventory
{
    public class SubInventoryModelTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void SetItemsでスロット数が応答のアイテム数になる()
        {
            var model = new SubInventoryModel(new BlockInventorySubInventoryIdentifier(Vector3Int.zero));
            var items = new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty() };

            model.SetItems(items);

            Assert.AreEqual(3, model.Count);
            Assert.IsNull(model.TrainMessage);
        }

        [Test]
        public void SetItemは範囲内スロットだけを書き換える()
        {
            var model = new SubInventoryModel(new BlockInventorySubInventoryIdentifier(Vector3Int.zero));
            model.SetItems(new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty() });
            var itemId = MasterHolder.ItemMaster.GetItemAllIds().First();

            model.SetItem(1, ServerContext.ItemStackFactory.Create(itemId, 5));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("インベントリのサイズを超えています"));
            model.SetItem(2, ServerContext.ItemStackFactory.Create(itemId, 1));

            Assert.AreEqual(5, model.SubInventory[1].Count);
            Assert.AreEqual(2, model.Count);
        }

        [Test]
        public void SetTrainMessageでスロットが空になりエラー種別が残る()
        {
            var model = new SubInventoryModel(new TrainInventorySubInventoryIdentifier(1));
            model.SetItems(new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty() });

            model.SetTrainMessage(TrainInventoryMessageType.ContainerMissing);

            Assert.AreEqual(0, model.Count);
            Assert.AreEqual(TrainInventoryMessageType.ContainerMissing, model.TrainMessage);
        }
    }
}
