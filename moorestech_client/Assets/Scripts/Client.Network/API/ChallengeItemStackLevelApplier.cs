using System;
using System.Collections.Generic;
using Core.Item;
using Core.Master;

namespace Client.Network.API
{
    // 同期済みのチャレンジ状態からスタックレベルを導出する（研究と同型・専用同期プロトコルは持たない）
    // Derive stack levels from already-synced challenge state (same shape as research, no dedicated sync protocol)
    public static class ChallengeItemStackLevelApplier
    {
        // ハンドシェイクのチャレンジ応答から適用（サーバーのロード時実行＝完了ClearedActions＋進行中StartedActionsに対応）
        // Apply from handshake challenge responses (mirrors server load: completed ClearedActions + current StartedActions)
        public static void ApplyFromResponses(List<ChallengeCategoryResponse> challenges)
        {
            foreach (var category in challenges)
            {
                foreach (var completed in category.CompletedChallenges) ApplyCleared(completed.ChallengeGuid);
                foreach (var current in category.CurrentChallenges) ApplyStarted(current.ChallengeGuid);
            }
        }

        // 完了チャレンジのClearedActionsを適用
        // Apply the completed challenge's ClearedActions
        public static void ApplyCleared(Guid challengeGuid)
        {
            var challenge = MasterHolder.ChallengeMaster.GetChallenge(challengeGuid);
            ItemStackLevelDataStore.Instance.ApplyUnlockItemStackLevelActions(challenge.ClearedActions.items);
        }

        // 開始チャレンジのStartedActionsを適用
        // Apply the started challenge's StartedActions
        public static void ApplyStarted(Guid challengeGuid)
        {
            var challenge = MasterHolder.ChallengeMaster.GetChallenge(challengeGuid);
            ItemStackLevelDataStore.Instance.ApplyUnlockItemStackLevelActions(challenge.StartedActions.items);
        }
    }
}
