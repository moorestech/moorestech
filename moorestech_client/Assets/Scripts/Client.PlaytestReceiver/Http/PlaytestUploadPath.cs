using System;
using System.Collections.Generic;

namespace Client.PlaytestReceiver.Http
{
    // アップロードURLのパス組み立て。ここが唯一の組み立て地点で、Task 7側では直せない
    // The single place where an upload URL path is built; nothing downstream can correct it
    public static class PlaytestUploadPath
    {
        public static string ForFile(string kind, string bundleId, string relativePath)
        {
            return $"{Escape(kind)}/{Escape(bundleId)}/{EscapeRelativePath(relativePath)}";
        }

        public static string ForComplete(string kind, string bundleId)
        {
            return $"{Escape(kind)}/{Escape(bundleId)}/complete";
        }

        // Windowsの\区切りを/へ寄せ、セグメントごとにエスケープする。#や?を含む名前が別キーへ化けるのを防ぐ
        // Backslashes are normalized and each segment is escaped so a name with # or ? cannot silently become another key
        private static string EscapeRelativePath(string relativePath)
        {
            var segments = relativePath.Replace('\\', '/').Split('/');
            var escaped = new List<string>(segments.Length);
            foreach (var segment in segments)
            {
                if (segment.Length == 0) continue;
                escaped.Add(Escape(segment));
            }
            return string.Join("/", escaped);
        }

        private static string Escape(string segment)
        {
            return Uri.EscapeDataString(segment);
        }
    }
}
