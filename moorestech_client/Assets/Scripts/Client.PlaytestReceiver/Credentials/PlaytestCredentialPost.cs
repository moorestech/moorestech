using Client.PlaytestReceiver.Http;

namespace Client.PlaytestReceiver.Credentials
{
    public abstract class PlaytestCredentialPost { internal PlaytestCredentialPost() { } }
    public sealed class PlaytestCredentialSent : PlaytestCredentialPost
    {
        public readonly PlaytestApiResult Response;
        public PlaytestCredentialSent(PlaytestApiResult response) { Response = response; }
    }
    public sealed class PlaytestCredentialNotSent : PlaytestCredentialPost
    {
        public readonly PlaytestSessionFailed Failure;
        public PlaytestCredentialNotSent(PlaytestSessionFailed failure) { Failure = failure; }
    }
}
