using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Server.Editor
{
    /// <summary>
    /// プレイヤービルドせずdefineのみCI検査する。
    /// Unityは-executeMethodの実行前に全アセンブリをコンパイルするため、このメソッドに到達できた時点で
    /// コンパイルは成功している。到達後はアセンブリ一覧が空でないことだけ追加で確かめる。
    ///
    /// CI checks compilation under the target platform's defines only, without a player build.
    /// Unity compiles every assembly before running -executeMethod, so reaching this method already proves
    /// compilation succeeded; afterwards it only additionally checks that the assembly list is not empty.
    /// </summary>
    public static class PlatformCompileCheck
    {
        public static void RunFromGithubAction()
        {
            // 実際に切り替わったターゲットを、ワークフローが渡した期待値と突合する
            // Compare the target actually in effect against the expected value passed by the workflow
            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            Debug.Log($"[PlatformCompileCheck] activeBuildTarget={activeTarget}");

            // 期待値はmatrixが正本。食い違ったまま緑になると検査が無意味になるので落とす
            // The matrix owns the expected value; going green on a mismatch would void the check, so fail instead
            var expectedTarget = ReadCommandLineValue("-expectedBuildTarget");
            if (expectedTarget != activeTarget.ToString())
            {
                Debug.LogError($"[PlatformCompileCheck] expected {expectedTarget} but the active target is {activeTarget}");
                EditorApplication.Exit(1);
                return;
            }

            // 同プロジェクトにグローバル名前空間のBuildPipelineクラスがあり修飾しないとそちらが優先される
            // The project declares a global BuildPipeline class that wins over UnityEditor's unless qualified
            var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(
                UnityEditor.BuildPipeline.GetBuildTargetGroup(activeTarget));
            var defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            Debug.Log($"[PlatformCompileCheck] defines={defines}");

            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            Debug.Log($"[PlatformCompileCheck] player assemblies={assemblies.Length}");

            // アセンブリが1つも無いのはインポートが破綻している状態で、コンパイル成功と区別する必要がある
            // Zero assemblies means the import itself is broken, which must be distinguished from a clean compile
            if (assemblies.Length == 0)
            {
                Debug.LogError("[PlatformCompileCheck] no player assemblies were produced");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[PlatformCompileCheck] " + string.Join(", ", assemblies.Select(a => a.name)));
            EditorApplication.Exit(0);

            #region Internal

            // -executeMethodへ渡されたフラグの次の要素を返す。未指定なら空文字
            // Returns the element following the given flag in the command line, or an empty string when absent
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
