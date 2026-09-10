using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Server.Editor
{
    /// <summary>
    /// プレイヤービルドせずdefineのみCI検査する。
    /// Unityは-executeMethodの実行前に全アセンブリをコンパイルするため、このメソッドへ到達できたこと自体が
    /// 「そのPFのdefine下でコンパイルが成功した」唯一の根拠であり、他に合否を決める検査は無い。
    /// 射程はUNITY_EDITOR定義下のコンパイルに限られる（ADR 0028の検出範囲節と同旨）。
    ///
    /// CI checks compilation under the target platform's defines only, without a player build.
    /// Unity compiles every assembly before running -executeMethod, so reaching this method is itself the only
    /// evidence that compilation succeeded under that platform's defines; nothing else here decides pass or fail.
    /// Its reach is limited to compilation under the UNITY_EDITOR define (same scope as ADR 0028's detection section).
    /// </summary>
    public static class PlatformCompileCheck
    {
        public static void RunFromGithubAction()
        {
            // 実際のターゲットを期待値と突合
            // Compare the active target against the expected value
            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            Debug.Log($"[PlatformCompileCheck] activeBuildTarget={activeTarget}");

            // 期待値はmatrixが正本。食い違ったまま緑になると検査が無意味になるので落とす
            // The matrix owns the expected value; going green on a mismatch would void the check, so fail instead
            var expectedTarget = ReadCommandLineValue("-expectedBuildTarget");
            if (!Enum.TryParse<BuildTarget>(expectedTarget, out var expected))
            {
                // 綴りミス・フラグ未指定は切替失敗と原因が別
                // A typo or a missing flag has a different cause than a failed switch
                Debug.LogError($"[PlatformCompileCheck] -expectedBuildTarget is missing or unknown: '{expectedTarget}'");
                EditorApplication.Exit(1);
                return;
            }
            if (expected != activeTarget)
            {
                Debug.LogError($"[PlatformCompileCheck] expected {expected} but the active target is {activeTarget}");
                EditorApplication.Exit(1);
                return;
            }

            // 同プロジェクトにグローバル名前空間のBuildPipelineクラスがあり修飾しないとそちらが優先される
            // The project declares a global BuildPipeline class that wins over UnityEditor's unless qualified
            var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(
                UnityEditor.BuildPipeline.GetBuildTargetGroup(activeTarget));
            var defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            Debug.Log($"[PlatformCompileCheck] defines={defines}");

            // アセンブリ一覧はasmdef定義の列挙で合否には用いない。失敗時に何が対象だったか読むための診断ログ
            // The assembly list enumerates asmdef definitions and never decides pass or fail; it is diagnostics for reading a failure
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            Debug.Log($"[PlatformCompileCheck] player assemblies={assemblies.Length}");
            Debug.Log("[PlatformCompileCheck] " + string.Join(", ", assemblies.Select(a => a.name)));
            EditorApplication.Exit(0);

            #region Internal

            // フラグの次要素を返す。無指定なら空文字
            // Returns the element following the flag; empty string when absent.
            string ReadCommandLineValue(string flag)
            {
                var args = Environment.GetCommandLineArgs();
                for (var i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == flag) return args[i + 1];
                }
                return string.Empty;
            }

            #endregion
        }
    }
}
