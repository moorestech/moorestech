using System;
using Core.Item;
using Microsoft.Extensions.DependencyInjection;
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

        // 変更系はDI経由でのみ公開されるため、コンテナから具象ストアを取得する
        // Mutations are DI-only, so resolve the concrete store from the container
        private ItemStackLevelDataStore _dataStore;

        [SetUp]
        public void Setup()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _dataStore = serviceProvider.GetService<ItemStackLevelDataStore>();
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
            _dataStore.UnlockStackLevel(Test1ItemGuid, 2);
            Assert.AreEqual(2, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
            Assert.AreEqual(200, ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.ItemId1));
        }

        [Test]
        public void UnlockIsIdempotentAndNeverDowngradesTest()
        {
            _dataStore.UnlockStackLevel(Test1ItemGuid, 3);
            _dataStore.UnlockStackLevel(Test1ItemGuid, 3);
            _dataStore.UnlockStackLevel(Test1ItemGuid, 2);
            Assert.AreEqual(3, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
            Assert.AreEqual(300, ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.ItemId1));
        }

        [Test]
        public void UnlockLevelIsClampedToTableLengthTest()
        {
            _dataStore.UnlockStackLevel(Test1ItemGuid, 99);
            Assert.AreEqual(3, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
        }

        [Test]
        public void SaveLoadRestoresLevelsTest()
        {
            _dataStore.UnlockStackLevel(Test1ItemGuid, 2);
            var saved = _dataStore.GetSaveJsonObject();

            // 新しいDIコンテナで復元
            // Restore in a fresh DI container
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var loadDataStore = loadServiceProvider.GetService<ItemStackLevelDataStore>();
            Assert.AreEqual(1, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));

            loadDataStore.LoadUnlockedLevels(saved);
            Assert.AreEqual(2, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
        }
    }
}
