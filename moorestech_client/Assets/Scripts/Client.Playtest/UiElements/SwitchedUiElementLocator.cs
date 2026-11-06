using Client.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.UiElements
{
    /// <summary>
    /// 全体スイッチで選ばれた系のロケータだけで探す。もう片方は照会もセッションも開かない（plan §3.3 R2・§4.4）
    /// Looks up only with the locator of the system chosen by the global switch; the other side is neither queried nor opened (plan §3.3 R2, §4.4)
    /// </summary>
    internal sealed class SwitchedUiElementLocator : IUiElementLocator
    {
        private readonly IUiElementLocator _active;

        public SwitchedUiElementLocator(UiSystem activeSystem, IUiElementLocator gameUi, IUiElementLocator web)
        {
            _active = activeSystem == UiSystem.GameUi ? gameUi : web;

            // シナリオのログから、どちらの系で探したかを辿れるようにする
            // Leave in the scenario log which system the lookups used
            Debug.Log($"[PlaytestUi] locator reads only the {activeSystem} UI");
        }

        public void StartSession()
        {
            _active.StartSession();
        }

        public void StopSession()
        {
            _active.StopSession();
        }

        public UniTask<UiElementSnapshot> Find(string testid, float remainingSeconds)
        {
            return _active.Find(testid, remainingSeconds);
        }

        public UniTask<UiSetTextResult> SetText(string testid, string text, float remainingSeconds)
        {
            return _active.SetText(testid, text, remainingSeconds);
        }
    }
}
