using System;
using MessagePack;
using Server.Event.EventReceive;

namespace Client.Game.InGame.Playtest.Progress
{
    // サーバーイベントのpayloadを進行記録の1イベントへ写す純関数。購読を張らずに写し替えだけを検証できる
    // Pure functions mapping a server event payload into one progress event, so the mapping is verifiable without any subscription
    // 復号は前例どおり catch しない。壊れたパケットは同じフレームで先に本来の購読者（ChallengeManager 等）が例外にする（ADR 0060 裁定B）
    // Decoding is not caught, following precedent: a broken packet throws first in its real subscriber (ChallengeManager and friends) in the same frame (ADR 0060 adjudication B)
    internal static class ProgressServerEvents
    {
        public static ProgressEventEntry ResearchCompleted(byte[] payload, DateTime utc, ulong tick)
        {
            var message = MessagePackSerializer.Deserialize<ResearchCompleteEventPacket.ResearchCompleteEventMessagePack>(payload);
            return ProgressEvents.ResearchCompleted(utc, tick, message.ResearchGuidStr);
        }

        public static ProgressEventEntry ChallengeCompleted(byte[] payload, DateTime utc, ulong tick)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(payload);
            return ProgressEvents.ChallengeCompleted(utc, tick, message.CompletedChallengeGuidStr);
        }
    }
}
