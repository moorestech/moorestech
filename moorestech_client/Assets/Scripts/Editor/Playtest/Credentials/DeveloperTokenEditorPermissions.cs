#if UNITY_EDITOR
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

internal static class DeveloperTokenEditorPermissions
{
    internal static string RestrictToOwner(string path, bool directory)
    {
        // RemoteExecと同じOSの権限制御を使い、秘密値を書き込む前に他ユーザーのアクセスを外す
        // Use the same OS permission tools as RemoteExec, removing other users before writing secrets
        var windows = Path.DirectorySeparatorChar == '\\';
        var fileName = windows ? "icacls" : "/bin/chmod";
        var grant = directory ? "(OI)(CI)F" : "F";
        var arguments = windows
            ? $"\"{path}\" /inheritance:r /grant:r \"{Environment.UserName}\":{grant}"
            : $"go-rwx \"{path}\"";
        // 権限制御コマンドは外部プロセス境界。失敗理由を保存結果に渡し、画面とログで通知する
        // Permission commands are an external process boundary; the save result reports failure to UI and logs
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            });
            var error = process.StandardError.ReadToEndAsync();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? null : $"所有者限定にできません: {path}: {fileName} exit:{process.ExitCode} {error.Result}";
        }
        catch (Exception error) when (error is Win32Exception || error is InvalidOperationException || error is IOException)
        {
            return $"所有者限定の設定を実行できません: {path}: {error.Message}";
        }
    }
}
#endif
