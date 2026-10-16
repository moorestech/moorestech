#if UNITY_EDITOR
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

internal static class DeveloperTokenEditorPermissions
{
    internal static string RestrictToOwner(string path, bool directory)
    {
        // RemoteExecと同じOSの権限制御を使い、秘密値を書き込む前に他ユーザーのアクセスを外す
        // Use the same OS permission tools as RemoteExec, removing other users before writing secrets
        var windows = Path.DirectorySeparatorChar == '\\';
        var fileName = windows ? "powershell.exe" : "/bin/chmod";
        var arguments = windows
            ? WindowsArguments(path, directory)
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

        #region Internal

        string WindowsArguments(string path, bool directory)
        {
            // 既存の明示ACEも継承ACEも引き継がず、現在のSIDだけでACLを構築する
            // Build a fresh ACL for the current SID without preserving explicit or inherited ACEs
            var encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(path));
            var securityType = directory ? "DirectorySecurity" : "FileSecurity";
            var inheritance = directory ? "ContainerInherit, ObjectInherit" : "None";
            var script = "$ErrorActionPreference='Stop'; " +
                $"$p=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{encodedPath}')); " +
                "$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User; " +
                $"$acl=New-Object Security.AccessControl.{securityType}; " +
                "$acl.SetOwner($sid); $acl.SetAccessRuleProtection($true,$false); " +
                $"$rule=New-Object Security.AccessControl.FileSystemAccessRule($sid,'FullControl','{inheritance}','None','Allow'); " +
                "$acl.AddAccessRule($rule); Set-Acl -LiteralPath $p -AclObject $acl; " +
                "$actual=Get-Acl -LiteralPath $p; " +
                "if (!$actual.AreAccessRulesProtected -or $actual.Owner -ne $sid.Translate([Security.Principal.NTAccount]).Value) { throw 'Owner ACL verification failed' }; " +
                "$rules=$actual.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]); " +
                "if ($rules.Count -ne 1 -or $rules[0].IdentityReference -ne $sid -or $rules[0].AccessControlType -ne 'Allow') { throw 'Unexpected access rules' }";
            return "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        }

        #endregion
    }
}
#endif
