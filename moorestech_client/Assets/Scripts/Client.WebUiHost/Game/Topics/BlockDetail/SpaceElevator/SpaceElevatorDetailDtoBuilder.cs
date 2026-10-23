using Client.Game.InGame.Block;
using Client.WebUiHost.Game.Topics;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 宇宙エレベーター拠点の状態詳細をDTOへ詰める
    /// Fills the space elevator base's state detail into its capability DTO
    /// </summary>
    public static class SpaceElevatorDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            // silent-ok: 他種別の詳細は対象のビルダーが処理する / 各専用ビルダーが担当する
            if (param is not SpaceElevatorBaseBlockParam elevatorParam) return;
            var state = block.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);
            // 初回の状態が届く前は未着として何も詰めない（webui は詳細なしで描く）
            // Before the first state arrives nothing is filled; the webui renders without the detail
            if (state == null)
            {
                Debug.LogWarning("SpaceElevatorBase の状態詳細が未着のため DTO を保留します");
                return;
            }

            dto.SpaceElevator = new SpaceElevatorDetailDto
            {
                Connected = state.IsConnected,
                CablePoolAmount = state.CablePoolAmount,
                // 1エレベーター分の量はマスタ値なので param から引く
                // The cables per elevator is a master value, so read it from the param
                CablesPerElevator = elevatorParam.CablesPerElevator,
                ClimbProgress = state.ClimbProgress,
                Status = ToWireStatus(state.Status),
            };
            #region Internal
            string ToWireStatus(SpaceElevatorBaseStatus status)
            {
                return status switch
                {
                    SpaceElevatorBaseStatus.ReadyToConnect => "readyToConnect",
                    SpaceElevatorBaseStatus.InsufficientCable => "insufficientCable",
                    SpaceElevatorBaseStatus.WaitingSatellite => "waitingSatellite",
                    SpaceElevatorBaseStatus.Climbing => "climbing",
                    SpaceElevatorBaseStatus.LowPower => "lowPower",
                    SpaceElevatorBaseStatus.OutputBlocked => "outputBlocked",
                    SpaceElevatorBaseStatus.InletBlocked => "inletBlocked",
                    _ => throw new System.ArgumentOutOfRangeException(nameof(status), status, "未対応の拠点状態"),
                };
            }
            #endregion
        }


    }
}
