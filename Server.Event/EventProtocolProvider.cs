using System.Collections.Generic;

namespace Server.Event
{
    /// <summary>
    ///     。
    ///     EventReceive
    ///     TODO ！！
    /// </summary>
    public class EventProtocolProvider
    {
        private readonly Dictionary<int, List<List<byte>>> _events = new();

        public void AddEvent(int playerId, List<byte> eventByteArray)
        {
            lock (_events)
            {
                if (_events.TryGetValue(playerId, out var eventList))
                    eventList.Add(eventByteArray);
                else
                    _events.Add(playerId, new List<List<byte>> { eventByteArray });
            }
        }

        public void AddBroadcastEvent(List<byte> eventByteArray)
        {
            lock (_events)
            {
                foreach (var key in _events.Keys) _events[key].Add(eventByteArray);
            }
        }

        public List<List<byte>> GetEventBytesList(int playerId)
        {
            lock (_events)
            {
                if (_events.ContainsKey(playerId))
                {
                    var data = _events[playerId].Copy();
                    _events[playerId].Clear();
                    return data;
                }

                //Dictionary
                _events.Add(playerId, new List<List<byte>>());
                return _events[playerId];
            }
        }
    }
}