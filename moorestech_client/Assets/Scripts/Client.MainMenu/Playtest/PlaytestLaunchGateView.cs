using System;
using Client.Localization;
using Client.MainMenu.PopUp;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Gate;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.MainMenu.Playtest
{
    // タイトル用の表示専用View。開始可否は決めず、照合結果を購読して理由を出す
    // A display-only view on the title screen; it never decides whether to start and only mirrors the verdict it subscribes to
    public class PlaytestLaunchGateView : MonoBehaviour
    {
        [SerializeField] private ServerConnectPopup messagePopup;

        private IPlaytestUploadRequester _uploadRequester;

        private void Start()
        {
            // MainMenuにはDIコンテナが無いので、ここを合成ルートとして受け口と走行役を組む
            // The MainMenu scene has no DI container, so this is the composition root for the receiver client and the runner
            var receiver = new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl);
            _uploadRequester = new PlaytestUploadRunner(receiver, PlaytestOutboxDirectories.FromGameSystemPaths());

            PlaytestLaunchGate.Current.Subscribe(Show).AddTo(this);
            PlaytestLaunchGate.EvaluateAsync(new PlaytestSteamTicketProvider(), receiver, DateTime.UtcNow, destroyCancellationToken).Forget();
        }

        private void Show(PlaytestGateResult result)
        {
            // 照合しない起動では待ち文言すら出さない。Editorや自作ビルドのタイトルは従来どおり無音で開く
            // A launch that is never checked shows no message at all, so the Editor title opens silently as before
            if (result.Status == PlaytestGateStatus.NotEvaluated || result.Status == PlaytestGateStatus.DeveloperMode) return;

            if (result.IsBlocked)
            {
                messagePopup.SetText(Localize.Get(result.ReasonKey));
                return;
            }

            // 照合を通った配布版は、前回持ち越した箱をここで送り始める（起動直後の1回）
            // A distribution build that passed the check starts shipping any deferred boxes here (the once-per-launch run)
            messagePopup.gameObject.SetActive(false);
            _uploadRequester.RequestUpload();
        }
    }
}
