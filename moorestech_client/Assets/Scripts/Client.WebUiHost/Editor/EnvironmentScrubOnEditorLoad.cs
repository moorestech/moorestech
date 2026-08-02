#if UNITY_EDITOR
using Client.WebUiHost.Common;
using UnityEditor;

namespace Client.WebUiHost.Editor
{
    /// <summary>
    /// エディタロード時点で自プロセスの汚染envを浄化する
    /// Scrubs corrupted env vars from this process as soon as the editor loads
    /// </summary>
    [InitializeOnLoad]
    public static class EnvironmentScrubOnEditorLoad
    {
        static EnvironmentScrubOnEditorLoad()
        {
            // PlayMode前のネイティブspawn（CEF等）にも綺麗なenvironを保証する
            // Guarantees a clean environ even for native spawns (e.g. CEF) before PlayMode
            SanitizedProcessEnvironment.SanitizeCurrentProcess();
        }
    }
}
#endif
