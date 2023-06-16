using System;
using System.Collections.Generic;
using Server.Protocol;
using Server.Protocol.Base;

namespace Server.Event
{
    /// <summary>
    /// サーバー内で起こったイベントの中で、各プレイヤーに送る必要があるイベントを管理します。
    /// 送る必要のある各イベントはEventReceiveフォルダの中に入っています
    /// </summary>
    public class EventProtocolProvider
    {
        private readonly Dictionary<int, List<ToClientProtocolMessagePackBase>> _events = new();

        public void AddEvent(int playerId, ToClientProtocolMessagePackBase eventByteArray)
        {
            if (_events.ContainsKey(playerId))
            {
                _events[playerId].Add(eventByteArray);
            }
            else
            {
                _events.Add(playerId, new List<ToClientProtocolMessagePackBase>() {eventByteArray});
            }
        }

        public void AddBroadcastEvent(ToClientProtocolMessagePackBase eventByteArray)
        {
            foreach (var key in _events.Keys)
            {
                _events[key].Add(eventByteArray);
            }
        }

        public List<ToClientProtocolMessagePackBase> GetEventBytesList(int playerId)
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
                _events.Add(playerId, new List<List<byte>>());
                return _events[playerId];
            }
        }
    }
}