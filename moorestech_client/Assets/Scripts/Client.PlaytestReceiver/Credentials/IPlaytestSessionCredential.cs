using System.Threading;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Credentials
{
    public interface IPlaytestSessionCredential
    {
        bool AcceptsSubject(string subject);
        UniTask<PlaytestCredentialPost> PostSessionAsync(IPlaytestReceiverApi api, CancellationToken token);
    }
}
