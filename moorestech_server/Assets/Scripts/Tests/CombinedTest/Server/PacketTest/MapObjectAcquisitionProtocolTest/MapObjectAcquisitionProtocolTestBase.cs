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
    public abstract class MapObjectAcquisitionProtocolTestBase
    {
        protected const int PlayerId = 1;

        // テストマスタのMining型mapObject(hp30 / damage7 / attackSpeed0.2)とその対応ツール
        // The Mining-type mapObject in the test master (hp30 / damage7 / attackSpeed0.2) and its matching tool
        protected static readonly Guid MiningMapObjectGuid = Guid.Parse("00000000-0000-2222-0000-000000000001");
        protected static readonly Guid PickUpMapObjectGuid = Guid.Parse("8c0e1339-be75-4690-99cd-58b5385a17cd");
        protected static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // TestMiningRockのminingToolsには無いアイテム
        // An item absent from TestMiningRock's miningTools
        protected static readonly Guid UnmatchedToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        protected const int ExpectedToolDamage = 7;
        protected const double ExpectedAttackSpeed = 0.2;

        [SetUp]
        public void SetUp()
        {
            // 高速採掘フラグを各テスト開始前にも除去し、前回異常終了の残置を隔離する
            // Remove the super-mine flag before each test too, isolating residue from an aborted prior test
            DebugParameters.RemoveBool(DebugParameterKeys.MapObjectSuperMine);
        }

        [TearDown]
        public void TearDown()
        {
            // 高速採掘フラグの残置は他テストを無言で壊すため必ず消す
            // A leftover super-mine flag silently breaks other tests, so always remove it
            DebugParameters.RemoveBool(DebugParameterKeys.MapObjectSuperMine);
        }

        protected IMapObject GetMapObject(Guid mapObjectGuid)
        {
            return ServerContext.MapObjectDatastore.MapObjects.First(mapObject => mapObject.MapObjectGuid == mapObjectGuid);
        }

        protected void EquipTool(PlayerInventoryData playerInventory, Guid toolItemGuid)
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(toolItemGuid);
            playerInventory.EquipmentInventory.SetItem(0, toolItemId, 1);
            playerInventory.EquipmentInventory.SetSelectedEquipmentIndex(0);
        }

        protected void SendAttack(PacketResponseCreator packet, int instanceId)
        {
            var messagePack = MiningProtocol.MiningProtocolMessagePack.CreateMapObjectRequest(instanceId);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack), Tests.Util.BoundPacketContext.Bind(PlayerId));
        }

        protected void SendAttackAfterCooldown(PacketResponseCreator packet, int instanceId)
        {
            AdvanceCooldown();
            SendAttack(packet, instanceId);
        }

        // サーバの経過時間はGameUpdaterのtickでしか進まないのでtickを直接進める
        // Server-side elapsed time only advances by GameUpdater ticks, so advance ticks directly
        protected void AdvanceCooldown()
        {
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(ExpectedAttackSpeed) + 1);
        }

        protected ItemId GetEarnItemId(Guid mapObjectGuid)
        {
            return MasterHolder.ItemMaster.GetItemId(GetMinableParam(mapObjectGuid).EarnItems.items[0].ItemGuid);
        }

        /// <summary>
        ///     採掘設定は判別子の内側にあるため、採掘できる個体としてほどいてから読む
        ///     The mining settings live inside the discriminator, so they are unwrapped as a minable object first
        /// </summary>
        protected static IMinableMapObjectParam GetMinableParam(Guid mapObjectGuid)
        {
            return (IMinableMapObjectParam)MasterHolder.MapObjectMaster.GetMapObjectElement(mapObjectGuid).MiningParam;
        }

        /// <summary>
        ///     一撃破壊がearnItemHpIntervalの閾値を何回跨ぐかをマスタのhpから導く
        ///     Derives from the master hp how many earnItemHpInterval thresholds a one-hit kill crosses
        /// </summary>
        protected int CalculateOneHitCrossedThresholdCount(Guid mapObjectGuid)
        {
            var minableParam = GetMinableParam(mapObjectGuid);
            return (minableParam.Hp - 1) / minableParam.EarnItemHpInterval + 1;
        }

        protected int CountMainInventoryItem(PlayerInventoryData playerInventory, ItemId itemId)
        {
            var mainInventory = playerInventory.MainOpenableInventory;
            return Enumerable.Range(0, mainInventory.GetSlotSize()).
                Where(slot => mainInventory.GetItem(slot).Id == itemId).
                Sum(slot => mainInventory.GetItem(slot).Count);
        }
    }
}
