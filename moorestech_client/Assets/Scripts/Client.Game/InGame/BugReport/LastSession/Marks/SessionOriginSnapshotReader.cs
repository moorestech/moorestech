using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.DiskOperations;
using Client.RemoteExec.Access;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回セッションが残した出所を、型と所有世代を確認して読み取る
    // Read the previous session's origin while checking types and ownership generation
    internal static class SessionOriginSnapshotReader
    {
        // 読めなければnullと理由を返す。無音で今回のビルドへ差し替えると、別ビルドのクラッシュとして再現される
        // Returns null with a reason when unreadable; silently substituting this boot's build would reproduce the crash on a different build
        internal static SessionOriginSnapshot ReadFrom(string path, out string failureReason)
        {
            failureReason = null;
            if (!File.Exists(path))
            {
                failureReason = $"セッション開始時の出所の印が無い: {path}";
                return null;
            }

            string remoteExecLedgerFileName;
            bool remoteExecAttempted;
            bool remoteExecLedgerWriteFailed;
            string steamId;
            string steamIdAbsenceReason;
            string kindText;
            BuildInfo buildInfo;
            string buildOriginMissingReason;
            SessionSnapshotCapture snapshotCapture;
            IReadOnlyList<MissingItem> salvageMissing;

            // 読み込みはディスクIO、パースは外部JSON境界。途中で落ちたセッションは切れたJSONを残しうる
            // Reading is disk IO and parsing is external JSON; an interrupted session can leave truncated JSON
            try
            {
                var obj = JObject.Parse(File.ReadAllText(path));
                steamId = (string)obj["steamId"];

                // 有効判定は台帳ファイル名の有無だけから導く。キーが無いかnullなら無効、文字列なら形式検証する
                // Derive the enabled state solely from the ledger file name; missing or null means disabled, a string is format-checked
                var ledgerToken = obj["remoteExecLedgerFileName"];
                if (ledgerToken == null || ledgerToken.Type == JTokenType.Null)
                {
                    remoteExecLedgerFileName = null;
                }
                else if (ledgerToken.Type == JTokenType.String && RemoteExecLedger.IsLedgerFileName((string)ledgerToken))
                {
                    remoteExecLedgerFileName = (string)ledgerToken;
                }
                else
                {
                    failureReason = $"セッション開始時の出所を読めない {path}: remoteExecLedgerFileNameが不正です";
                    return null;
                }

                // 試行印は台帳と別の場所に先行保存される。退避済み出所ではJSON値を引き継ぐ
                // The attempt signal is written first outside the ledger; salvaged origins carry its JSON value
                var attemptedToken = obj["remoteExecAttempted"];
                if (attemptedToken != null && attemptedToken.Type != JTokenType.Boolean)
                {
                    failureReason = $"セッション開始時の出所を読めない {path}: remoteExecAttemptedが不正です";
                    return null;
                }
                remoteExecAttempted = (attemptedToken != null && (bool)attemptedToken) ||
                    File.Exists(Path.Combine(Path.GetDirectoryName(path), RemoteExecLedger.AttemptSignalFileName));
                var failedToken = obj["remoteExecLedgerWriteFailed"];
                if (failedToken != null && failedToken.Type != JTokenType.Boolean)
                {
                    failureReason = $"セッション開始時の出所を読めない {path}: remoteExecLedgerWriteFailedが不正です";
                    return null;
                }
                remoteExecLedgerWriteFailed = (failedToken != null && (bool)failedToken) ||
                    File.Exists(Path.Combine(Path.GetDirectoryName(path), RemoteExecLedger.FailureSignalFileName));

                steamIdAbsenceReason = ReadSteamIdAbsenceReason(steamId, obj["steamIdAbsenceReason"]);
                kindText = (string)obj["buildOriginKind"];
                var buildInfoToken = obj["buildInfo"];
                buildInfo = buildInfoToken == null || buildInfoToken.Type == JTokenType.Null ? null : buildInfoToken.ToObject<BuildInfo>(SessionOriginSnapshot.CreateSerializer());
                buildOriginMissingReason = (string)obj["buildOriginMissingReason"];
                snapshotCapture = SessionSnapshotCapture.Read(obj["snapshotCapture"]);
                salvageMissing = ReadSalvageMissing(obj["salvageMissing"], snapshotCapture.Owner);
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e) || e is JsonException || e is ArgumentException)
            {
                failureReason = $"セッション開始時の出所を読めない {path}: {e.GetBaseException().Message}";
                return null;
            }

            var buildOrigin = ToBuildOrigin(kindText, buildInfo, buildOriginMissingReason, out failureReason);
            if (buildOrigin == null) return null;
            var origin = new SessionOriginSnapshot(steamId, steamIdAbsenceReason, buildOrigin, remoteExecLedgerFileName, snapshotCapture, salvageMissing);
            origin.SetRemoteExecAttempted(remoteExecAttempted);
            origin.SetRemoteExecLedgerWriteFailed(remoteExecLedgerWriteFailed);
            return origin;

            #region Internal

            string ReadSteamIdAbsenceReason(string forSteamId, JToken reasonToken)
            {
                // 旧形式の印でSteamIDも理由も無いときは、理由欠落を明示した理由で埋める
                // A legacy mark lacking both SteamID and reason explicitly reports the missing reason
                if (!string.IsNullOrEmpty(forSteamId)) return null;
                var reason = reasonToken?.Type == JTokenType.String ? (string)reasonToken : null;
                return string.IsNullOrWhiteSpace(reason) ? SessionOriginSnapshot.LegacyMarkSteamIdAbsenceReason : reason;
            }

            IReadOnlyList<MissingItem> ReadSalvageMissing(JToken token, string owner)
            {
                // 旧形式や世代不一致を「欠損なし」にしない
                // Never interpret legacy or mismatched generations as having no missing evidence
                var unknown = new List<MissingItem> { new MissingItem { Item = "previousOrigin", Reason = "退避欠損の履歴が不明（旧形式・不正形式・所有世代不一致）" } };
                if (!(token is JObject ledger) || !JToken.DeepEquals(ledger["version"], new JValue(1)) ||
                    !JToken.DeepEquals(ledger["owner"], owner == null ? JValue.CreateNull() : new JValue(owner)) || !(ledger["items"] is JArray items)) return unknown;

                var result = new List<MissingItem>();
                foreach (var item in items)
                {
                    if (!(item is JObject entry) || entry["item"]?.Type != JTokenType.String || entry["reason"]?.Type != JTokenType.String ||
                        string.IsNullOrWhiteSpace((string)entry["item"]) || string.IsNullOrWhiteSpace((string)entry["reason"])) return unknown;
                    result.Add(new MissingItem { Item = (string)entry["item"], Reason = (string)entry["reason"] });
                }
                return result;
            }

            BuildOriginReading ToBuildOrigin(string forKindText, BuildInfo forBuildInfo, string missingReason, out string buildOriginFailureReason)
            {
                buildOriginFailureReason = null;
                if (!Enum.TryParse<BuildOriginKind>(forKindText, out var kind))
                {
                    buildOriginFailureReason = $"セッション開始時の出所の種類が読めない value:{forKindText} path:{path}";
                    return null;
                }

                if (kind == BuildOriginKind.Editor) return BuildOriginReading.Editor();
                if (kind == BuildOriginKind.BuildWithoutInfo) return BuildOriginReading.WithoutInfo(missingReason ?? "前回セッションの開始時点で build-info.json を読めていなかった");
                if (forBuildInfo != null) return BuildOriginReading.Baked(forBuildInfo);

                buildOriginFailureReason = $"焼き込み情報つきビルドと記録されているのに buildInfo が無い path:{path}";
                return null;
            }

            #endregion
        }
    }
}
