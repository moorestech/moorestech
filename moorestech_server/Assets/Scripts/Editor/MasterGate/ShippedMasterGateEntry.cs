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

        // CI の入口。G1 → G3 の順に回す（-masterDataRoot と、-researchOrder に G2 の出力が要る）
        // CI entry: run G1 then G3 (needs -masterDataRoot and -researchOrder pointing at G2's output)
        public static void RunFromCommandLine()
        {
            var passed = MasterGateArguments.TryResolveServerDataDirectory(out var serverDataDirectory)
                         && RunAll(serverDataDirectory, MasterGateArguments.FindResearchOrderPath());
            EditorApplication.Exit(passed ? 0 : 1);
        }

        // G1 の唯一の入口（batch・手元の G1 関数・RunAll がここを通る）
        // The single G1 entry (batch, the local G1 function and RunAll all go through it)
        public static bool RunLoadGate(string serverDataDirectory, out string failure)
        {
            return ShippedMasterOwner.TryLoad(serverDataDirectory, out failure);
        }

        // Editor を閉じずに判定する入口（uloop execute-dynamic-code から呼ぶ）。G1 が落ちたら G3 は回さない
        // Judge without quitting the Editor (called from uloop execute-dynamic-code); G3 is skipped when G1 fails
        public static bool RunAll(string serverDataDirectory, string researchOrderPath)
        {
            if (!RunLoadGate(serverDataDirectory, out _)) return false;
            if (!ResearchWalkOrderFile.TryRead(researchOrderPath, out var steps, out _)) return false;
            if (!ShippedMasterOwner.TryCreateServer(serverDataDirectory, out var serviceProvider, out _)) return false;
            return ResearchWalkRunner.Run(serviceProvider, steps, out _);
        }
    }
}
