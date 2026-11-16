#if UNITY_EDITOR
using UnityEditor;

namespace Client.ExternalProcess.Editor
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
            // PlayMode前にEditorから起こす子プロセスやネイティブプラグインにも綺麗なenvironを保証する
            // Guarantees a clean environ for child processes and native plugins the editor starts before PlayMode
            SanitizedProcessEnvironment.SanitizeCurrentProcess();
        }
    }
}
#endif
