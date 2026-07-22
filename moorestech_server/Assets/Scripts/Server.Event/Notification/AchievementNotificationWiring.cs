using System;
using Game.Challenge;
using Game.Context;
using Game.Research;
using Game.UnlockState;
using UniRx;

namespace Server.Event.Notification
{
    /// <summary>
    /// ドメインの達成イベントを購読し通知基盤へ配線する
    /// Subscribes to domain achievement events and wires them into the notification service
    /// </summary>
    public class AchievementNotificationWiring : IBootInitializable
    {
        private readonly NotificationService _notificationService;
        private readonly ResearchEvent _researchEvent;
        private readonly ChallengeEvent _challengeEvent;
        private readonly IGameUnlockStateDataController _unlockState;

        public AchievementNotificationWiring(NotificationService notificationService, ResearchEvent researchEvent, ChallengeEvent challengeEvent, IGameUnlockStateDataController unlockState)
        {
            _notificationService = notificationService;
            _researchEvent = researchEvent;
            _challengeEvent = challengeEvent;
            _unlockState = unlockState;
        }

        public void Load()
        {
            // 研究完了は完了プレイヤー宛て、チャレンジ・アンロックは全員宛て
            // Research completion targets the completing player; challenge/unlock broadcast to all
            _researchEvent.OnResearchCompleted.Subscribe(data => _notificationService.Notify(data.playerId,
                NotificationMessagePack.CreateAchievement("achievement.researchCompleted", new[] { data.researchNode.ResearchNodeName })));

            _challengeEvent.OnCompleteChallenge.Subscribe(property => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.challengeCompleted", new[] { property.ChallengeTask.ChallengeMasterElement.Title })));

            // ChallengeCategoryのアンロックはチャレンジ達成通知と重複するため配線しない（v1判断）
            // Challenge category unlock is skipped to avoid duplicating the challenge-completed notification (v1 decision)
            _unlockState.OnUnlockItem.Subscribe(itemId => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievementWithItem("achievement.unlockedItem", Array.Empty<string>(), itemId)));
            _unlockState.OnUnlockCraftRecipe.Subscribe(_ => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.unlockedCraftRecipe", Array.Empty<string>())));
            _unlockState.OnUnlockMachineRecipe.Subscribe(_ => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.unlockedMachineRecipe", Array.Empty<string>())));
            _unlockState.OnUnlockBlock.Subscribe(_ => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.unlockedBlock", Array.Empty<string>())));
            _unlockState.OnUnlockTrainCar.Subscribe(_ => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.unlockedTrainCar", Array.Empty<string>())));
            _unlockState.OnUnlockConnectTool.Subscribe(_ => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.unlockedConnectTool", Array.Empty<string>())));
        }
    }
}
