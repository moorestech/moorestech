using System;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     接続相手1件（相手ブロックと引いた種類）
    ///     One connection partner (partner block and the tool kind used)
    /// </summary>
    public readonly struct ConnectionLinePartner
    {
        public readonly BlockInstanceId PartnerId;
        public readonly Guid ConnectToolGuid;

        private ConnectionLinePartner(BlockInstanceId partnerId, Guid connectToolGuid)
        {
            PartnerId = partnerId;
            ConnectToolGuid = connectToolGuid;
        }

        // 状態詳細の配列をクライアント表現へ写す（接続ゼロはnullで届き得る）
        // Map the state-detail array to client form (zero connections may arrive as null)
        public static ConnectionLinePartner[] FromMessagePacks(ConnectionLinePartnerMessagePack[] packs)
        {
            if (packs == null) return Array.Empty<ConnectionLinePartner>();

            var partners = new ConnectionLinePartner[packs.Length];
            for (var i = 0; i < packs.Length; i++)
            {
                partners[i] = new ConnectionLinePartner(new BlockInstanceId(packs[i].PartnerBlockInstanceId), packs[i].ConnectToolGuid);
            }
            return partners;
        }
    }
}
