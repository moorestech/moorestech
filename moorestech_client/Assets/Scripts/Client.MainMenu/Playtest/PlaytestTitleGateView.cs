using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Launch;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Client.Starter.Playtest.TitleGates;
using UniRx;
using UnityEngine;

namespace Client.MainMenu.Playtest
{
    // タイトルの合成ルート。同意と前回異常終了の確認を始め、段階をポップアップへ映す（ADR 0070）
    // The title composition root begins consent and previous-crash confirmation and mirrors their step onto the popups (ADR 0070)
    public class PlaytestTitleGateView : MonoBehaviour
    {
        [SerializeField] private PlaytestConsentPopup consentPopup;
        [SerializeField] private CrashReportPopup crashReportPopup;

        private void Start()
        {
            // タイトルの退避物を扱う前に配布版の識別を確定する
            // Publish the distribution identity before handling title salvage
            PlaytestLaunchProfile.EnsureIdentityPublished();
            // MainMenuにはDIコンテナが無いので、ここで受け口と走行役を組む
            // The MainMenu scene has no DI container, so compose the receiver and runner here
            var receiver = new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl);
            var uploadRequester = new PlaytestUploadRunner(receiver, PlaytestOutboxDirectories.FromGameSystemPaths(), new PlaytestSteamTicketProvider());

            // 配布版判定を確定し、通信を待たずにタイトルの確認を始める（ADR 0070）
            // Resolve the distribution kind and begin title confirmations without waiting for a network check (ADR 0070)
            var sequence = PlaytestTitleGates.Begin(uploadRequester);
            consentPopup.Initialize(sequence);
            crashReportPopup.Initialize(sequence);

            // 常時有効な合成ルートが購読を持ち、非アクティブのポップアップへ段階を映す
            // The always-active composition root owns the subscription and mirrors the step onto inactive popups
            sequence.Step.Subscribe(step =>
            {
                consentPopup.SetVisible(step == PlaytestTitleGateStep.Consent);
                crashReportPopup.SetVisible(step == PlaytestTitleGateStep.CrashReport);
            }).AddTo(this);
        }
    }
}
