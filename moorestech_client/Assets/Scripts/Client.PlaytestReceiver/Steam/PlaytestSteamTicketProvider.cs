using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
#if !UNITY_EDITOR_LINUX
using Steamworks;
#endif
using UnityEngine;

namespace Client.PlaytestReceiver.Steam
{
    // チケット取得の面。テストはここをフェイクに差し替えてSteamに触れない
    // The ticket-acquisition face; tests fake it so they never touch Steam
    public interface IPlaytestSteamTicketProvider
    {
        bool IsSteamRunning();
        UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token);
        void ReleaseWebApiTicket();
    }

    // Linux EditorのCIには非公開Steamworksアセットが無いため、テスト用の拒否実装へ切り替える
    // CI's Linux Editor lacks the private Steamworks asset, so switch to a rejecting test implementation
#if UNITY_EDITOR_LINUX
    public sealed class PlaytestSteamTicketProvider : IPlaytestSteamTicketProvider
    {
        public bool IsSteamRunning()
        {
            Debug.Log("[PlaytestReceiver] Steam is unavailable in the Linux Editor");
            return false;
        }

        public UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token)
        {
            Debug.LogWarning("[PlaytestReceiver] Steam ticket requests are unavailable in the Linux Editor");
            return UniTask.FromResult<string>(null);
        }

        public void ReleaseWebApiTicket()
        {
        }
    }
#else
    // Web API用の認証チケットを取る。SteamManagerはAssembly-CSharp側にあり参照できないのでネイティブへ直接聞く
    // Obtains the Web API auth ticket; SteamManager lives in Assembly-CSharp and is unreachable, so we ask Steam directly
    public sealed class PlaytestSteamTicketProvider : IPlaytestSteamTicketProvider
    {
        private UniTaskCompletionSource<string> _pending;
        private Callback<GetTicketForWebApiResponse_t> _callback;
        private HAuthTicket _issuedTicket = HAuthTicket.Invalid;

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
                ReleaseIssuedTicket();
                return null;
            }

            // 打ち切りは待ち受け自体を終わらせる。TimeoutWithoutExceptionは打ち切りも時間切れとして返すため後で選り分ける
            // Cancellation ends the wait; TimeoutWithoutException reports it as a timeout too, so it is sorted out afterwards
            using (token.Register(OnCancelled))
            {
                var (timedOut, ticketHex) = await _pending.Task.TimeoutWithoutException(TimeSpan.FromSeconds(PlaytestReceiverConfig.TicketTimeoutSeconds), DelayType.Realtime);
                DisposeCallback();

                // hexを呼び出し元へ返す経路だけがチケットの寿命を引き継ぐ。受け口の検証後にReleaseWebApiTicketで解放される
                // Only the exit that hands a hex to the caller keeps the ticket alive; it is released via ReleaseWebApiTicket after the receiver verifies it
                if (ticketHex == null)
                {
                    ReleaseIssuedTicket();
                }

                token.ThrowIfCancellationRequested();
                if (!timedOut) return ticketHex;

                Debug.LogWarning($"[PlaytestReceiver] Steam did not answer GetAuthTicketForWebApi within {PlaytestReceiverConfig.TicketTimeoutSeconds}s");
                return null;
            }

            #region Internal

            bool TryRequestTicket()
            {
                // Steam未初期化・ネイティブ不在ならSteamworksが例外を投げる境界。ここで畳んで「チケット無し」にする
                // Steamworks throws when uninitialized or absent; that boundary is folded here into "no ticket"
                try
                {
                    _callback = Callback<GetTicketForWebApiResponse_t>.Create(OnTicketReceived);
                    _issuedTicket = SteamUser.GetAuthTicketForWebApi(PlaytestReceiverConfig.SteamIdentity);
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[PlaytestReceiver] GetAuthTicketForWebApi failed: {exception.GetBaseException().Message}");
                    return false;
                }
            }

            void DisposeCallback()
            {
                _callback?.Dispose();
                _callback = null;
            }

            #endregion
        }

        // 受け口がチケットの検証を終えた直後に呼ぶ（成功・失敗いずれも）。検証前に取り消すと相手先の検証が失敗する
        // Call right after the receiver finishes verifying the ticket (success or failure); cancelling earlier fails their verification
        public void ReleaseWebApiTicket()
        {
            ReleaseIssuedTicket();
        }

        private void OnCancelled()
        {
            _pending.TrySetResult(null);
        }

        private void OnTicketReceived(GetTicketForWebApiResponse_t response)
        {
            _issuedTicket = response.m_hAuthTicket;
            if (response.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[PlaytestReceiver] web api ticket result was {response.m_eResult}");
                _pending.TrySetResult(null);
                return;
            }
            _pending.TrySetResult(ToHex(response.m_rgubTicket, response.m_cubTicket));
        }

        private void ReleaseIssuedTicket()
        {
            if (_issuedTicket == HAuthTicket.Invalid) return;

            // ネイティブ呼び出しはdllが無い・Steamが落ちた環境で例外になる境界。ここで畳んで戻り値を諦めない
            // The native call throws where the dll is absent or Steam has gone away; that boundary is folded here so callers never hang
            try
            {
                SteamUser.CancelAuthTicket(_issuedTicket);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlaytestReceiver] CancelAuthTicket failed: {exception.GetBaseException().Message}");
            }
            finally
            {
                _issuedTicket = HAuthTicket.Invalid;
            }
        }

        private static string ToHex(byte[] ticket, int length)
        {
            var builder = new StringBuilder(length * 2);
            for (var index = 0; index < length; index++) builder.Append(ticket[index].ToString("x2"));
            return builder.ToString();
        }
    }
#endif
}
