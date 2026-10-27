using Client.Game.InGame.Block;
using Client.WebUiHost.Game.Topics;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    public static class SpaceElevatorDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            // silent-ok: 他種別の詳細は対象のビルダーが処理する / 各専用ビルダーが担当する
            if (param is not SpaceElevatorBaseBlockParam elevatorParam) return;
            var state = block.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);
            // silent-ok: 初回の状態を受け取る前だけ詳細を省き、受信後の再配信で埋まる / ケーブル表示と接続ボタンが出ないことでパネル上で気づく
            if (state == null) return;

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
