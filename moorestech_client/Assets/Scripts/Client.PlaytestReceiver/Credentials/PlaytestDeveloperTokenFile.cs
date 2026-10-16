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
            string contents;
            // ディスクIO境界。読めない資格情報を呼び手へ返す
            // Disk IO boundary: return unreadable credentials to the caller
            try { contents = File.ReadAllText(path); }
            // silent-ok: 未設定は正常な状態 / 呼び手がAbsentを送信状況へ載せる
            catch (FileNotFoundException) { return new PlaytestDeveloperTokenAbsent(); }
            // silent-ok: 未設定ディレクトリは正常 / 呼び手がAbsentを送信状況へ載せる
            catch (DirectoryNotFoundException) { return new PlaytestDeveloperTokenAbsent(); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return Unreadable(exception.Message);
            }
            var token = contents.Trim();
            if (Regex.IsMatch(token, TokenPattern)) return new PlaytestDeveloperTokenPresent(token);
            return Unreadable("トークンの形が違う");

            #region Internal

            PlaytestDeveloperTokenUnreadable Unreadable(string reason)
            {
                Debug.LogWarning($"[PlaytestReceiver] developer token is unreadable: {reason}");
                return new PlaytestDeveloperTokenUnreadable(reason);
            }

            #endregion
        }
    }
}
