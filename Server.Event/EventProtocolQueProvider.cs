using System;
using System.Collections.Generic;
using Core.Util;

namespace Server.PacketHandle.PacketResponse.Event
{
    /// <summary>
    /// 各プレイヤーにパケットとして送る必要があるイベントのキューを管理します
    /// イベントキューは初回接続時に自身のIDを登録する必要ががあります。
    /// しかし、イベント要求パケットは常に送られてくるため、登録プロトコルを作る必要はなく、最初にイベント要求パケットが送られた時点で登録すれば良いです
    /// そのため、イベントプロトコルのテストは初回にID登録作業が必要となります。
    /// </summary>
    public class EventProtocolQueProvider
    {
        public const short EventPacketId = 3;
        private Dictionary<int, List<byte[]>> _events = new Dictionary<int, List<byte[]>>();

        public void AddEvent(int playerId,byte[] eventByteArray)
        {
        
            if (_events.ContainsKey(playerId))
            {
                _events[playerId].Add(eventByteArray);
            }
            else
            {
                _events.Add(playerId,new List<byte[]>(){eventByteArray});
            }
            
        }
        public void AddBroadcastEvent(byte[] eventByteArray)
        {
            foreach (var key in _events.Keys)
            {
                _events[key].Add(eventByteArray);
            }
        }

        public List<byte[]> GetEventBytesList(int playerId)
        {
            if (_events.ContainsKey(playerId))
            {
                var data = _events[playerId].Copy();
                _events[playerId].Clear();
                return data;
            }
            else
            {
                //ブロードキャストイベントの時に使うので、Dictionaryにキーを追加しておく
                _events.Add(playerId,new List<byte[]>());
                return _events[playerId];
            }
        }
        private static EventProtocolQueProvider _instance;
        public static EventProtocolQueProvider Instance
        {
            get
            {
                if (_instance is null) _instance = new EventProtocolQueProvider();
                return _instance;
            }
        }
    }
}