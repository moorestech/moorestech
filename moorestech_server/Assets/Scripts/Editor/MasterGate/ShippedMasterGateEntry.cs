using UnityEditor;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     ゲートの入口。batch の入口は終了コード（0=PASS / 1=FAIL）で返し、理由は Unity のログに出す。static マスタの書き換えは ShippedMasterOwner だけが行う
    ///     Gate entries; batch entries report via the exit code (0 = PASS / 1 = FAIL) and the reason goes to the Unity log; only ShippedMasterOwner rewrites the static master
    /// </summary>
    public static class ShippedMasterGateEntry
    {
        // G1 だけを回す（置き場は -masterDataRoot で必須。無ければ理由をログに出して 1 で終わる）
        // Run G1 only (-masterDataRoot is required; without it the reason is logged and the exit code is 1)
        public static void RunLoadGateFromCommandLine()
        {
            var passed = MasterGateArguments.TryResolveServerDataDirectory(out var serverDataDirectory)
                         && RunLoadGate(serverDataDirectory, out _);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        // G1 の唯一の入口（batch・手元の G1 関数・Task 11 の RunAll がここを通る）
        // The single G1 entry (batch, the local G1 function and Task 11's RunAll all go through it)
        public static bool RunLoadGate(string serverDataDirectory, out string failure)
        {
            return ShippedMasterOwner.TryLoad(serverDataDirectory, out failure);
        }
    }
}
