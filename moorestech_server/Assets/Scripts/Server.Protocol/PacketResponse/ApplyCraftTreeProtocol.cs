using System.Collections.Generic;
using Game.Context;
using Game.CraftTree.Manager;
using Game.CraftTree.Network;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// クライアントからサーバーへのクラフトツリー適用プロトコル
    /// </summary>
    public sealed class ApplyCraftTreeProtocol : IPacketResponse
    {
        /// <summary>
        /// プロトコル識別タグ
        /// </summary>
        public const string ProtocolTag = "va:applyCraftTree";
        
        private readonly CraftTreeManager _craftTreeManager;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="craftTreeManager">クラフトツリーマネージャー</param>
        public ApplyCraftTreeProtocol(CraftTreeManager craftTreeManager)
        {
            _craftTreeManager = craftTreeManager;
        }
        
        /// <summary>
        /// リクエストへの応答を生成
        /// </summary>
        /// <param name="payload">リクエストペイロード</param>
        /// <returns>応答メッセージ</returns>
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            // リクエストをデシリアライズ
            var request = MessagePackSerializer.Deserialize<ApplyCraftTreeRequestMessagePack>(payload.ToArray());
            
            // クライアントから受け取ったツリーデータをサーバー側に適用
            _craftTreeManager.ApplyCraftTreeFromClient(new PlayerId(request.PlayerId), request.TreeData);
            
            // 空の応答を返す
            return new ApplyCraftTreeResponseMessagePack();
        }
    }
    
    /// <summary>
    /// クラフトツリー適用リクエストのメッセージパック
    /// </summary>
    [MessagePackObject]
    public class ApplyCraftTreeRequestMessagePack : ProtocolMessagePackBase
    {
        /// <summary>
        /// プレイヤーID
        /// </summary>
        [Key(0)]
        public string PlayerId { get; set; }
        
        /// <summary>
        /// ツリーデータ
        /// </summary>
        [Key(1)]
        public CraftTreeData TreeData { get; set; }
        
        /// <summary>
        /// デフォルトコンストラクタ
        /// </summary>
        public ApplyCraftTreeRequestMessagePack()
        {
        }
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <param name="treeData">ツリーデータ</param>
        public ApplyCraftTreeRequestMessagePack(string playerId, CraftTreeData treeData)
        {
            PlayerId = playerId;
            TreeData = treeData;
        }
    }
    
    /// <summary>
    /// クラフトツリー適用応答のメッセージパック
    /// </summary>
    [MessagePackObject]
    public class ApplyCraftTreeResponseMessagePack : ProtocolMessagePackBase
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ApplyCraftTreeResponseMessagePack()
        {
        }
    }
}