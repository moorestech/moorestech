using System;
using Client.RemoteExec.Access;
using UnityEngine;

namespace Client.RemoteExec
{
    // 起動引数から遠隔実行の許可を決める（ADR 0072）
    // Resolve remote execution permission from launch arguments (ADR 0072)
    public static class RemoteExecLaunchOption
    {
        internal const string Marker = "--remoteExec";

        public static bool IsEnabled { get; private set; }

        public static void ResolveFromCommandLine(string[] args)
        {
            IsEnabled = 0 <= Array.IndexOf(args, Marker);
            // 無効な起動では前回の入口を撤去する。残すと死んだプロセスのトークンが有効な入口として読まれる
            // A disabled boot withdraws the previous entry; leaving it lets a dead process's token read as a live one
            if (!IsEnabled)
            {
                RemoteExecAccessFile.Remove();
                return;
            }
            // サーバー側実行は送信コードがtickスレッドを占有しうる。無音停止と区別できるよう起動時に明示する
            // A server-side run can occupy the tick thread, so the boot log says so to keep a stall distinguishable from silence
            Debug.LogWarning($"[RemoteExec] {Marker} が指定されたため遠隔実行を有効にします（server実行の送信コードはtick末尾を占有し、返らなければセーブ確定点とスナップショットが止まります）");
        }
    }
}
