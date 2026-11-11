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
        private const string ResearchCompletedId = "achievement.researchCompleted";
        private const string ChallengeCompletedId = "achievement.challengeCompleted";

        public static IReadOnlyList<NotificationParam> FromWire(string messageId, IReadOnlyDictionary<string, string> wireParams)
        {
            var hasGuidParam = TryGetGuidParamName(messageId, out var guidParamName);
            var result = new List<NotificationParam>(wireParams.Count);
            foreach (var pair in wireParams) result.Add(ToParam(pair.Key, pair.Value));

            // 名前が欠けると文言に生の枠が残るので、開発者が気づけるよう警告する（Localize も {name} を残して LogError を出す）
            // A missing value leaves a raw token in the text, so warn for developers (Localize also keeps {name} and logs an error)
            if (hasGuidParam && !wireParams.ContainsKey(guidParamName)) Debug.LogWarning($"[NotificationParams] {messageId}: missing param {guidParamName}");
            return result;

            #region Internal

            NotificationParam ToParam(string name, string raw)
            {
                if (!hasGuidParam || name != guidParamName) return new TextNotificationParam(name, raw);
                if (Guid.TryParse(raw, out var guid)) return new ContentNameNotificationParam(name, ContentKeyOf(messageId, guid));

                // 読めない値は文字のまま出し、理由を開発者に残す
                // Show an unreadable value as text and leave the reason for developers
                Debug.LogWarning($"[NotificationParams] {messageId}: param {name} is not a guid: {raw}");
                return new TextNotificationParam(name, raw);
            }

            #endregion
        }

        private static bool TryGetGuidParamName(string messageId, out string name)
        {
            name = messageId switch
            {
                ResearchCompletedId => "research",
                ChallengeCompletedId => "challenge",
                _ => string.Empty,
            };
            return name.Length > 0;
        }

        private static ContentLocalizationKey ContentKeyOf(string messageId, Guid guid)
        {
            // TryGetGuidParamName が真の messageId だけがここへ来る
            // Only messageIds for which TryGetGuidParamName is true arrive here
            return messageId switch
            {
                ResearchCompletedId => ContentLocalizationKeys.ResearchName(guid),
                ChallengeCompletedId => ContentLocalizationKeys.ChallengeTitle(guid),
                _ => throw new ArgumentOutOfRangeException(nameof(messageId), messageId, null),
            };
        }
    }
}
