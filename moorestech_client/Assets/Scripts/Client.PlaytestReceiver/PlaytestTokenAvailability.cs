namespace Client.PlaytestReceiver
{
    // 手元のトークンが使えるかの結末。キャッシュ命中はSteamIDを伴わないので、認証の結末とは別の型で運ぶ
    // Whether the held token is usable; a cache hit carries no SteamID, so it travels in a type apart from the authentication outcome
    internal sealed class PlaytestTokenAvailability
    {
        public static readonly PlaytestTokenAvailability Usable = new(true, PlaytestSessionOutcome.Authenticated, "");

        public bool IsUsable { get; }

        // 使えない理由。IsUsableがfalseのときだけ読まれる
        // Why the token is unusable; read only while IsUsable is false
        public PlaytestSessionOutcome FailureOutcome { get; }
        public string Detail { get; }

        private PlaytestTokenAvailability(bool isUsable, PlaytestSessionOutcome failureOutcome, string detail)
        {
            IsUsable = isUsable;
            FailureOutcome = failureOutcome;
            Detail = detail;
        }

        public static PlaytestTokenAvailability Unavailable(PlaytestSessionResult failed)
        {
            return new PlaytestTokenAvailability(false, failed.Outcome, failed.Detail);
        }
    }
}
