using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.GearRotation;
using Client.Game.InGame.Block.GearRotation.Gpu;
using Game.Gear.Common;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [RequireComponent(typeof(GearStateChangeProcessorSimulator))]
    public class GearStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public IReadOnlyList<RotationInfo> RotationInfos => rotationInfos;
        [SerializeReference, SubclassSelector] private List<RotationInfo> rotationInfos = new();

        private GearStateDetail _currentGearState;
        // 表の行のキーにしたブロック原点。行を書いたときだけ値を持つ
        // Block origin used as the table key; set only once a row has been written
        private Vector3? _gpuBlockOrigin;

        public void Initialize(BlockGameObject blockGameObject) { }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            _currentGearState = blockState.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);

            // シェーダーで回す部品は、ブロック原点をキーにした表へ回転状態を書く
            // Shader-spun parts get their rotation state written to the table keyed by block origin
            // 歯車の情報が無い状態はCPU回転と同じく止める(RPM 0)
            // A state without gear details stops the spin (RPM 0), as the CPU rotation does
            if (HasRotationInfo(true))
            {
                _gpuBlockOrigin ??= GetComponentInParent<BlockGameObject>(true).transform.position;
                var rpm = _currentGearState?.CurrentRpm ?? 0f;
                var isClockwise = _currentGearState?.IsClockwise ?? true;
                GearRotationGpuTable.SetState(_gpuBlockOrigin.Value, rpm, isClockwise, Time.time);
            }
        }

        // CPUで回す部品だけGearRotationTickerが一覧から進める。有効な間だけ一覧に載る
        // GearRotationTicker advances only CPU-spun parts from the list; listed only while enabled
        private void OnEnable()
        {
            if (HasRotationInfo(false)) GearRotationUpdateList.Add(this);
        }

        private void OnDisable()
        {
            GearRotationUpdateList.Remove(this);
        }

        private void OnDestroy()
        {
            if (_gpuBlockOrigin.HasValue) GearRotationGpuTable.Remove(_gpuBlockOrigin.Value);
        }

        private bool HasRotationInfo(bool gpuDriven)
        {
            foreach (var rotationInfo in rotationInfos)
            {
                if (rotationInfo != null && rotationInfo.IsGpuDriven == gpuDriven) return true;
            }
            return false;
        }

        public void AdvanceRotation(float deltaTime)
        {
            if (_currentGearState == null) return; // silent-ok: 状態の受信前は回転させない / 状態の受信後に回り出すことで確認する

            Rotate(_currentGearState, deltaTime);
        }

        public void SetRotationWithinRange(bool withinRange)
        {
            // 停止中の最新RPMを進行再開より先に反映する
            // Apply the latest received RPM before resuming progression
            if (withinRange && _currentGearState != null) Rotate(_currentGearState, 0f);
            foreach (var rotationInfo in rotationInfos)
            {
                if (rotationInfo == null) continue; // silent-ok: 未設定の回転枠は対象なし / Prefabの回転リストで確認する
                rotationInfo.SetRotationEnabled(withinRange);
            }
            enabled = withinRange;
        }

        public void Rotate(GearStateDetail gearStateDetail, float deltaTime)
        {
            foreach (var rotationInfo in rotationInfos)
            {
                if (rotationInfo == null) continue;
                rotationInfo.Rotate(gearStateDetail, deltaTime);
            }
        }

#if UNITY_EDITOR
        public GearStateDetail DebugCurrentGearState => _currentGearState;
#endif
    }
}
