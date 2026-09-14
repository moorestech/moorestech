using Client.Game.InGame.BugReport.Playtest;
using UnityEditor;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    // 迂回の印はEditorプロセス寿命より長く残り得るディスク上のファイル。PlayModeを抜けたら必ず消し、手動再生にゲートを戻す
    // The bypass mark is a file that can outlive the Editor process, so it is removed on leaving Play Mode to give a manual run its gates back
    // 印は pid で割ってあるため消し損ねても他プロセスには効かないが、同じEditorでの手動再生には効いてしまう
    // The mark is split by pid, so a leftover never reaches another process, but it would still silence a manual run in this same Editor
    [InitializeOnLoad]
    public static class PlaytestStartGateBypassCleanup
    {
        // 購読はドメインリロードで消えるため、Editorの読み込みごとに張り直す
        // The subscription dies with every domain reload, so it is re-established on each Editor load
        static PlaytestStartGateBypassCleanup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) PlaytestStartGateBypass.Clear();
        }
    }
}
