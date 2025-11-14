using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Game.Common.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// 全レール接続情報を取得するプロトコル
    /// Protocol to get all rail connection information
    /// </summary>
    public class GetRailConnectionsProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getRailConnections";
        
        public GetRailConnectionsProtocol(ServiceProvider serviceProvider)
        {
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var allConnections = RailGraphDatastore.GetAllRailConnections();
            
            return new GetRailConnectionsResponse(allConnections);
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class GetRailConnectionsRequest : ProtocolMessagePackBase
        {
            public GetRailConnectionsRequest() { Tag = ProtocolTag; }
        }
        
        [MessagePackObject]
        public class GetRailConnectionsResponse : ProtocolMessagePackBase
        {
            [Key(2)] public RailConnectionDataMessagePack[] Connections { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GetRailConnectionsResponse() { }
            
            public GetRailConnectionsResponse(List<RailConnectionInfo> connectionInfos)
            {
                Tag = ProtocolTag;
                
                // RailConnectionInfoからRailConnectionDataへ変換（変換処理は静的メソッド内で実行）
                // Convert RailConnectionInfo to RailConnectionData (conversion is done in static method)
                var connectionDataList = new List<RailConnectionDataMessagePack>();
                foreach (var connectionInfo in connectionInfos)
                {
                    var connectionData = RailConnectionDataMessagePack.TryCreate(connectionInfo);
                    if (connectionData == null) continue;
                    
                    connectionDataList.Add(connectionData);
                }
                
                Connections = connectionDataList.ToArray();
            }
        }
        
        #endregion
    }
}
