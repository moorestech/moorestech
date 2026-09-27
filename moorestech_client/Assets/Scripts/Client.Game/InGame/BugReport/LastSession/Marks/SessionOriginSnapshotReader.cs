using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.DiskOperations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回セッションが残した出所を、型と所有世代を確認して読み取る
    // Read the previous session's origin while checking types and ownership generation
    internal static class SessionOriginSnapshotReader
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
        private static readonly Regex LedgerFileName = new Regex(@"^ledger-[0-9]+[.]jsonl$", RegexOptions.Compiled);

        internal static SessionOriginSnapshot ReadFrom(string path, out string failureReason)
        {
            failureReason = null;
            if (!File.Exists(path))
            {
                failureReason = $"セッション開始時の出所の印が無い: {path}";
                return null;
            }

            bool remoteExecEnabled;
            string remoteExecLedgerFileName;
            string steamId;
            string steamIdAbsenceReason;
            string kindText;
            BuildInfo buildInfo;
            string buildOriginMissingReason;
            SessionSnapshotCapture snapshotCapture;
            IReadOnlyList<MissingItem> salvageMissing;

            // 前回プロセスの印はディスクIOと外部JSONの境界で読む
            // Read the prior process mark at the disk IO and external JSON boundary
            try
            {
                var obj = JObject.Parse(File.ReadAllText(path));
                steamId = (string)obj["steamId"];
                var enabledToken = obj["remoteExecEnabled"];
                if (enabledToken != null && enabledToken.Type != JTokenType.Boolean)
                {
                    failureReason = $"セッション開始時の出所を読めない {path}: remoteExecEnabledはBooleanである必要があります（型: {enabledToken.Type}）";
                    return null;
                }
                remoteExecEnabled = enabledToken != null && (bool)enabledToken;

                // 旧形式のキー欠損は許すが、新形式の不正な名前や型は理由付きで拒否する
                // Allow a missing legacy key but reject malformed new names and token types with a reason
                var ledgerToken = obj["remoteExecLedgerFileName"];
                remoteExecLedgerFileName = ledgerToken?.Type == JTokenType.String ? (string)ledgerToken : null;
                if (ledgerToken != null && ledgerToken.Type != JTokenType.Null && ledgerToken.Type != JTokenType.String ||
                    remoteExecLedgerFileName != null && !LedgerFileName.IsMatch(remoteExecLedgerFileName) ||
                    !remoteExecEnabled && remoteExecLedgerFileName != null ||
                    remoteExecEnabled && ledgerToken?.Type == JTokenType.Null)
                {
                    failureReason = $"セッション開始時の出所を読めない {path}: remoteExecLedgerFileNameが不正です";
                    return null;
                }

                steamIdAbsenceReason = ReadSteamIdAbsenceReason(steamId, obj["steamIdAbsenceReason"]);
                kindText = (string)obj["buildOriginKind"];
                var buildInfoToken = obj["buildInfo"];
                buildInfo = buildInfoToken == null || buildInfoToken.Type == JTokenType.Null ? null : buildInfoToken.ToObject<BuildInfo>(Serializer);
                buildOriginMissingReason = (string)obj["buildOriginMissingReason"];
                snapshotCapture = SessionSnapshotCapture.Read(obj["snapshotCapture"]);
                salvageMissing = ReadSalvageMissing(obj["salvageMissing"], snapshotCapture.Owner);
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e) || e is JsonException || e is ArgumentException)
            {
                failureReason = $"セッション開始時の出所を読めない {path}: {e.GetBaseException().Message}";
                return null;
            }

            var buildOrigin = ToBuildOrigin(kindText, buildInfo, buildOriginMissingReason, path, out failureReason);
            return buildOrigin == null ? null : new SessionOriginSnapshot(steamId, steamIdAbsenceReason, buildOrigin, remoteExecEnabled, remoteExecLedgerFileName, snapshotCapture, salvageMissing);
        }

        private static string ReadSteamIdAbsenceReason(string steamId, JToken reasonToken)
        {
            if (!string.IsNullOrEmpty(steamId)) return null;
            var reason = reasonToken?.Type == JTokenType.String ? (string)reasonToken : null;
            return string.IsNullOrWhiteSpace(reason) ? SessionOriginSnapshot.LegacyMarkSteamIdAbsenceReason : reason;
        }

        private static IReadOnlyList<MissingItem> ReadSalvageMissing(JToken token, string owner)
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

        private static BuildOriginReading ToBuildOrigin(string kindText, BuildInfo buildInfo, string missingReason, string path, out string failureReason)
        {
            failureReason = null;
            if (!Enum.TryParse<BuildOriginKind>(kindText, out var kind))
            {
                failureReason = $"セッション開始時の出所の種類が読めない value:{kindText} path:{path}";
                return null;
            }

            if (kind == BuildOriginKind.Editor) return BuildOriginReading.Editor();
            if (kind == BuildOriginKind.BuildWithoutInfo) return BuildOriginReading.WithoutInfo(missingReason ?? "前回セッションの開始時点で build-info.json を読めていなかった");
            if (buildInfo != null) return BuildOriginReading.Baked(buildInfo);

            failureReason = $"焼き込み情報つきビルドと記録されているのに buildInfo が無い path:{path}";
            return null;
        }
    }
}
