using Client.Game.InGame.Block;
using Game.Block.Interface.State;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.SpaceElevator
{
    // 接続と昇降の進みを簡易モデルへ反映する
    // Reflect connection and climb progress in the placeholder model
    public class SpaceElevatorBaseStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private GameObject cable;
        [SerializeField] private Transform climber;
        [SerializeField] private Vector3 climberBottomLocalPosition;
        [SerializeField] private Vector3 climberTopLocalPosition;

        public void Initialize(BlockGameObject blockGameObject)
        {
            // 初期状態の受信までは未接続として描く
            // Draw as unconnected until the initial state arrives
            cable.SetActive(false);
            climber.gameObject.SetActive(false);
        }

        public void OnChangeState(BlockStateMessagePack blockState)
        {
            var detail = blockState.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);

            // 必須の状態詳細が欠けたら不整合を通知する
            // Report a mismatch when the required state detail is missing
            if (detail == null)
            {
                Debug.LogError("[SpaceElevatorBaseStateChangeProcessor] SpaceElevatorBase state detail is missing");
                return;
            }

            // 接続で表示を切り替え、進みに合わせて昇る
            // Toggle visibility with the connection and climb with the progress
            cable.SetActive(detail.IsConnected);
            climber.gameObject.SetActive(detail.IsConnected);
            climber.localPosition = Vector3.Lerp(climberBottomLocalPosition, climberTopLocalPosition, detail.ClimbProgress);
        }
    }
}
