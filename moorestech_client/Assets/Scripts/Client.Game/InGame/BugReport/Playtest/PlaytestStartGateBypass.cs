using System;
using System.IO;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Playtest
{
    // 開始ゲートは応答があるまで初期化を止める。応答者のいない自動起動（テスト・プレイテストDSL）はここで迂回を宣言する
    // The start gates hold initialization until answered, so an unattended boot (tests, the playtest DSL) declares its bypass here
    // 印は本番の BugReports/ ではなく専用ルートへ置く。本物の退避物と正常終了マーカーを一切書き換えないため
    // The mark lands in a dedicated root instead of the production BugReports/, so a real salvage and its clean-exit markers are never rewritten
    // ルートの中は pid で割る。印が残ったままEditorが死んでも、別プロセスの起動には一切効かない（バッチ1のpid印と同じ綴り）
    // Inside the root the marks are split by pid, so a leftover never affects another process's boot (the same spelling as batch 1's pid marks)
    public static class PlaytestStartGateBypass
    {
        public static string RootDirectory => Path.Combine(Application.temporaryCachePath, "playtest-start-gate-bypass");

        public static string MarkerPath(int processId)
        {
            return Path.Combine(RootDirectory, RecordingProcessDirectories.ProcessDirectoryPrefix + processId);
        }

        // 自動起動の入口から呼ぶ。印はドメインリロードを越えて残る必要があるためディスクに置く
        // Called from the unattended entry points; the mark lives on disk because it must outlive a domain reload
        public static void Apply()
        {
            WriteMarker(MarkerPath(RecordingProcessDirectories.CurrentProcessId()));
        }

        public static void Clear()
        {
            DeleteMarker(MarkerPath(RecordingProcessDirectories.CurrentProcessId()));
        }

        // 迂回する理由。応答者が居ないまま待つと恒久停止するので、理由は必ず開発者ログへ出す側（ゲート）へ返す
        // Why the boot bypasses; waiting with nobody to answer halts forever, so the reason is handed back to the gate that logs it
        public static string UnattendedReason()
        {
            if (Application.isBatchMode) return "batchMode";
            return File.Exists(MarkerPath(RecordingProcessDirectories.CurrentProcessId())) ? "unattendedBootMark" : null;
        }

        // ディスクは外部資源。印を置けなくても起動自体は続け、その場合ゲートが出て止まることを理由付きで残す
        // Disk is an external resource; the boot continues without the mark, and the resulting stall at the gate is logged with its reason
        private static void WriteMarker(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "");
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                Debug.LogError($"開始ゲート迂回の印を書けませんでした（無人起動が開始ゲートで止まります） {path}: {e.Message}");
            }
        }

        private static void DeleteMarker(string path)
        {
            // 迂回印の削除もディスクIO。書き込みと同じ資源なので消せなくてもEditor終了は続ける
            // Deleting the bypass mark is disk IO too; it shares the same resource as the write, so failure never blocks the Editor from closing
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                Debug.LogWarning($"開始ゲート迂回の印を消せませんでした（このEditorプロセスの間だけゲートが出ません） {path}: {e.Message}");
            }
        }
    }
}
