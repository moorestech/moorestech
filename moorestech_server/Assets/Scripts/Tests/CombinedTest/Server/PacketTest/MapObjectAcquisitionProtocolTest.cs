using System;
using System.Linq;
using System.Threading;
using Common.Debug;
using Core.Master;
using Game.Context;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.MapObjectAcquisitionProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     採掘のダメージ算出とクールダウンをサーバが握っていることを検証する
    ///     Verifies that the server owns mining damage resolution and the cooldown
    /// </summary>
    public class MapObjectAcquisitionProtocolTest
    {
        private const int PlayerId = 0;

        // テストマスタのMining型mapObject(hp30 / damage7 / attackSpeed0.2)とその対応ツール
        // The Mining-type mapObject in the test master (hp30 / damage7 / attackSpeed0.2) and its matching tool
        private static readonly Guid MiningMapObjectGuid = Guid.Parse("00000000-0000-2222-0000-000000000001");
        private static readonly Guid PickUpMapObjectGuid = Guid.Parse("8c0e1339-be75-4690-99cd-58b5385a17cd");
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private const int ExpectedToolDamage = 7;
        private const double ExpectedAttackSpeed = 0.2;

        [TearDown]
        public void TearDown()
        {
            // 高速採掘フラグの残置は他テストを無言で壊すため必ず消す
            // A leftover super-mine flag silently breaks other tests, so always remove it
            DebugParameters.RemoveBool(DebugParameterKeys.MapObjectSuperMine);
        }

        [Test]
        public void 対応ツール装備時のみサーバがダメージを算出して掘れる()
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
            EquipTool(playerInventory);
            SendAttack(packet, mapObject.InstanceId);
            Assert.AreEqual(initialHp - ExpectedToolDamage, mapObject.CurrentHp);
        }

        [Test]
        public void attackSpeed未満の連打は無視される()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var mapObject = GetMapObject(MiningMapObjectGuid);
            var initialHp = mapObject.CurrentHp;
            EquipTool(playerInventory);

            // 1打目は通り、直後の2打目はクールダウンで捨てられる
            // The first hit lands and the immediate second hit is dropped by the cooldown
            SendAttack(packet, mapObject.InstanceId);
            SendAttack(packet, mapObject.InstanceId);
            Assert.AreEqual(initialHp - ExpectedToolDamage, mapObject.CurrentHp);

            // attackSpeed分待てば次の打撃は再び通る
            // After waiting attackSpeed the next hit lands again
            Thread.Sleep((int)(ExpectedAttackSpeed * 1000) + 100);
            SendAttack(packet, mapObject.InstanceId);
            Assert.AreEqual(initialHp - ExpectedToolDamage * 2, mapObject.CurrentHp);
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

            var mapObjectElement = MasterHolder.MapObjectMaster.GetMapObjectElement(PickUpMapObjectGuid);
            var expectedItemId = MasterHolder.ItemMaster.GetItemId(mapObjectElement.EarnItems[0].ItemGuid);
            var mainInventory = playerInventory.MainOpenableInventory;
            var earnedCount = Enumerable.Range(0, mainInventory.GetSlotSize()).
                Where(slot => mainInventory.GetItem(slot).Id == expectedItemId).
                Sum(slot => mainInventory.GetItem(slot).Count);
            Assert.Greater(earnedCount, 0);
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

        private IMapObject GetMapObject(Guid mapObjectGuid)
        {
            return ServerContext.MapObjectDatastore.MapObjects.First(mapObject => mapObject.MapObjectGuid == mapObjectGuid);
        }

        private void EquipTool(PlayerInventoryData playerInventory)
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            playerInventory.EquipmentInventory.SetItem(0, toolItemId, 1);
            playerInventory.EquipmentInventory.SetSelectedEquipmentIndex(0);
        }

        private void SendAttack(PacketResponseCreator packet, int instanceId)
        {
            var messagePack = new GetMapObjectProtocolProtocolMessagePack(PlayerId, instanceId);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack), new PacketResponseContext(null));
        }
    }
}
