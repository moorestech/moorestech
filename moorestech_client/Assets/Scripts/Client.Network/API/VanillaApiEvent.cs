using System;
using System.Collections.Generic;
using Server.Event;
using UniRx;

namespace Client.Network.API
{
    public interface IVanillaApiEvent
    {
        IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction);
    }
    
    public class VanillaApiEvent : IVanillaApiEvent
    {
        private readonly EventResponseDispatcher _dispatcher = new();
        private readonly List<EventMessagePack> _bufferedEvents = new();
        private bool _isDispatchStarted;

        public VanillaApiEvent(PacketExchangeManager packetExchangeManager)
        {
            // push配信されたイベントを購読する（ポーリング廃止）
            // Subscribe to pushed events; polling is removed
            packetExchangeManager.OnEventPacket.Subscribe(OnEventPacketReceived);

            #region Internal

            void OnEventPacketReceived(EventMessagePack eventMessagePack)
            {
                // ハンドラ購読完了前は全イベントをバッファする（初回同期の取りこぼし防止）
                // Buffer everything until InitializeDispatch so no event is lost before handlers subscribe
                if (!_isDispatchStarted)
                {
                    _bufferedEvents.Add(eventMessagePack);
                    return;
                }

                Dispatch(eventMessagePack);
            }

            #endregion
        }

        // 全ハンドラの購読登録完了後に1回だけ呼ぶ。バッファを到着順にreplayして即時配信へ移行する
        // Call once after all handlers subscribed; replays the buffer in arrival order then goes live
        public void InitializeDispatch()
        {
            _isDispatchStarted = true;
            foreach (var buffered in _bufferedEvents) Dispatch(buffered);
            _bufferedEvents.Clear();
        }

        private void Dispatch(EventMessagePack eventMessagePack)
        {
            _dispatcher.Dispatch(eventMessagePack.Tag, eventMessagePack.Payload);
        }

        public IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction)
        {
            return _dispatcher.Subscribe(tag, responseAction);
        }
    }
}
