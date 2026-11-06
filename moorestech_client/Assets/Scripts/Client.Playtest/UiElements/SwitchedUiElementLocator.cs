using Client.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.UiElements
{
    /// <summary>
    /// 選ばれた系だけで探す。他方は開かない
    /// Looks up only in the chosen system; the other is never opened
    /// </summary>
    internal sealed class SwitchedUiElementLocator : IUiElementLocator
    {
        private readonly IUiElementLocator _active;

        public SwitchedUiElementLocator(UiSystem activeSystem, IUiElementLocator gameUi, IUiElementLocator web)
        {
            _active = activeSystem == UiSystem.GameUi ? gameUi : web;

            // 探した系をログに残す
            // Log which system the lookup used
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
