using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using MessagePack;

namespace Game.Block.Blocks.ConnectionLine
{
    /// <summary>
    /// 接続線（電線・歯車チェーン）1本ぶんの同期データ。接続先と引いた種類を運ぶ
    /// Sync data for one connection line (wire or gear chain): the partner and the tool it was drawn with
    /// </summary>
    [MessagePackObject]
    public class ConnectionLinePartnerMessagePack
    {
        [Key(0)] public int PartnerBlockInstanceId;
        [Key(1)] public Guid ConnectToolGuid;

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ConnectionLinePartnerMessagePack()
        {
        }

        public ConnectionLinePartnerMessagePack(int partnerBlockInstanceId, Guid connectToolGuid)
        {
            PartnerBlockInstanceId = partnerBlockInstanceId;
            ConnectToolGuid = connectToolGuid;
        }

        // 電線・チェーンの接続台帳から同期配列を作る（相手の型は問わない）
        // Build the sync array from a wire or chain connection ledger (the peer type does not matter)
        public static ConnectionLinePartnerMessagePack[] CreateArray<TPeer>(IEnumerable<KeyValuePair<BlockInstanceId, (TPeer Peer, ConnectionLineRecord Record)>> connections)
        {
            return connections.Select(c => new ConnectionLinePartnerMessagePack(c.Key.AsPrimitive(), c.Value.Record.ConnectToolGuid)).ToArray();
        }
    }
}
