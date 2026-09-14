namespace Client.PlaytestReceiver.Http
{
    // HTTPの結末。到達できたかどうかと、到達できたときの状態コードを分けて持つ
    // The outcome of one HTTP call; reachability and the status code are kept apart
    public sealed class PlaytestApiResult
    {
        public int StatusCode;
        public string Body;
        public string TransportError;

        public bool IsTransportFailure => TransportError != null;

        // 成功は2xxだけ。到達できていないものを状態コード0の成功と取り違えないよう到達判定を先に見る
        // Only a 2xx succeeds; reachability is checked first so an unreached call cannot pass as a status-0 success
        public bool IsSuccess => !IsTransportFailure && 200 <= StatusCode && StatusCode < 300;
    }
}
