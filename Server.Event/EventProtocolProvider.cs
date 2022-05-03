using System.Collections.Generic;

namespace Server.Event
{
    /// <summary>
    /// サーバー内で起こったイベントの中で、各プレイヤーに送る必要があるイベントを管理します。
    /// 送る必要のある各イベントはEventReceiveフォルダの中に入っています
    /// </summary>
    public class EventProtocolProvider
    {
        private Dictionary<int, List<List<byte>>> _events = new();

        public void AddEvent(int playerId, List<byte> eventByteArray)
        {
            if (_events.ContainsKey(playerId))
            {
                _events[playerId].Add(eventByteArray);
            }
            else
            {
                _events.Add(playerId, new List<List<byte>>() {eventByteArray});
            }
        }

        public void AddBroadcastEvent(List<byte> eventByteArray)
        {
            foreach (var key in _events.Keys)
            {
                _events[key].Add(eventByteArray);
            }
        }

        public List<List<byte>> GetEventBytesList(int playerId)
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