using Game.Context;
using Game.SaveLoad.Interface;
using UniRx;

namespace Server.Event.Notification
{
    /// <summary>
    /// マスタ欠損で除去した件数を、接続したプレイヤーへ1回だけ知らせる
    /// Tells each connecting player once how much was removed for missing master data
    /// 除去はワールドのロード時に終わっており誰も接続していないので、broadcastではなく接続時pushで届ける
    /// Pruning finishes at world load while nobody is connected, so this pushes on connect instead of broadcasting
    /// </summary>
    public class MissingMasterPruneNotificationWiring : IBootInitializable
    {
        private readonly NotificationService _notificationService;

        // NotificationServiceはproviderをprivateに抱えるため、購読元として同じproviderを別に受ける
        // NotificationService keeps its provider private, so the same provider is injected separately to subscribe on
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IMissingMasterPruneReportLookup _reportLookup;

        public MissingMasterPruneNotificationWiring(NotificationService notificationService, EventProtocolProvider eventProtocolProvider, IMissingMasterPruneReportLookup reportLookup)
        {
            _notificationService = notificationService;
            _eventProtocolProvider = eventProtocolProvider;
            _reportLookup = reportLookup;
        }

        public void Load()
        {
            // sink登録直後の同期push契約に乗る（前例: TrainFullSnapshotEventPacket）
            // Rides the synchronous push contract right after sink registration (precedent: TrainFullSnapshotEventPacket)
            _eventProtocolProvider.OnPlayerEventStreamRegistered.Subscribe(NotifyIfPruned);
        }

        private void NotifyIfPruned(int playerId)
        {
            var report = _reportLookup.Report;
            if (!report.HasRemoval)
            {
                // 除去0件は本番の常態。理由を残しておかないと「通知が来ない」の切り分けができない
                // Zero removals is the normal case; without this line a missing notice cannot be diagnosed
                UnityEngine.Debug.Log($"マスタ欠損の除去が無いためplayerId={playerId}への通知は出しません。");
                return;
            }

            // 再接続のたびに出し直す。クールダウンで握り潰すと接続直後の1回が消える
            // Re-sent on every reconnect; the cooldown would swallow the single post-connect notice
            _notificationService.NotifyWithoutCooldown(playerId,
                NotificationMessagePack.CreateSaveMigrationPruned(report.RemovedBlockCount, report.EmptiedItemStackCount, report.RemovedResearchCount));
        }
    }
}
