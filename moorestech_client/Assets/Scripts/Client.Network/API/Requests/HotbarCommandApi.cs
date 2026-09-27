using System;
using Server.Protocol.PacketResponse;

namespace Client.Network.API
{
    internal sealed class HotbarCommandApi
    {
        private readonly PacketSender _packetSender;

        public HotbarCommandApi(PacketSender packetSender)
        {
            _packetSender = packetSender;
        }

        /// <summary>
        /// ホットバーの枠へ設置対象を割り当てる（結果はホットバー更新イベントで返る）
        /// Assign a placement target to a hotbar slot; the result comes back through the hotbar update event
        /// </summary>
        public void AssignHotbar(int slot, Guid targetId)
        {
            var request = HotbarProtocol.HotbarProtocolMessagePack.CreateAssignRequest(slot, targetId);
            _packetSender.Send(request);
        }

        /// <summary>
        /// ホットバーの枠を空にする
        /// Clear a hotbar slot
        /// </summary>
        public void ClearHotbar(int slot)
        {
            var request = HotbarProtocol.HotbarProtocolMessagePack.CreateClearRequest(slot);
            _packetSender.Send(request);
        }

        /// <summary>
        /// ホットバーの2枠を入れ替える
        /// Swap two hotbar slots
        /// </summary>
        public void SwapHotbar(int slotA, int slotB)
        {
            var request = HotbarProtocol.HotbarProtocolMessagePack.CreateSwapRequest(slotA, slotB);
            _packetSender.Send(request);
        }
    }
}
