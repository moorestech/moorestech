using System;
using System.Linq;
using Client.Game.InGame.Block;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 発射台の状態とスロット群の絶対添字をDTOへ変換する
    /// Converts the launch pad state and the absolute indices of its slot groups into the DTO
    /// </summary>
    public static class RocketLaunchPadDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block)
        {
            // silent-ok: 他種別の詳細は各builderが担当する / 発射台以外の通常パネルで確認できる
            if (block.BlockMasterElement.BlockParam is not RocketLaunchPadBlockParam param) return;
            // silent-ok: 在庫取得失敗時は詳細を構築できない / dto.Errorを共通パネルが表示する
            if (dto.Error != null) return;
            var state = block.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey);
            // silent-ok: 初回の状態を受け取る前だけ詳細を省き、受信後の再配信で埋まる / ボタンと段階表示が出ないことでパネル上で気づく
            if (state == null) return;

            var layout = new RocketLaunchPadSlotLayout(param);

            // スロット群はサーバーと同じ並びの型が計算した添字をそのまま載せる
            // Carry the indices computed by the layout type the server also uses
            dto.RocketLaunchPad = new RocketLaunchPadDetailDto
            {
                Phase = ToWirePhase(state.Phase),
                WaitReason = ToWireWaitReason(state.WaitReason),
                Progress = state.Progress,
                MaterialSlots = layout.MaterialSlots.ToList(),
                PayloadSlots = layout.PayloadSlots.ToList(),
                OutputSlots = Enumerable.Range(layout.OutputSlotStart, dto.ItemSlots.Count - layout.OutputSlotStart).ToList(),
            };

            #region Internal

            string ToWirePhase(RocketLaunchPadPhase phase)
            {
                return phase switch
                {
                    RocketLaunchPadPhase.Idle => "idle",
                    RocketLaunchPadPhase.AwaitingBuildCommand => "awaitingBuildCommand",
                    RocketLaunchPadPhase.Building => "building",
                    RocketLaunchPadPhase.AwaitingLaunchCommand => "awaitingLaunchCommand",
                    RocketLaunchPadPhase.ReadyToLaunch => "readyToLaunch",
                    RocketLaunchPadPhase.Launching => "launching",
                    _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "未知の発射台の段階"),
                };
            }

            string ToWireWaitReason(RocketLaunchPadWaitReason reason)
            {
                return reason switch
                {
                    RocketLaunchPadWaitReason.None => "none",
                    RocketLaunchPadWaitReason.MissingMaterials => "missingMaterials",
                    RocketLaunchPadWaitReason.MissingPayload => "missingPayload",
                    RocketLaunchPadWaitReason.OutputFull => "outputFull",
                    RocketLaunchPadWaitReason.NoPower => "noPower",
                    _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "未知の発射台の待ち理由"),
                };
            }

            #endregion
        }

    }
}
