using System;
using System.Linq;
using Common.Debug;
using Core.Master;
using Core.Update;
using Game.Context;
using Game.Map;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MapModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Tests.Module.TestMod;
using Tests.Util;
using Server.Protocol.PacketResponse;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     採掘のダメージ算出とクールダウンをサーバが握っていることを検証する
    ///     Verifies that the server owns mining damage resolution and the cooldown
    /// </summary>
    public class MapObjectAcquisitionProtocolTest : MapObjectAcquisitionProtocolTestBase
    {

        // 取得個数の抽選が世界共有の乱数でないと、同じスナップショットとパケット列を再生しても取得数がずれる
        // If the drop-count roll does not come from the shared world random, replaying the same snapshot and packets yields different drops
        [Test]
        public void mapObjectの取得個数抽選はGameRandomを引き破壊済みへの再打撃では引かない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var mapObject = GetMapObject(MiningMapObjectGuid);

            var beforeAttack = GameRandomDrawAssert.CurrentState();
            var earnedItems = mapObject.Attack(mapObject.CurrentHp);
            Assert.IsNotEmpty(earnedItems, "HP境界を越えた打撃で取得物が出ていない（テストの前提が崩れている）");
            GameRandomDrawAssert.AssertDrewAtLeastOnce(beforeAttack, "mapObjectの取得個数抽選が世界共有の乱数を引いていない");

            // 破壊済みへの再打撃は取得物を作らないので、1回も引いてはならない
            // Re-hitting a destroyed object creates no drops, so it must not draw at all
            var untouched = GameRandomDrawAssert.StateAfterDraws(0);
            GameRandomDrawAssert.BeginDrawCount();
            Assert.IsEmpty(mapObject.Attack(1), "破壊済みへの再打撃で取得物が出ている");
            GameRandomDrawAssert.AssertDrawn(untouched, "破壊済みへの再打撃が乱数を引いている");
        }

        [Test]
        public void 素手では掘れず対応ツール装備時のみdamage分HPが減り閾値未達では報酬が入らない()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var mapObject = GetMapObject(MiningMapObjectGuid);
            var initialHp = mapObject.CurrentHp;

            // 素手のままではサーバがダメージを解決できずHPは変化しない
            // With bare hands the server resolves no damage, so HP stays untouched
            SendAttack(packet, mapObject.InstanceId);
            Assert.AreEqual(initialHp, mapObject.CurrentHp);

            // 対応ツールを装備して選択するとマスタのdamage分だけHPが減る
            // Equipping and selecting the matching tool reduces HP by the master-defined damage
            EquipTool(playerInventory, ToolItemGuid);
            SendAttack(packet, mapObject.InstanceId);
            Assert.AreEqual(initialHp - ExpectedToolDamage, mapObject.CurrentHp);

            // HP30→23はearnItemHpIntervalの閾値20に届かないため報酬アイテムは入らない
            // HP 30->23 falls short of the earnItemHpInterval threshold 20, so no reward items arrive
            var earnItemId = GetEarnItemId(MiningMapObjectGuid);
            Assert.AreEqual(0, CountMainInventoryItem(playerInventory, earnItemId));
        }

        [Test]
        public void 対応しないツールを装備してもHPは減らず報酬も入らない()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var mapObject = GetMapObject(MiningMapObjectGuid);
            var initialHp = mapObject.CurrentHp;

            // miningToolsに無いアイテムを装備してもどのdamageにも解決されずHPは変化しない
            // An item absent from miningTools resolves to no damage, so HP stays untouched
            EquipTool(playerInventory, UnmatchedToolItemGuid);
            SendAttack(packet, mapObject.InstanceId);
            Assert.AreEqual(initialHp, mapObject.CurrentHp);
            Assert.AreEqual(0, CountMainInventoryItem(playerInventory, GetEarnItemId(MiningMapObjectGuid)));

            // 同じmapObjectでも対応ツールへ持ち替えれば掘れることまで確かめる
            // Confirm the same mapObject is still mineable once the matching tool is equipped
            EquipTool(playerInventory, ToolItemGuid);
            SendAttackAfterCooldown(packet, mapObject.InstanceId);
            Assert.AreEqual(initialHp - ExpectedToolDamage, mapObject.CurrentHp);
        }

        [Test]
        public void 閾値を跨いだ回数だけ報酬アイテムが入る()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var mapObject = GetMapObject(MiningMapObjectGuid);
            var earnItemId = GetEarnItemId(MiningMapObjectGuid);
            EquipTool(playerInventory, ToolItemGuid);

            // 1打目はHP30→23で閾値20に未達なのでアイテムは入らない
            // The first hit takes HP 30->23 and misses threshold 20, so no items arrive
            SendAttack(packet, mapObject.InstanceId);
            Assert.AreEqual(0, CountMainInventoryItem(playerInventory, earnItemId));

            // 2打目でHP16となり閾値20を1回跨ぐ
            // The second hit reaches HP 16 and crosses threshold 20 once
            SendAttackAfterCooldown(packet, mapObject.InstanceId);
            Assert.AreEqual(1, CountMainInventoryItem(playerInventory, earnItemId));

            // 3打目でHP9となり閾値10も跨ぎ累計2回分になる
            // The third hit reaches HP 9 and crosses threshold 10, totalling two rewards
            SendAttackAfterCooldown(packet, mapObject.InstanceId);
            Assert.AreEqual(2, CountMainInventoryItem(playerInventory, earnItemId));
        }
    }
}
