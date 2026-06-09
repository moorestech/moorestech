using Client.Game.InGame.Riding;
using UnityEngine;

namespace Client.Game.InGame.Player.StateController.State
{
    // 乗車中のプレイヤー追従を管理するステート
    // State that manages player follow while riding
    public class RidingPlayerState : IPlayerState
    {
        private static readonly Quaternion RidingLocalRotation = Quaternion.identity;

        private readonly IRideFollowTargetResolver _rideFollowTargetResolver;

        private RidingPlayerStateContext _rideContext;
        private bool _isFollowTargetApplied;

        public RidingPlayerState(IRideFollowTargetResolver rideFollowTargetResolver)
        {
            // 乗車対象の具象解決は resolver に任せ、この state は train/car を知らない
            // Delegate concrete ridable resolution to the resolver so this state does not know train/car details
            _rideFollowTargetResolver = rideFollowTargetResolver;
        }

        public void OnEnter(IPlayerStateContext context)
        {
            _rideContext = context as RidingPlayerStateContext;
            _isFollowTargetApplied = false;
            TryApplyRidingContext();
        }

        public void Tick()
        {
            if (_rideContext == null)
            {
                return;
            }

            // target object がまだ未生成なら、生成されるまで追従適用を再試行する
            // Retry applying follow until the target object is created
            if (!_isFollowTargetApplied)
            {
                TryApplyRidingContext();
                return;
            }

            // 適用済み target が消えたら追従を解除する
            // Clear follow when the already-applied target disappears
            if (!_rideFollowTargetResolver.Exists(_rideContext))
            {
                var player = ResolvePlayerObjectController();
                player.ClearRideFollowTarget();
                _rideContext = null;
                _isFollowTargetApplied = false;
            }
        }

        public void OnExit()
        {
            var player = ResolvePlayerObjectController();
            var dismountPosition = player.Position;
            if (_rideContext != null)
            {
                // target がまだ存在していれば resolver から降車位置を取得する
                // Resolve the dismount position from the resolver when the target still exists
                dismountPosition = _rideFollowTargetResolver.ResolveDismountPosition(_rideContext, player.Position);
            }

            player.ClearRideFollowTarget();
            player.SetPlayerPosition(dismountPosition);
            _rideContext = null;
            _isFollowTargetApplied = false;
        }

        private void TryApplyRidingContext()
        {
            if (_rideContext == null)
            {
                return;
            }

            // resolver が follow target を解決できたフレームで追従を開始する
            // Start following on the frame where the resolver can resolve the follow target
            if (!_rideFollowTargetResolver.TryResolveFollowTarget(_rideContext, out var followTarget))
            {
                return;
            }

            var player = ResolvePlayerObjectController();
            player.SetRideFollowTarget(followTarget, Vector3.zero, RidingLocalRotation);
            _isFollowTargetApplied = true;
        }

        // TODOこのキャストをやめて必要なメソッドをIPlayerObjectControllerに入れる
        private static PlayerObjectController ResolvePlayerObjectController()
        {
            return PlayerSystemContainer.Instance?.PlayerObjectController as PlayerObjectController;
        }
    }
}
