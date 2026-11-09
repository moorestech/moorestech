using System;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Tutorial
{
    public static class TutorialTextFactory
    {
        public static bool TryCreate(string tutorialGuid, string context, out UiText text)
        {
            text = null;
            // マスタ由来の GUID が読めなければ、その 1 件を出さず開発者に理由を出す
            // When the master GUID cannot be parsed, skip that one item and tell developers why
            if (!Guid.TryParse(tutorialGuid, out var guid))
            {
                Debug.LogError($"[Tutorial] tutorialGuid is not a GUID: '{tutorialGuid}' ({context})");
                return false;
            }
            text = UiText.FromContent(ContentLocalizationKeys.ChallengeTutorialText(guid));
            return true;
        }
    }
}
