using System;
using Core.Master;
using Game.Context;
using Game.Map;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    /// <summary>
    ///     装飾物(miningType None)への攻撃がサーバーで拒否されることを検証する
    ///     Verifies the server rejects an attack on a decoration (miningType None)
    /// </summary>
    public class MapObjectNotInteractableMiningTest
    {
        private const int PlayerId = 0;

        // テストマスタの装飾物。配置には無いのでファクトリで生成する
        // The decoration in the test master; it has no placement, so the factory creates it
        private static readonly Guid DecorationMapObjectGuid = Guid.Parse("00000000-0000-4444-0000-000000000001");

        [Test]
        public void 装飾物への攻撃はNotInteractableで拒否されHPも減らない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var miningService = serviceProvider.GetService<MapObjectMiningService>();
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var decoration = serviceProvider.GetService<IMapObjectFactory>().Create(100, DecorationMapObjectGuid, 10, false, Vector3.zero);

            // 石の斧を装備していても装飾物は削れない
            // Even with a stone axe equipped, a decoration cannot be worn down
            var axeId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            var equippedItem = ServerContext.ItemStackFactory.Create(axeId, 1);
            var result = miningService.TryAttack(PlayerId, decoration, equippedItem, playerInventory.MainOpenableInventory, out var earnedItems);

            Assert.AreEqual(MiningAttackResult.NotInteractable, result);
            Assert.IsNull(earnedItems);
            Assert.IsFalse(decoration.IsDestroyed);
            Assert.AreEqual(10, decoration.CurrentHp);
        }
    }
}
