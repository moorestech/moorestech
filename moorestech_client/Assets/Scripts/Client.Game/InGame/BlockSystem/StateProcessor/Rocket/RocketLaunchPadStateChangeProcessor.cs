using Client.Game.InGame.Block;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.Rocket
{
    // 建造完了で本体を表示し、打ち上げ中は加速して上昇させる
    // Show the completed rocket body and accelerate its ascent during launch
    public class RocketLaunchPadStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private Transform body;
        [SerializeField] private float riseHeight = 80f;

        private Vector3 _bodyRestLocalPosition;
        private float _launchSeconds;
        private float _launchElapsedSeconds;
        private bool _isLaunching;

        public void Initialize(BlockGameObject blockGameObject)
        {
            // 初期位置とマスタの打ち上げ時間を保持する
            // Cache the rest position and launch duration from the master
            _bodyRestLocalPosition = body.localPosition;
            _launchSeconds = ((RocketLaunchPadBlockParam)blockGameObject.BlockMasterElement.BlockParam).LaunchSeconds;
            body.gameObject.SetActive(false);
        }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            var detail = blockState.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey);

            // 建造完了から発射終了まで本体を表示
            // Show the body from build completion through launch end
            body.gameObject.SetActive(detail.Phase is RocketLaunchPadPhase.AwaitingLaunchCommand or RocketLaunchPadPhase.ReadyToLaunch or RocketLaunchPadPhase.Launching);

            // 途中参加でもサーバーの進行位置から上昇を始める
            // Start the ascent at server progress even when joining mid-launch
            var wasLaunching = _isLaunching;
            _isLaunching = detail.Phase == RocketLaunchPadPhase.Launching;
            if (_isLaunching && !wasLaunching) _launchElapsedSeconds = detail.Progress * _launchSeconds;
            if (_isLaunching) UpdateBodyPosition();
            else body.localPosition = _bodyRestLocalPosition;
        }

        private void Update()
        {
            if (!_isLaunching) return; // silent-ok: 待機中は上昇しない / 表示と位置は状態通知で更新する

            // クライアントのフレーム間を滑らかにつなぐ
            // Smoothly advance the visual between client frames
            _launchElapsedSeconds = Mathf.Min(_launchElapsedSeconds + Time.deltaTime, _launchSeconds);
            UpdateBodyPosition();
        }

        private void UpdateBodyPosition()
        {
            // モデルの縮尺に依存せずワールド上方へ加速上昇する
            // Accelerate upwards in world space independently of the model scale
            var ratio = _launchElapsedSeconds / _launchSeconds;
            body.position = body.parent.TransformPoint(_bodyRestLocalPosition) + Vector3.up * (riseHeight * ratio * ratio);
        }
    }
}
