using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.UIState;
using UnityEngine;

namespace Client.Ui.State.Tutorial.KeyControlHint
{
    /// <summary>
    /// keyControl 要素を今の画面と blocking スキットで絞ってヒントにする
    /// Filters keyControl elements by the current screen and blocking skit into hints
    /// </summary>
    public sealed class KeyControlHintProjection
    {
        private readonly HashSet<string> _warnedUiStates = new();
        private readonly TutorialTextFactory _textFactory = new();

        public KeyControlHintViewModel Project(TutorialPresentationData data, UIStateEnum current, bool blockingSkit)
        {
            // blocking のスキット中はスキットに画面を譲る
            // Yield the screen to a blocking skit
            if (blockingSkit) return KeyControlHintViewModel.Empty;
            var hints = new List<KeyControlHintCue>();
            foreach (var session in data.Sessions)
            {
                foreach (var element in session.Elements)
                {
                    if (element is not TutorialKeyControlElementData keyControl || !Matches(keyControl.UiState)) continue;
                    var key = TutorialElementKey.Of(session.TutorialSessionId, element.ElementId);
                    // 読めない GUID のヒントは外す（LogError は TutorialTextFactory が GUID ごとに 1 回）
                    // Drop a hint with an unreadable GUID (TutorialTextFactory logs once per GUID)
                    if (!_textFactory.TryCreate(keyControl.TutorialGuid, $"keyControl {key}", out var text)) continue;
                    hints.Add(new KeyControlHintCue(keyControl.KeyName, text));
                }
            }
            return new KeyControlHintViewModel(hints);

            #region Internal

            bool Matches(string uiState)
            {
                // 語彙外の綴りはどの画面とも一致させず、1 回だけ警告する（Web matchesUiState）
                // An out-of-vocabulary spelling matches no screen and warns once (Web matchesUiState)
                if (Enum.TryParse<UIStateEnum>(uiState, false, out var parsed) && Enum.IsDefined(typeof(UIStateEnum), parsed)) return parsed == current;
                if (_warnedUiStates.Add(uiState)) Debug.LogWarning($"[tutorial] Unknown keyControl uiState: {uiState}");
                return false;
            }

            #endregion
        }
    }
}
