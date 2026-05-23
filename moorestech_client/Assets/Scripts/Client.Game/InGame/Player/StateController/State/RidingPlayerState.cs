using Client.Game.InGame.Train.View.Object;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Player.StateController.State
{
    // 乗車中の Player ステート。Tick で context (RidingPlayerStateContext) のプロパティを参照して
    // 現在乗るべき列車を取得し、車両 entity が揃ったタイミングで親付け＋カメラ追従を設定する。
    // OnExit で降車 pose を適用する。
    // Riding player state. Reads the current ride target from the context (RidingPlayerStateContext) every frame
    // and applies parenting + camera follow when the car entity is ready. OnExit warps to the dismount pose.
    public class RidingPlayerState : IPlayerState
    {
        private static readonly Quaternion RidingLocalRotation = Quaternion.identity;

        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;
        // 列車ステート (TrainHUDScreenState) が所有する context の参照。
        // Reference to the context owned by the train state (TrainHUDScreenState).
        private RidingPlayerStateContext _rideContext;
        // 実際に親付けが完了している車両 id。null の間は未 parent（RPC 応答待ち or 車両未生成）。
        // The car id we have actually parented onto; null while unparented (waiting for RPC or car spawn).
        private TrainCarInstanceId? _mountedTrainCarInstanceId;
        private Vector3 _mountedSeatLocalPosition;

        public RidingPlayerState(TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _trainCarObjectDatastore = trainCarObjectDatastore;
        }

        public void OnEnter(IPlayerStateContext context)
        {
            // context は Riding 遷移時に必ず RidingPlayerStateContext で渡される契約（呼び出し側 = TrainHUDScreenState）。
            // The caller (TrainHUDScreenState) is contracted to pass a RidingPlayerStateContext when entering Riding.
            _rideContext = context as RidingPlayerStateContext;
            _mountedTrainCarInstanceId = null;
        }

        public void Tick()
        {
            if (_rideContext == null) return;

            // context から「現在乗るべき列車」を取得。null / -1 のうちは RPC 応答待ち等で未確定。
            // Pull the current ride target from the context; null / -1 means RPC reply pending etc.
            if (!_rideContext.CurrentCarId.HasValue) return;
            var targetId = _rideContext.CurrentCarId.Value;
            var seatIndex = _rideContext.CurrentSeatIndex;
            if (seatIndex < 0) return;

            // 車両 entity が未生成 or 破棄済みのとき: 既に mount 済みなら dangling target を解除する。
            // When the car entity is missing (not yet spawned or already destroyed), clear any dangling mount.
            if (!_trainCarObjectDatastore.TryGetEntity(targetId, out var entity))
            {
                ClearDanglingMountIfNeeded();
                return;
            }

            // 初回 or 異なる車両へ移ったタイミングで parent し直す。
            // Parent on first attach or when the target car changes.
            if (!_mountedTrainCarInstanceId.HasValue || _mountedTrainCarInstanceId.Value != targetId)
            {
                MountToCar(targetId, entity, seatIndex);
            }

            // 毎フレーム ride follow を再適用する（既存挙動踏襲・安全策）。
            // Re-apply ride follow every frame (mirrors prior behavior as a safety net).
            var playerObjectController = ResolvePlayerObjectController();
            if (playerObjectController != null)
            {
                playerObjectController.SetRideFollowTarget(entity.transform, _mountedSeatLocalPosition, RidingLocalRotation);
            }

            #region Internal

            // 車両 entity がまだロードされていない or 破棄済みの場合、過去 mount の follow を解除して dangling を防ぐ。
            // If the car entity isn't loaded yet or has been destroyed, release any prior follow to avoid dangling references.
            void ClearDanglingMountIfNeeded()
            {
                if (!_mountedTrainCarInstanceId.HasValue) return;

                var poc = ResolvePlayerObjectController();
                if (poc != null) poc.ClearRideFollowTarget();
                _mountedTrainCarInstanceId = null;
            }

            void MountToCar(TrainCarInstanceId targetCarId, TrainCarEntityObject targetEntity, int targetSeatIndex)
            {
                var playerObjectController = ResolvePlayerObjectController();
                if (playerObjectController == null) return;

                // 座席マスタのオフセットを解決し、車両 entity 相対で着席位置を決める（仕様書セクション9）。
                // Resolve the seat offset from master and seat the player relative to the car entity.
                _mountedSeatLocalPosition = ResolveSeatLocalPosition(targetEntity, targetSeatIndex);

                var playerTransform = playerObjectController.transform;
                playerTransform.SetParent(null, true);
                playerObjectController.SetRideFollowTarget(targetEntity.transform, _mountedSeatLocalPosition, RidingLocalRotation);
                playerObjectController.SetControllable(false);
                _mountedTrainCarInstanceId = targetCarId;
            }

            // 座席マスタ（ridableSeats）の seatIndex 番目のオフセットを返す。範囲外なら原点。
            // Returns the offset of seat seatIndex from the seat master; falls back to origin when out of range.
            static Vector3 ResolveSeatLocalPosition(TrainCarEntityObject targetEntity, int targetSeatIndex)
            {
                var seats = targetEntity.TrainCarMasterElement.RidableSeats;
                if (seats == null || targetSeatIndex < 0 || seats.Length <= targetSeatIndex)
                {
                    return Vector3.zero;
                }

                var seat = seats[targetSeatIndex];
                return new Vector3((float)seat.OffsetX, (float)seat.OffsetY, (float)seat.OffsetZ);
            }

            #endregion
        }

        public void OnExit()
        {
            var playerObjectController = ResolvePlayerObjectController();
            if (playerObjectController != null)
            {
                playerObjectController.ClearRideFollowTarget();
                if (_mountedTrainCarInstanceId.HasValue)
                {
                    var playerTransform = playerObjectController.transform;
                    var (dismountPosition, dismountRotation) = ResolveDismountPose(playerTransform);

                    playerTransform.SetParent(null, true);
                    playerObjectController.SetPlayerPosition(dismountPosition);
                    playerTransform.rotation = dismountRotation;
                }

                playerObjectController.SetControllable(true);
            }

            _mountedTrainCarInstanceId = null;
            _rideContext = null;

            #region Internal

            // 車両 Prefab の TrainCarDismountPoint が指す Transform に位置・回転ごとワープする。
            // 車両 entity が既に破棄されている場合は player 現在 pose をそのまま返す。
            // Warps to the Transform pointed to by TrainCarDismountPoint on the car Prefab.
            // Returns the player's current pose if the entity is already destroyed.
            (Vector3 position, Quaternion rotation) ResolveDismountPose(Transform playerTransform)
            {
                if (_mountedTrainCarInstanceId.HasValue
                    && _trainCarObjectDatastore.TryGetEntity(_mountedTrainCarInstanceId.Value, out var entity)
                    && entity != null)
                {
                    var marker = entity.GetComponentInChildren<TrainCarDismountPoint>(true);
                    if (marker != null && marker.Point != null)
                    {
                        return (marker.Point.position, marker.Point.rotation);
                    }

                    return (entity.transform.position, entity.transform.rotation);
                }

                return (playerTransform.position, playerTransform.rotation);
            }

            #endregion
        }

        private static PlayerObjectController ResolvePlayerObjectController()
        {
            return PlayerSystemContainer.Instance?.PlayerObjectController as PlayerObjectController;
        }
    }
}
