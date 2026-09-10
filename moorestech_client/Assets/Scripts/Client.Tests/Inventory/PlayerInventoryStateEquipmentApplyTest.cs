using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Network.API;
using Client.Tests.UIState;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.PlayerInventoryResponseProtocol;

namespace Client.Tests.Inventory
{
    /// <summary>
    ///     初期インベントリ応答が装備モデルへ実際に適用されるかを、PlayerInventoryStateの結線ごと検証する。
    ///     装備は専用の取得プロトコルを持たずこの応答に相乗りするため、結線が1行落ちるだけで初期装備が無言で失われる。
    ///     Verifies that the initial inventory response actually reaches the equipment model through PlayerInventoryState's wiring.
    ///     Equipment has no dedicated fetch protocol and rides on this response, so dropping one wiring line silently loses it.
    /// </summary>
    public class PlayerInventoryStateEquipmentApplyTest : UIStateTestFixtureBase
    {
        // 装備モデルの初期値は先頭(0)なので、それと区別できるスロットを選んでおく
        // The equipment model starts at the first slot (0), so pick a slot that is distinguishable from it
        private const int SelectedSlot = 1;
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 初期インベントリ応答の装備と選択インデックスが装備モデルへ適用される()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var equipment = new LocalPlayerEquipment();

            // 装備スロット0に工具、選択はスロット1という初期値と異なる状態を応答に載せる
            // The response carries a tool in slot 0 and selects slot 1, both differing from the model's initial state
            var handshake = CreateToolHandshakeResponse(toolItemId);
            CreatePlayerInventoryState(equipment, handshake);

            Assert.AreEqual(toolItemId, equipment.Slots[0].Id);
            Assert.AreEqual(SelectedSlot, equipment.SelectedIndex);

            #region Internal

            InitialHandshakeResponse CreateToolHandshakeResponse(ItemId itemId)
            {
                var equipmentSlots = new[] { new ItemMessagePack(itemId, 1), new ItemMessagePack(ItemMaster.EmptyItemId, 0) };
                return CreateHandshakeResponse(new PlayerInventoryResponse(new PlayerInventoryResponseProtocolMessagePack(
                    0, Array.Empty<ItemMessagePack>(), new ItemMessagePack(ItemMaster.EmptyItemId, 0), equipmentSlots, SelectedSlot)));
            }

            #endregion
        }
    }
}
