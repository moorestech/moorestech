using System;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     装備インベントリの選択スロットを変更するプロトコル
    ///     操作結果は選択イベントで返し、直接応答しない。
    ///     Results arrive via the selected-index event; no direct response.
    /// </summary>
    public class SetSelectedEquipmentIndexProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:setSelectedEquipmentIndex";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public SetSelectedEquipmentIndexProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<SetSelectedEquipmentIndexMessagePack>(payload);
            var equipmentInventory = _playerInventoryDataStore.GetInventoryData(request.PlayerId).EquipmentInventory;

            // 範囲外の指定は装備インベントリ側でクランプされる
            // Out-of-range indexes are clamped by the equipment inventory itself
            equipmentInventory.SetSelectedEquipmentIndex(request.SelectedIndex);

            return null;
        }

        #region MessagePack

        [MessagePackObject]
        public class SetSelectedEquipmentIndexMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public int SelectedIndex { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SetSelectedEquipmentIndexMessagePack() { Tag = ProtocolTag; }

            public SetSelectedEquipmentIndexMessagePack(int playerId, int selectedIndex)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                SelectedIndex = selectedIndex;
            }
        }

        #endregion
    }
}
