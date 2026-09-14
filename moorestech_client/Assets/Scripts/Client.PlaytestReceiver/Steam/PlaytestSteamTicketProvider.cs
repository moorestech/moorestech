using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace Client.PlaytestReceiver.Steam
{
    // チケット取得の面。テストはここをフェイクに差し替えてSteamに触れない
    // The ticket-acquisition face; tests fake it so they never touch Steam
    public interface IPlaytestSteamTicketProvider
    {
        bool IsSteamRunning();
        UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token);
    }

    // Web API用の認証チケットを取る。SteamManagerはAssembly-CSharp側にあり参照できないのでネイティブへ直接聞く
    // Obtains the Web API auth ticket; SteamManager lives in Assembly-CSharp and is unreachable, so we ask Steam directly
    public sealed class PlaytestSteamTicketProvider : IPlaytestSteamTicketProvider
    {
        private UniTaskCompletionSource<string> _pending;
        private Callback<GetTicketForWebApiResponse_t> _callback;

        public bool IsSteamRunning()
        {
            // ネイティブ呼び出しはdllが無い環境で例外になる境界。存在しなければ「Steamは動いていない」に畳む
            // The native call throws where the dll is absent; that boundary collapses to "Steam is not running"
            try
            {
                return SteamAPI.IsSteamRunning();
            }
            catch (Exception exception)
            {
                Debug.Log($"[PlaytestReceiver] Steam is unavailable: {exception.GetBaseException().Message}");
                return false;
            }
        }

        public async UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token)
        {
            // 待ち受けは1本だけ。重ねると前の待ちが宙に浮き、どちらのチケットが返ったのか分からなくなる
            // Only one wait may be in flight; overlapping ones orphan the previous await and blur which ticket arrived
            if (_callback != null)
            {
                Debug.LogWarning("[PlaytestReceiver] a web api ticket request is already in flight");
                return null;
            }

            _pending = new UniTaskCompletionSource<string>();
            if (!TryRequestTicket())
            {
                DisposeCallback();
                return null;
            }

            // 打ち切りは待ち受け自体を終わらせる。TimeoutWithoutExceptionは打ち切りも時間切れとして返すため後で選り分ける
            // Cancellation ends the wait; TimeoutWithoutException reports it as a timeout too, so it is sorted out afterwards
            using (token.Register(OnCancelled))
            {
                var (timedOut, ticketHex) = await _pending.Task.TimeoutWithoutException(TimeSpan.FromSeconds(PlaytestReceiverConfig.TicketTimeoutSeconds), DelayType.Realtime);
                DisposeCallback();

                token.ThrowIfCancellationRequested();
                if (!timedOut) return ticketHex;

                Debug.LogWarning($"[PlaytestReceiver] Steam did not answer GetAuthTicketForWebApi within {PlaytestReceiverConfig.TicketTimeoutSeconds}s");
                return null;
            }
        }

        private bool TryRequestTicket()
        {
            // Steam未初期化・ネイティブ不在ならSteamworksが例外を投げる境界。ここで畳んで「チケット無し」にする
            // Steamworks throws when uninitialized or absent; that boundary is folded here into "no ticket"
            try
            {
                _callback = Callback<GetTicketForWebApiResponse_t>.Create(OnTicketReceived);
                SteamUser.GetAuthTicketForWebApi(PlaytestReceiverConfig.SteamIdentity);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlaytestReceiver] GetAuthTicketForWebApi failed: {exception.GetBaseException().Message}");
                return false;
            }
        }

        private void OnCancelled()
        {
            _pending.TrySetResult(null);
        }

        private void OnTicketReceived(GetTicketForWebApiResponse_t response)
        {
            if (response.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[PlaytestReceiver] web api ticket result was {response.m_eResult}");
                _pending.TrySetResult(null);
                return;
            }
            _pending.TrySetResult(ToHex(response.m_rgubTicket, response.m_cubTicket));
        }

        private void DisposeCallback()
        {
            _callback?.Dispose();
            _callback = null;
        }

        private static string ToHex(byte[] ticket, int length)
        {
            var builder = new StringBuilder(length * 2);
            for (var index = 0; index < length; index++) builder.Append(ticket[index].ToString("x2"));
            return builder.ToString();
        }
    }
}
