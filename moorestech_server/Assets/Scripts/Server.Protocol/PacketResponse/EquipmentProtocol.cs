using System;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     装備インベントリを操作するプロトコル
    ///     操作結果は選択イベントで返し、直接応答しない。
    ///     Results arrive via the selected-index event; no direct response.
    /// </summary>
    public class EquipmentProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:equipment";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public EquipmentProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<EquipmentProtocolMessagePack>(payload);
            var equipmentInventory = _playerInventoryDataStore.GetInventoryData(request.PlayerId).EquipmentInventory;

            // 範囲外の指定は装備インベントリ側でクランプされる
            // Out-of-range indexes are clamped by the equipment inventory itself
            switch (request.Operation)
            {
                case EquipmentOperation.SetSelectedIndex:
                    equipmentInventory.SetSelectedEquipmentIndex(request.SelectedIndex);
                    break;
            }

            return null;
        }

        #region MessagePack

        [MessagePackObject]
        public class EquipmentProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public EquipmentOperation Operation { get; set; }
            [Key(4)] public int SelectedIndex { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public EquipmentProtocolMessagePack() { Tag = ProtocolTag; }

            // Operationごとに必要フィールドのみ設定する
            // Private constructor; static factories set only the fields each Operation needs
            private EquipmentProtocolMessagePack(int playerId, EquipmentOperation operation, int selectedIndex)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Operation = operation;
                SelectedIndex = selectedIndex;
            }

            public static EquipmentProtocolMessagePack CreateSetSelectedIndexRequest(int playerId, int selectedIndex)
            {
                return new EquipmentProtocolMessagePack(playerId, EquipmentOperation.SetSelectedIndex, selectedIndex);
            }
        }

        public enum EquipmentOperation
        {
            SetSelectedIndex = 0,
        }

        #endregion
    }
}
