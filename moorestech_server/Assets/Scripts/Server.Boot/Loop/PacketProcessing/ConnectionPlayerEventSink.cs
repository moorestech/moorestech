using MessagePack;
using Server.Event;
using Server.Protocol;

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

            // 応答と同じ送信キューへ積み、FIFOで応答との順序を保つ
            // Enqueue into the same send queue as responses so FIFO order holds across the stream
            _sendQueueProcessor.EnqueueMessage(body);
        }
    }
}
