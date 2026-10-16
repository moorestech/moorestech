using System.Threading;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Credentials
{
    public sealed class SteamTicketSessionCredential : IPlaytestSessionCredential
    {
        private readonly IPlaytestSteamTicketProvider _ticketProvider;
        public SteamTicketSessionCredential(IPlaytestSteamTicketProvider ticketProvider) { _ticketProvider = ticketProvider; }

        public bool AcceptsSubject(string subject)
        {
            return ulong.TryParse(subject, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0;
        }

        public async UniTask<PlaytestCredentialPost> PostSessionAsync(IPlaytestReceiverApi api, CancellationToken token)
        {
            var ticketHex = await _ticketProvider.RequestWebApiTicketHexAsync(token);
            if (ticketHex == null)
            {
                Debug.LogWarning("[PlaytestReceiver] no web api ticket; session was not requested");
                return new PlaytestCredentialNotSent(PlaytestSessionResult.Failed(PlaytestSessionOutcome.TicketUnavailable, "no web api ticket"));
            }

            // 検証完了・打ち切りのどちらでもチケットを解放する
            // Release the ticket after verification or cancellation
            try
            {
                return new PlaytestCredentialSent(await api.PostSessionAsync(ticketHex, token));
            }
            finally
            {
                _ticketProvider.ReleaseWebApiTicket();
            }
        }
    }
}
