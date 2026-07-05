using System;
using Core.Item;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core
{
    public class ItemStackLevelDataStoreTest
    {
        // ForUnitTest モッドの Test1 アイテム（テーブル: [100, 200, 300]）
        // Test1 item in the ForUnitTest mod (table: [100, 200, 300])
        private static readonly Guid Test1ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void DefaultLevelIsOneAndMaxStackIsFirstTableEntryTest()
        {
            Assert.AreEqual(1, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
            Assert.AreEqual(100, ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.ItemId1));
        }

        [Test]
        public void UnlockStackLevelIncreasesMaxStackTest()
        {
            ItemStackLevelDataStore.Instance.UnlockStackLevel(Test1ItemGuid, 2);
            Assert.AreEqual(2, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
            Assert.AreEqual(200, ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.ItemId1));
        }

        [Test]
        public void UnlockIsIdempotentAndNeverDowngradesTest()
        {
            ItemStackLevelDataStore.Instance.UnlockStackLevel(Test1ItemGuid, 3);
            ItemStackLevelDataStore.Instance.UnlockStackLevel(Test1ItemGuid, 3);
            ItemStackLevelDataStore.Instance.UnlockStackLevel(Test1ItemGuid, 2);
            Assert.AreEqual(3, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
            Assert.AreEqual(300, ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.ItemId1));
        }

        [Test]
        public void UnlockLevelIsClampedToTableLengthTest()
        {
            ItemStackLevelDataStore.Instance.UnlockStackLevel(Test1ItemGuid, 99);
            Assert.AreEqual(3, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
        }

        [Test]
        public void SaveLoadRestoresLevelsTest()
        {
            ItemStackLevelDataStore.Instance.UnlockStackLevel(Test1ItemGuid, 2);
            var saved = ItemStackLevelDataStore.Instance.GetSaveJsonObject();

            // 新しいDIコンテナで復元
            // Restore in a fresh DI container
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.AreEqual(1, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));

            ItemStackLevelDataStore.Instance.LoadUnlockedLevels(saved);
            Assert.AreEqual(2, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
        }
    }
}
