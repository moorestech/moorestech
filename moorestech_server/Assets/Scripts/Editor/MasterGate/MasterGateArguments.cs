using System;
using System.IO;
using UnityEngine;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     ゲートの入力（出荷マスタの置き場・G2 の研究順ファイル）をコマンドライン引数から解く。置き場は推測せず -masterDataRoot を必須にする
    ///     Resolves gate inputs (shipped master location, G2 research-order file) from command-line args; the location is never guessed and -masterDataRoot is required
    /// </summary>
    public static class MasterGateArguments
    {
        private const string MasterDataRootArgument = "-masterDataRoot";
        private const string ResearchOrderArgument = "-researchOrder";

        // server_v8 の絶対パス。-masterDataRoot が無ければ理由をログに出して false（ピンからの自動解決はしない。判断記録 A23）
        // Absolute server_v8 path; without -masterDataRoot log the reason and return false (no pin auto-resolution, decision A23)
        public static bool TryResolveServerDataDirectory(out string serverDataDirectory)
        {
            var masterRoot = FindArgument(MasterDataRootArgument);
            if (string.IsNullOrEmpty(masterRoot))
            {
                serverDataDirectory = null;
                Debug.LogError($"[MasterGate] FAIL {MasterDataRootArgument} <マスタrepoの根> is required (the gate never guesses the master location)");
                return false;
            }
            serverDataDirectory = Path.GetFullPath(Path.Combine(masterRoot, "server_v8"));
            return true;
        }

        // G2 が書いた研究順 JSON のパス。指定が無ければ null（読み手が理由を出して失敗する）
        // Path of the research-order JSON G2 wrote; null when absent (the reader fails with a reason)
        public static string FindResearchOrderPath()
        {
            return FindArgument(ResearchOrderArgument);
        }

        private static string FindArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }
    }
}
