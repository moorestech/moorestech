using System.Collections.Generic;
using MessagePack;

namespace Server.Event.EventReceive
{
    public static class EventPacketUtil
    {
        public static TBlockState GetStateDetail<TBlockState>(this Dictionary<string,byte[]> stateDetail, string stateKey)
        {
            if (!stateDetail.TryGetValue(stateKey, out var bytes))
            {
                return default;
            }
            
            return MessagePackSerializer.Deserialize<TBlockState>(bytes);
        }

    }
}