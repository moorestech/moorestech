using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Client.Network.API
{
    // タグごとの購読口と配信を持つ。購読者1人の例外を隔離する責務をここへ切り出し、通信の配線なしで単体で立てられるようにする
    // Owns the per-tag subscription port and the delivery; the per-subscriber isolation lives here so it stands up alone without any networking wiring
    public sealed class EventResponseDispatcher
    {
        private readonly Dictionary<string, Subject<byte[]>> _eventResponseSubjects = new();

        public IDisposable Subscribe(string tag, Action<byte[]> responseAction)
        {
            if (!_eventResponseSubjects.TryGetValue(tag, out var subject))
            {
                subject = new Subject<byte[]>();
                _eventResponseSubjects.Add(tag, subject);
            }

            return subject.Subscribe(payload => InvokeIsolated(tag, responseAction, payload));
        }

        public void Dispatch(string tag, byte[] payload)
        {
            if (!_eventResponseSubjects.TryGetValue(tag, out var subject)) return;
            subject.OnNext(payload);
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
