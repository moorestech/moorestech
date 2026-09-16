using System.Threading;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Http
{
    // トークンを付けて送る1回分の呼び出し。取り直しと再送の判断は PlaytestSession が持つ
    // One call sent with a bearer token; PlaytestSession owns the decision to refresh and resend
    internal interface IPlaytestAuthorizedCall
    {
        UniTask<PlaytestApiResult> SendAsync(string bearerToken, CancellationToken token);
    }
}
