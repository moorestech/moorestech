using System;
using System.Collections.Generic;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Tutorial
{
    /// <summary>
    /// チュートリアル GUID を content の文にする。読めない GUID は GUID ごとに 1 回だけ LogError する
    /// Turns a tutorial GUID into content text; an unreadable GUID logs its error once per GUID
    /// </summary>
    public sealed class TutorialTextFactory
    {
        private readonly HashSet<string> _invalidGuids = new();

        public bool TryCreate(string tutorialGuid, string context, out UiText text)
        {
            text = null;
            // silent-ok: 読めないと分かった GUID は LogError を出し直さない（射影はデータ変化・毎フレームで組み直すため） / 同じ GUID は初回に下の LogError で開発者へ出た
            if (_invalidGuids.Contains(tutorialGuid)) return false;
            // マスタ由来の GUID が読めなければ、その 1 件を出さず開発者に理由を出す
            // When the master GUID cannot be parsed, skip that one item and tell developers why
            if (!Guid.TryParse(tutorialGuid, out var guid))
            {
                _invalidGuids.Add(tutorialGuid);
                Debug.LogError($"[Tutorial] tutorialGuid is not a GUID: '{tutorialGuid}' ({context})");
                return false;
            }
            text = UiText.FromContent(ContentLocalizationKeys.ChallengeTutorialText(guid));
            return true;
        }
    }
}
