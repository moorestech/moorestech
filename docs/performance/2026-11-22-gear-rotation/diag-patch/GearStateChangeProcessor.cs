using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.GearRotation;
using Game.Gear.Common;
using Server.Event.EventReceive;
using UnityEngine;
using Unity.Profiling;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    [RequireComponent(typeof(GearStateChangeProcessorSimulator))]
    public class GearStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        public IReadOnlyList<RotationInfo> RotationInfos => rotationInfos;
        [SerializeReference, SubclassSelector] private List<RotationInfo> rotationInfos = new();

        private GearStateDetail _currentGearState;

        public void Initialize(BlockGameObject blockGameObject) { }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            _currentGearState = blockState.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
        }

        // 回転は個別のUpdateではなくGearRotationTickerが一覧から進める。有効な間だけ一覧に載る
        // Rotation is advanced by GearRotationTicker from the list rather than per-gear Update; listed only while enabled
        private void OnEnable()
        {
            GearRotationUpdateList.Add(this);
        }

        private void OnDisable()
        {
            GearRotationUpdateList.Remove(this);
        }

        private static readonly ProfilerMarker DiagProc = new(ProfilerCategory.Scripts, "GearDiag.MovingProcessor");
        public void AdvanceRotation(float deltaTime)
        {
            if (_currentGearState != null && _currentGearState.CurrentRpm != 0) { using (DiagProc.Auto()) Rotate(_currentGearState, deltaTime); return; }
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
