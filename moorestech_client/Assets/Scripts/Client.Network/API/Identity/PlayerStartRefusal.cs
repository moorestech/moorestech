using Mooresmaster.Localization.Generated;

namespace Client.Network.API.Identity
{
    // 通常の開始拒否は表示キーと開発者向け理由を結果として運ぶ
    // Expected start refusals carry both a display key and a developer-facing reason
    public readonly struct PlayerStartRefusal
    {
        public readonly LocalizationKey Key;
        public readonly string LogReason;

        public PlayerStartRefusal(LocalizationKey key, string logReason)
        {
            Key = key;
            LogReason = logReason;
        }
    }

    public readonly struct InitialHandshakeAttempt
    {
        public readonly InitialHandshakeResponse Response;
        public readonly PlayerStartRefusal? Refusal;

        public InitialHandshakeAttempt(InitialHandshakeResponse response, PlayerStartRefusal? refusal)
        {
            Response = response;
            Refusal = refusal;
        }
    }
}
