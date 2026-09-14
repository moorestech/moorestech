using System;
using Client.Localization;
using Client.MainMenu.PopUp;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Gate;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.MainMenu.Playtest
{
    // タイトルでの表示専用オブザーバ。開始の可否は決めず、照合を回して理由を出すだけ
    // A display-only observer on the title screen; it never decides whether the game may start, it only shows the reason
    public class PlaytestLaunchGateView : MonoBehaviour
    {
        [SerializeField] private ServerConnectPopup messagePopup;

        private void Start()
        {
            EvaluateAsync().Forget();
        }

        private async UniTaskVoid EvaluateAsync()
        {
            // 照合しない起動では待ち文言すら出さない。Editorや自作ビルドのタイトルは従来どおり無音で開く
            // A launch that is never checked shows no waiting message either, so the Editor title opens silently as before
            var ticketProvider = new PlaytestSteamTicketProvider();
            if (!PlaytestLaunchGate.RequiresCheck(ticketProvider))
            {
                PlaytestLaunchGate.SetCurrent(PlaytestGateDecision.DeveloperMode);
                return;
            }

            messagePopup.SetText(Localize.Get(LocalizationKeys.Ui.Playtest.Checking));

            var session = new PlaytestSession(new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl), ticketProvider);
            var result = await PlaytestLaunchGate.EvaluateAsync(session, ticketProvider, DateTime.UtcNow, destroyCancellationToken);

            if (result.IsBlocked)
            {
                messagePopup.SetText(Localize.GetFormatted(result.ReasonKey, new[] { result.Detail }));
                return;
            }

            messagePopup.gameObject.SetActive(false);
            if (result.Status != PlaytestGateStatus.Allowed) return;

            // 照合を通った配布版は、前回持ち越した箱をここで送り始める（起動直後の1回）
            // A distribution build that passed the check starts shipping any deferred boxes here (the once-per-launch run)
            PlaytestUploadRunner.Instance.RequestUpload(PlaytestLaunchGate.Session);
        }
    }
}
