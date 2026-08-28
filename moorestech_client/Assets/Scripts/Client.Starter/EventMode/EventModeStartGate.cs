using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.EventMode
{
    /// <summary>
    /// 出展モードの開始ゲート。言語が選ばれるまで開始を止め、選択と同時に無操作監視を始める。
    /// The event-mode start gate: holds the start until a language is chosen and begins idle watching at that moment.
    /// </summary>
    public static class EventModeStartGate
    {
        public static async UniTask WaitForLanguageSelectionAsync()
        {
            var settings = EventExhibitionSettings.FromEnvironment();
            if (!settings.IsEnabled) return;

            // 画面を出せないなら無人ブースを止めない方を採る。英語のまま開始し監視だけ始める
            // With no screen to show, keeping the unattended booth alive wins: start in English and only begin watching
            var hub = Client.WebUiHost.Boot.WebUiHost.Hub;
            if (hub == null)
            {
                Debug.LogError("EventModeStartGate: WebUiHostが起動しておらず言語選択を出せないため英語のまま開始します");
                EventIdleQuitWatcher.Create(settings.IdleTimeoutSeconds);
                return;
            }

            var gate = EventLanguageGateBinder.Bind(hub);
            await gate.WaitForSelectionAsync();

            // 監視の生成が武装そのもの。待機中は個体が存在しないので無操作終了は起こり得ない
            // Creating the watcher is the arming itself: no instance exists while waiting, so an idle quit cannot fire
            EventIdleQuitWatcher.Create(settings.IdleTimeoutSeconds);
        }
    }
}
