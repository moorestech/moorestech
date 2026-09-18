using System;
using Client.PlaytestReceiver.Http.Responses;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload.Failure
{
    // 署名付きPUTの403の原因。期限切れと読めない本文は一過性、それ以外は署名し直しても直らない
    // The cause of a presigned PUT's 403; an expiry and an unreadable body are transient, anything else never heals by re-signing
    internal enum PlaytestSignedPutForbiddenCause
    {
        ExpiredSignature,
        UnrecognizedBody,
        Permanent,
    }

    // 署名付きPUTの403の読み分け。期限切れだけはprepareで署名し直せば直り、それ以外（署名不一致・鍵の失効等）は何度送っても直らない
    // Reads a 403 from the presigned PUT; only an expiry heals by re-signing through prepare, the rest (signature mismatch, revoked key) never heals
    internal static class PlaytestSignedPutForbidden
    {
        // 根拠: S3は期限切れの署名付きURLに 403 AccessDenied「Request has expired」を返す。R2の実際の Code は未確認のため、
        // 期限切れを名乗る Code（RequestExpired / ExpiredRequest）も同じ扱いにする。S3のXMLとして読めない403（CDN・プロキシ由来など）は
        // 恒久失敗と断定する根拠が無いので、恒久側へ倒さず一過性に残す（表が尽きれば数えずに持ち越すだけ）
        // Basis: S3 answers an expired presigned URL with 403 AccessDenied "Request has expired". R2's actual Code is unconfirmed,
        // so codes that name an expiry (RequestExpired / ExpiredRequest) are treated the same. A 403 that is not S3 XML (from a CDN or proxy)
        // gives no ground to call it permanent, so it stays transient rather than tipping to permanent (it only defers uncounted when the schedule runs out)
        public static PlaytestSignedPutForbiddenCause Classify(string body)
        {
            if (!R2ErrorBody.TryRead(body, out var code, out var message))
            {
                Debug.LogWarning("[PlaytestReceiver] the presigned PUT answered 403 without an S3 error code; treating it as transient");
                return PlaytestSignedPutForbiddenCause.UnrecognizedBody;
            }
            if (code == "RequestExpired" || code == "ExpiredRequest") return PlaytestSignedPutForbiddenCause.ExpiredSignature;
            if (code == "AccessDenied" && 0 <= message.IndexOf("expired", StringComparison.OrdinalIgnoreCase)) return PlaytestSignedPutForbiddenCause.ExpiredSignature;
            return PlaytestSignedPutForbiddenCause.Permanent;
        }
    }
}
