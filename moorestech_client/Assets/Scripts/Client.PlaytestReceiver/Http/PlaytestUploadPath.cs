using System;

namespace Client.PlaytestReceiver.Http
{
    // アップロードURLのパス組み立て。ここが唯一の組み立て地点で、Task 7側では直せない
    // The single place where an upload URL path is built; nothing downstream can correct it
    public static class PlaytestUploadPath
    {
        public static string ForPrepare(PlaytestUploadKind kind, string bundleId)
        {
            return $"{KindSegment(kind)}/{Escape(bundleId)}/prepare";
        }

        public static string ForComplete(PlaytestUploadKind kind, string bundleId)
        {
            return $"{KindSegment(kind)}/{Escape(bundleId)}/complete";
        }

        // クライアント側の宣言規則の正本。受け口の parseDeclaration（bundleDeclaration.ts）と同じ順で見て、拒否理由かnullを返す
        // The client's single source of the declaration rules; checks in the receiver's parseDeclaration order and returns the rejection reason or null
        public static string DescribeRejection(string relative, long bytes, int declaredCount, long declaredTotal)
        {
            var segments = relative.Split('/');
            foreach (var segment in segments)
            {
                if (!IsSafeSegment(segment)) return "unsafe-path";
            }
            // 先頭セグメントが予約名だと受け口の印（READY/ACKED/DECLARED）や操作名と衝突する
            // A reserved first segment would collide with the receiver's markers (READY/ACKED/DECLARED) or verbs
            if (0 <= Array.IndexOf(PlaytestReceiverConfig.ReservedUploadSegments, segments[0])) return "reserved-name";
            if (PlaytestReceiverConfig.MaxFileBytes < bytes) return "too-large";
            if (PlaytestReceiverConfig.MaxBundleFiles <= declaredCount) return "too-many-files";
            if (PlaytestReceiverConfig.MaxBundleBytes < declaredTotal + bytes) return "bundle-too-large";
            return null;
        }

        // 受け口の keys.ts isSafeSegment と同じ規則。逸脱と区切り文字・制御文字だけを拒み、UTF-8の実ファイル名は通す
        // The same rule as the receiver's isSafeSegment in keys.ts; only traversal, separators and control characters are refused, UTF-8 names pass
        private static bool IsSafeSegment(string segment)
        {
            if (segment.Length == 0 || segment == "." || segment == "..") return false;
            foreach (var character in segment)
            {
                if (character == '\\' || character < 0x20 || character == 0x7f) return false;
            }
            return true;
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
