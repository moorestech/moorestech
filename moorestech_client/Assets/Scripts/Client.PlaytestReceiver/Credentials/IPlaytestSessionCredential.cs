using System.Threading;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Credentials
{
    public interface IPlaytestSessionCredential
    {
        UniTask<PlaytestCredentialPost> PostSessionAsync(IPlaytestReceiverApi api, CancellationToken token);
    }
}
