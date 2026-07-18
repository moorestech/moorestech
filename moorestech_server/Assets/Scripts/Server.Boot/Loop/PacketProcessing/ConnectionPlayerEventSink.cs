using MessagePack;
using Server.Event;
using Server.Protocol;
using Server.Util;

namespace Server.Boot.Loop.PacketProcessing
{
    // 接続のSendQueueProcessorへenvelope+長さヘッダ付きでイベントを積むsink
    // Sink that wraps a connection's SendQueueProcessor with envelope and length header
    public class ConnectionPlayerEventSink : IPlayerEventSink
    {
        private readonly SendQueueProcessor _sendQueueProcessor;

        public ConnectionPlayerEventSink(SendQueueProcessor sendQueueProcessor)
        {
            _sendQueueProcessor = sendQueueProcessor;
        }

        public void EnqueueEvent(EventMessagePack eventMessagePack)
        {
            var body = MessagePackSerializer.Serialize(new EventStreamMessagePack(eventMessagePack));

            // 応答と同じ長さヘッダ形式で積み、FIFOで応答との順序を保つ
            // Use the same length header as responses so FIFO order holds across the stream
            var header = ToByteArray.Convert(body.Length);
            var sendData = new byte[header.Length + body.Length];
            header.CopyTo(sendData, 0);
            body.CopyTo(sendData, header.Length);
            _sendQueueProcessor.EnqueueSendData(sendData);
        }
    }
}
