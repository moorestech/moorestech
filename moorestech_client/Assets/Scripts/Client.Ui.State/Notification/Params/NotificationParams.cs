using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Notification.Params
{
    /// <summary>
    /// 受信した文字の辞書を型付きの引数へ変える。Guid を運ぶ 2 種の通知だけ、その値を内容名のキーにする（Web の contentParamKeyBuilders）
    /// Turns the received text dictionary into typed arguments; only the two Guid-bearing notifications turn that value into a content-name key (Web contentParamKeyBuilders)
    /// </summary>
    public static class NotificationParams
    {
        internal const string ResearchCompletedId = "achievement.researchCompleted";
        internal const string ChallengeCompletedId = "achievement.challengeCompleted";

        private enum GuidContentKind { None, ResearchName, ChallengeTitle }

        public static IReadOnlyList<NotificationParam> FromWire(string messageId, IReadOnlyDictionary<string, string> wireParams)
        {
            var (guidParamName, guidKind) = GuidParamOf();
            var result = new List<NotificationParam>(wireParams.Count);
            foreach (var pair in wireParams) result.Add(ToParam(pair.Key, pair.Value));

            // 名前が欠けると文言に生の枠が残るので、開発者が気づけるよう警告する（Localize も {name} を残して LogError を出す）
            // A missing value leaves a raw token in the text, so warn for developers (Localize also keeps {name} and logs an error)
            if (guidKind != GuidContentKind.None && !wireParams.ContainsKey(guidParamName)) Debug.LogWarning($"[NotificationParams] {messageId}: missing param {guidParamName}");
            return result;

            #region Internal

            // Guid を運ぶ messageId の引数名と内容名キーの種別を 1 つの switch で対にして返す。運ばない id は None
            // Pairs a Guid-bearing messageId's argument name and content-key kind in one switch; other ids return None
            (string, GuidContentKind) GuidParamOf()
            {
                return messageId switch
                {
                    ResearchCompletedId => ("research", GuidContentKind.ResearchName),
                    ChallengeCompletedId => ("challenge", GuidContentKind.ChallengeTitle),
                    _ => (string.Empty, GuidContentKind.None),
                };
            }

            NotificationParam ToParam(string name, string raw)
            {
                if (guidKind == GuidContentKind.None || name != guidParamName) return new TextNotificationParam(name, raw);
                if (Guid.TryParse(raw, out var guid)) return new ContentNameNotificationParam(name, guidKind == GuidContentKind.ResearchName ? ContentLocalizationKeys.ResearchName(guid) : ContentLocalizationKeys.ChallengeTitle(guid));

                // 読めない値は文字のまま出し、理由を開発者に残す
                // Show an unreadable value as text and leave the reason for developers
                Debug.LogWarning($"[NotificationParams] {messageId}: param {name} is not a guid: {raw}");
                return new TextNotificationParam(name, raw);
            }

            #endregion
        }
    }
}
