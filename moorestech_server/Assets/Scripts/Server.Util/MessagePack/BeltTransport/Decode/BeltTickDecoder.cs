using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    public static class BeltTickDecoder
    {
        public static BeltTickDecodeResult Decode(byte[] payload)
        {
            if (payload == null) return new(null, "Packet bytes are missing.");
            BeltTickMessagePack message;
            // 外部ネットワークbytesの復号だけを例外隔離する。
            // Isolate only decoding bytes received from the external network.
            try { message = MessagePackSerializer.Deserialize<BeltTickMessagePack>(payload); }
            catch (MessagePackSerializationException error) { return new(null, error.Message); }
            string reason = BeltTickStructureValidation.Validate(message);
            return reason == null ? new(message.ToCore(), null) : new(null, reason);
        }
    }
}
