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
    public class MapObjectAcquisitionCooldownTest : MapObjectAcquisitionProtocolTestBase
    {

        [Test]
        public void attackSpeed未満の連打は無視される()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var mapObject = GetMapObject(MiningMapObjectGuid);
            var initialHp = mapObject.CurrentHp;
            EquipTool(playerInventory, ToolItemGuid);

            // 1打目は通り、直後の2打目はクールダウンで捨てられる
            // The first hit lands and the immediate second hit is dropped by the cooldown
            SendAttack(packet, mapObject.InstanceId);
            SendAttack(packet, mapObject.InstanceId);
            Assert.AreEqual(initialHp - ExpectedToolDamage, mapObject.CurrentHp);

            // attackSpeed分のtickを進めれば次の打撃は再び通る
            // After advancing attackSpeed worth of ticks the next hit lands again
            AdvanceCooldown();
            SendAttack(packet, mapObject.InstanceId);
            Assert.AreEqual(initialHp - ExpectedToolDamage * 2, mapObject.CurrentHp);
        }

        [Test]
        public void 同じプレイヤーは別mapObjectへ切り替えてもクールダウンを共有する()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var miningService = serviceProvider.GetService<MapObjectMiningService>();
            EquipTool(playerInventory, ToolItemGuid);

            var first = GetMapObject(MiningMapObjectGuid);
            var second = new VanillaStaticMapObject(
                999, MiningMapObjectGuid, false, first.CurrentHp, first.Position + UnityEngine.Vector3.right);
            var secondInitialHp = second.CurrentHp;

            // 対象変更後もプレイヤー単位で待機
            // Cooldown remains player-wide after changing targets
            Assert.AreEqual(MiningAttackResult.Success,
                miningService.TryAttack(PlayerId, first, playerInventory.EquipmentInventory.GetSelectedItem(), playerInventory.MainOpenableInventory, out _));
            Assert.AreEqual(MiningAttackResult.CooldownNotElapsed,
                miningService.TryAttack(PlayerId, second, playerInventory.EquipmentInventory.GetSelectedItem(), playerInventory.MainOpenableInventory, out _));
            Assert.AreEqual(secondInitialHp, second.CurrentHp);
        }

        [Test]
        public void PickUpはツール不要で一撃取得()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var mapObject = GetMapObject(PickUpMapObjectGuid);

            // 素手のまま1回の打撃で破壊され、報酬アイテムがメインインベントリへ入る
            // A single bare-handed hit destroys it and the reward items land in the main inventory
            SendAttack(packet, mapObject.InstanceId);
            Assert.IsTrue(mapObject.IsDestroyed);

            // 跨いだ閾値の回数だけ[minCount, maxCount]の抽選が行われる
            // The [minCount, maxCount] roll happens once per crossed threshold
            var earnItem = GetMinableParam(PickUpMapObjectGuid).EarnItems.items[0];
            var earnedCount = CountMainInventoryItem(playerInventory, MasterHolder.ItemMaster.GetItemId(earnItem.ItemGuid));
            var crossedThresholdCount = CalculateOneHitCrossedThresholdCount(PickUpMapObjectGuid);
            Assert.GreaterOrEqual(earnedCount, earnItem.MinCount * crossedThresholdCount);
            Assert.LessOrEqual(earnedCount, earnItem.MaxCount * crossedThresholdCount);
        }

        [Test]
        public void 高速採掘デバッグ時は素手でもクールダウン無しで破壊される()
        {
            DebugParameters.SaveBool(DebugParameterKeys.MapObjectSuperMine, true);

            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var mapObject = GetMapObject(MiningMapObjectGuid);

            // 素手かつMining型でも高速採掘フラグで一撃破壊される
            // Even bare-handed on a Mining-type object, the super-mine flag destroys it in one hit
            SendAttack(packet, mapObject.InstanceId);
            Assert.IsTrue(mapObject.IsDestroyed);
        }
    }
}
