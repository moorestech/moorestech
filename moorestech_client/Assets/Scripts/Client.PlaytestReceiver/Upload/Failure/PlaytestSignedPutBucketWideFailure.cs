using System;
using Client.PlaytestReceiver.Http.Responses;

namespace Client.PlaytestReceiver.Upload.Failure
{
    // 署名付きPUTの拒否のうち、ファイルではなくバケットや受け口の設定全体の失敗を名乗るもの。どのファイル・どの箱も同じ理由で拒まれる
    // Presigned PUT refusals naming a failure of the bucket or the receiver's whole setup rather than the file; every file and box is refused alike
    internal static class PlaytestSignedPutBucketWideFailure
    {
        // 根拠: S3/R2 の Code のうちバケットの不在・名前・鍵を指すもの。ファイルを見送ったり箱を数えたりすると、設定を直した後も戻らない
        // Basis: the S3/R2 codes pointing at the bucket's absence, its name or the key; skipping files or counting boxes would outlive the fix to the setup
        private static readonly string[] BucketWideCodes = { "NoSuchBucket", "InvalidBucketName", "InvalidAccessKeyId", "AllAccessDisabled" };

        public static bool IsBucketWide(string body, out string code)
        {
            if (!R2ErrorBody.TryRead(body, out code, out _)) return false;
            return 0 <= Array.IndexOf(BucketWideCodes, code);
        }
    }
}
