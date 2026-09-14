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
    }
}
