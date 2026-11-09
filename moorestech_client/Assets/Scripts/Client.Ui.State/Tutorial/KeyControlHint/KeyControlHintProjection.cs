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
        private readonly HashSet<string> _warnedTextKeys = new();

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
                    // silent-ok: 読めないと分かった文は再検証の LogError を出し直さない / 初回に TutorialTextFactory が LogError を出した
                    if (_warnedTextKeys.Contains(key)) continue;
                    if (!TutorialTextFactory.TryCreate(keyControl.TutorialGuid, $"keyControl {key}", out var text))
                    {
                        _warnedTextKeys.Add(key);
                        continue;
                    }
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
