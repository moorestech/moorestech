#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using Client.PlaytestReceiver.Credentials;

internal static class DeveloperTokenEditorFile
{
    internal static (DeveloperTokenWindowResultKind Kind, DeveloperTokenWindowResultData Data) Write(string path, string input)
    {
        var token = input.Trim();
        if (!Regex.IsMatch(token, PlaytestDeveloperTokenFile.TokenPattern))
            return (DeveloperTokenWindowResultKind.InvalidToken, new DeveloperTokenWindowResultData(""));
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string failure = null;
        // 外部ディスク境界。同じディレクトリで書き終えてから原子的に公開する
        // External disk boundary: finish writing in the same directory before atomic publication
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            failure = DeveloperTokenEditorPermissions.RestrictToOwner(Path.GetDirectoryName(path), true);
            if (failure == null)
            {
                File.WriteAllText(temporary, token);
                failure = DeveloperTokenEditorPermissions.RestrictToOwner(temporary, false);
                if (failure == null)
                {
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                }
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            failure = exception.Message;
        }
        if (failure == null) return (DeveloperTokenWindowResultKind.Saved, new DeveloperTokenWindowResultData(""));

        // 一時ファイルの後片付けも外部ディスク境界。失敗を元の理由へ足して画面とログへ渡す
        // Temporary-file cleanup is an external disk boundary; retain its failure for the UI and log
        try { File.Delete(temporary); }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            failure += $" / 一時ファイルを削除できません: {temporary}: {exception.Message}";
        }
        return (DeveloperTokenWindowResultKind.SaveFailed, new DeveloperTokenWindowResultData(failure));
    }

    internal static (DeveloperTokenWindowResultKind Kind, DeveloperTokenWindowResultData Data) Delete(string path)
    {
        string failure = null;
        // 外部ディスク境界。結果は呼び手が画面とログの両方へ表示する
        // External disk boundary: the caller publishes the result to both the UI and log
        try { File.Delete(path); }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            failure = exception.Message;
        }
        return failure == null
            ? (DeveloperTokenWindowResultKind.Deleted, new DeveloperTokenWindowResultData(""))
            : (DeveloperTokenWindowResultKind.DeleteFailed, new DeveloperTokenWindowResultData(failure));
    }
}
#endif
