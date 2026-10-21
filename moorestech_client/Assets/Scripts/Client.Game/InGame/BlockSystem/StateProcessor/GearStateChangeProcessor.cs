using System.Collections.Generic;
using Client.Game.InGame.Block;
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

        public void Initialize(BlockGameObject blockGameObject) { }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            _currentGearState = blockState.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
        }

        private void Update()
        {
            if (_currentGearState == null) return;

            Rotate(_currentGearState, Time.deltaTime);
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
