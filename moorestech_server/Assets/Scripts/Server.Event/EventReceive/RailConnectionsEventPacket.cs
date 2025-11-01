using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using MessagePack;
using Server.Event;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// レール接続情報の更新イベントを送信するパケット
    /// Packet for sending rail connection update events
    /// </summary>
    public class RailConnectionsEventPacket
    {
        public const string EventTag = "va:event:railConnectionsUpdate";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public RailConnectionsEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            
            // レールグラフ更新イベントをSubscribe
            // Subscribe to rail graph update events
            RailGraphDatastore.RailGraphUpdateEvent.Subscribe(OnRailGraphUpdate);
        }
        
        // レールグラフ更新イベントのハンドラ
        // Handler for rail graph update events
        private void OnRailGraphUpdate(List<RailComponentID> changedComponentIds)
        {
            // 全レール接続情報を取得
            // Get all rail connection information
            //var allConnections = RailGraphDatastore.GetAllRailConnections();
            
            var eventMessage = new RailConnectionsEventMessagePack(changedComponentIds);
            var payload = MessagePackSerializer.Serialize(eventMessage);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class RailConnectionsEventMessagePack
        {
            //[Key(0)] public RailConnectionDataMessagePack[] AllConnections { get; set; }
            [Key(0)] public RailComponentIDMessagePack[] ChangedComponentIds { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionsEventMessagePack() { }
            
            
            public RailConnectionsEventMessagePack(List<RailComponentID> changedComponentIds)
            {
                // RailConnectionInfoからRailConnectionDataへ変換（変換処理は静的メソッド内で実行）
                // Convert RailConnectionInfo to RailConnectionData (conversion is done in static method)
                // var connectionDataList = new List<RailConnectionDataMessagePack>();
                /*
                foreach (var connectionInfo in connectionInfos)
                {
                    var connectionData = RailConnectionDataMessagePack.TryCreate(connectionInfo);
                    if (connectionData == null) continue;
                    
                    connectionDataList.Add(connectionData);
                }
                */
                
                //AllConnections = connectionDataList.ToArray();
                
                // 変更されたRailComponentIDをMessagePack形式に変換
                // Convert changed RailComponentIDs to MessagePack format
                ChangedComponentIds = changedComponentIds.Select(id => new RailComponentIDMessagePack(id)).ToArray();
            }
        }
        
        #endregion
    }
}

