using System;
using System.Collections.Generic;
using Server.Event;
using UniRx;
using UnityEngine;

namespace Client.Network.API
{
    public interface IVanillaApiEvent
    {
        IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction);
    }
    
    public class VanillaApiEvent : IVanillaApiEvent
    {
        private readonly Dictionary<string, Subject<byte[]>> _eventResponseSubjects = new();
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
            if (!_eventResponseSubjects.TryGetValue(eventMessagePack.Tag, out var subject)) return;
            subject.OnNext(eventMessagePack.Payload);
        }

        public IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction)
        {
            if (!_eventResponseSubjects.TryGetValue(tag, out var subject))
            {
                subject = new Subject<byte[]>();
                _eventResponseSubjects.Add(tag, subject);
            }

            return subject.Subscribe(payload => InvokeIsolated(tag, responseAction, payload));
        }

        // payloadはサーバー由来の外部入力。復号や処理の例外を購読者ごとに隔離しないと、同じタグの後続購読者へ配信が届かなくなる
        // The payload is external input from the server; without per-subscriber isolation one decode or handler failure stops delivery to the rest of that tag's subscribers
        // 購読順は登録順で決まるため、隔離が無いと「先に本来の購読者が例外にする」かどうかが登録順の偶然に左右される
        // Subscription order follows registration order, so without isolation whether "the real subscriber throws first" is left to the accident of registration order
        private static void InvokeIsolated(string tag, Action<byte[]> responseAction, byte[] payload)
        {
            try
            {
                responseAction(payload);
            }
            catch (Exception e)
            {
                Debug.LogError($"イベントの購読者が例外を投げました（他の購読者への配信は続行します） tag:{tag} {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
