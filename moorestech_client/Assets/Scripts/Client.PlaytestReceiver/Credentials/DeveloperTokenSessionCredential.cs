using System.Threading;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Credentials
{
    public sealed class DeveloperTokenSessionCredential : IPlaytestSessionCredential
    {
        private readonly string _developerToken;
        public DeveloperTokenSessionCredential(string developerToken) { _developerToken = developerToken; }

        public bool AcceptsSubject(string subject) { return subject.StartsWith("dev-", System.StringComparison.Ordinal); }

        public async UniTask<PlaytestCredentialPost> PostSessionAsync(IPlaytestReceiverApi api, CancellationToken token)
        {
            return new PlaytestCredentialSent(await api.PostDeveloperSessionAsync(_developerToken, token));
        }
    }
}
