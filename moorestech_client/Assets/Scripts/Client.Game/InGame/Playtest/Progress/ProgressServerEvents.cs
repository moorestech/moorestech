using System;
using Client.Game.InGame.Playtest.Progress.Record.Events;
using MessagePack;
using Server.Event.EventReceive;

namespace Client.Game.InGame.Playtest.Progress
{
    // サーバーイベントのpayloadを進行記録の1イベントへ写す純関数。購読を張らずに写し替えだけを検証できる
    // Pure functions mapping a server event payload into one progress event, so the mapping is verifiable without any subscription
    // 復号は前例どおり catch しない（ADR 0060 裁定B）。壊れたパケットの例外は VanillaApiEvent が購読者ごとに隔離するので、他の購読者の配信は止まらない
    // Decoding is not caught, following precedent (ADR 0060 adjudication B); VanillaApiEvent isolates each subscriber, so a broken packet never stops delivery to the others
    internal static class ProgressServerEvents
    {
        public static ResearchCompletedEvent ResearchCompleted(byte[] payload, DateTime utc, ulong tick)
        {
            var message = MessagePackSerializer.Deserialize<ResearchCompleteEventPacket.ResearchCompleteEventMessagePack>(payload);
            return new ResearchCompletedEvent(utc, tick, message.ResearchGuidStr);
        }

        public static ChallengeCompletedEvent ChallengeCompleted(byte[] payload, DateTime utc, ulong tick)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(payload);
            return new ChallengeCompletedEvent(utc, tick, message.CompletedChallengeGuidStr);
        }

        public static CraftCompletedEvent CraftCompleted(byte[] payload, DateTime utc, ulong tick)
        {
            var message = MessagePackSerializer.Deserialize<CraftCompletedEventPacket.CraftCompletedEventMessagePack>(payload);
            return new CraftCompletedEvent(utc, tick, message.CraftRecipeGuidStr);
        }
    }
}
