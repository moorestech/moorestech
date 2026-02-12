using System;
using System.Collections.Generic;
using MessagePack;
using Server.Event.EventReceive;

namespace Server.Event
{
    /// <summary>
    ///     サーバー内で起こったイベントの中で、各プレイヤーに送る必要があるイベントを管理します。
    ///     送る必要のある各イベントはEventReceiveフォルダの中に入っています
    ///     TODO ここのロックは一時的なものなので今後はちゃんとゲーム全体としてセマフォをしっかりやる！！
    /// </summary>
    public class EventProtocolProvider
    {
        private readonly Dictionary<int, List<EventMessagePack>> _events = new();
        
        public void AddEvent(int playerId, string tag, byte[] payload)
        {
            lock (_events)
            {
                var eventMessagePack = new EventMessagePack(tag, payload);
                
                if (_events.TryGetValue(playerId, out var eventList))
                    eventList.Add(eventMessagePack);
                else
                    _events.Add(playerId, new List<EventMessagePack> { eventMessagePack });
            }
        }
        
        public void AddBroadcastEvent(string tag, byte[] payload)
        {
            lock (_events)
            {
                var eventMessagePack = new EventMessagePack(tag, payload);
                
                foreach (var key in _events.Keys) _events[key].Add(eventMessagePack);
            }
        }
        
        public List<EventMessagePack> GetEventBytesList(int playerId)
        {
            lock (_events)
            {
                if (_events.ContainsKey(playerId))
                {
                    var events = _events[playerId];
                    var data = new List<EventMessagePack>();
                    data.AddRange(events);
                    
                    _events[playerId].Clear();
                    return data;
                }
                
                //ブロードキャストイベントの時に使うので、何かしらリクエストがあった際はDictionaryにキーを追加しておく
                _events.Add(playerId, new List<EventMessagePack>());
                
                return new List<EventMessagePack>();
            }
        }
    }
    
    
    [MessagePackObject]
    public class EventMessagePack
    {
        public EventMessagePack(string tag, byte[] payload)
        {
            Tag = tag;
            Payload = payload;
        }
        
        [Obsolete("This constructor is for deserialization. Do not use directly.")]
        public EventMessagePack()
        {
        }
        
        [Key(0)] public string Tag { get; set; }
        
        [Key(1)] public byte[] Payload { get; set; }
        
        [Key(2)] public Dictionary<string,BlockStateMessagePack> MessagePacks { get; set; }
    }
}