using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Client.PlaytestReceiver.Credentials
{
    public static class PlaytestDeveloperTokenFile
    {
        public const string TokenPattern = "^mdt_[0-9a-f]{64}$";

        public static PlaytestDeveloperTokenRead Read(string path)
        {
            // ディスクIO境界。読めない資格情報を状態として呼び手へ返す
            // Disk IO boundary: expose unreadable credentials as a result to the caller
            try
            {
                var token = File.ReadAllText(path).Trim();
                if (Regex.IsMatch(token, TokenPattern)) return new PlaytestDeveloperTokenPresent(token);
                return Unreadable("トークンの形が違う");
            }
            // silent-ok: 未設定は正常な状態 / 呼び手がAbsentを受け取り送信状況へ載せる
            catch (FileNotFoundException) { return new PlaytestDeveloperTokenAbsent(); }
            // silent-ok: 未設定ディレクトリは正常 / 呼び手がAbsentを受け取り送信状況へ載せる
            catch (DirectoryNotFoundException) { return new PlaytestDeveloperTokenAbsent(); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return Unreadable(exception.Message);
            }
        }

        public static void Write(string path, string token)
        {
            // ディスクIO境界。保存失敗は呼び手にも伝え成功扱いを防ぐ
            // Disk IO boundary: propagate persistence failures so callers cannot report success
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, token);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[PlaytestReceiver] developer token could not be saved: {exception.Message}");
                throw;
            }
        }

        public static void Delete(string path)
        {
            // ディスクIO境界。削除失敗をログと呼び手の両方へ届ける
            // Disk IO boundary: expose deletion failures to both the log and caller
            try { File.Delete(path); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[PlaytestReceiver] developer token could not be deleted: {exception.Message}");
                throw;
            }
        }

        private static PlaytestDeveloperTokenUnreadable Unreadable(string reason)
        {
            Debug.LogWarning($"[PlaytestReceiver] developer token is unreadable: {reason}");
            return new PlaytestDeveloperTokenUnreadable(reason);
        }
    }
}
