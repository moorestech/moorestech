using System;
using System.Collections.Generic;

namespace Client.PlaytestReceiver.Http
{
    // アップロードURLのパス組み立て。ここが唯一の組み立て地点で、Task 7側では直せない
    // The single place where an upload URL path is built; nothing downstream can correct it
    public static class PlaytestUploadPath
    {
        // 逸脱を含む名前は受け口が400で弾く。送る前にnullで返し、呼び出し側が到達失敗と取り違えないようにする
        // The receiver rejects traversal with a 400, so an unsafe name returns null here instead of looking unreachable
        public static string ForFile(PlaytestUploadKind kind, string bundleId, string relativePath)
        {
            var segments = relativePath.Replace('\\', '/').Trim('/').Split('/');
            var escaped = new List<string>(segments.Length);
            foreach (var segment in segments)
            {
                if (segment.Length == 0 || segment == "." || segment == "..") return null;
                escaped.Add(Escape(segment));
            }
            if (escaped.Count == 0) return null;

            return $"{KindSegment(kind)}/{Escape(bundleId)}/{string.Join("/", escaped)}";
        }

        public static string ForComplete(PlaytestUploadKind kind, string bundleId)
        {
            return $"{KindSegment(kind)}/{Escape(bundleId)}/complete";
        }

        // 種別から受け口の語への唯一の変換。語の集合は contract.json と一致をテストで固定する
        // The only mapping from kind to the receiver's word; the word set is pinned to contract.json by a test
        public static string KindSegment(PlaytestUploadKind kind)
        {
            switch (kind)
            {
                case PlaytestUploadKind.Report: return "report";
                case PlaytestUploadKind.Progress: return "progress";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown upload kind");
            }
        }

        // セグメントごとにエスケープする。#や?を含む名前が別キーへ化けるのを防ぐ（受け口は1回だけ復号する）
        // Each segment is escaped so a name with # or ? cannot silently become another key; the receiver decodes exactly once
        private static string Escape(string segment)
        {
            return Uri.EscapeDataString(segment);
        }
    }
}
