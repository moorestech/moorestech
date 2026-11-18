using System.Collections.Generic;
using Client.Ui.Foundation.Input.Gesture;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input.Gesture
{
    /// <summary>
    /// 押下の分類の呼ばれ方を文字列で記録する（テスト用）
    /// Records press classification calls as strings (test double)
    /// </summary>
    internal sealed class RecordingPressHandler : IUiPressGestureHandler
    {
        public readonly List<string> Calls = new();

        public void OnPressStarted(Vector2 panelPosition)
        {
            Calls.Add("start");
        }

        public void OnDragMoved(UiPressMove move)
        {
            Calls.Add($"move:{move.FromStart.x:0.#},{move.FromStart.y:0.#}|{move.SinceLast.x:0.#},{move.SinceLast.y:0.#}");
        }

        public void OnDragEnded(Vector2 panelPosition)
        {
            Calls.Add("end");
        }

        public void OnTapped(VisualElement pressTarget)
        {
            Calls.Add($"tap:{pressTarget.name}");
        }

        public void OnAborted(bool dragged)
        {
            Calls.Add($"abort:{dragged}");
        }
    }
}
