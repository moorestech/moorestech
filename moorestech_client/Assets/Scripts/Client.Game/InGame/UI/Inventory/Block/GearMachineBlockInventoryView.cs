using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using TMPro;
using UnityEngine;
using static Client.Game.InGame.UI.Inventory.Block.GearEnergyTransformerUIView;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class GearMachineBlockInventoryView : MachineBlockInventoryView
    {
        [SerializeField] private TMP_Text torque;
        [SerializeField] private TMP_Text rpm;
        [SerializeField] private TMP_Text networkInfo;

        private GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack _cachedNetworkInfo;

        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            // UIオープン時に1度だけネットワーク集約値を取得
            // Fetch gear network aggregate info once when the UI opens
            FetchNetworkInfo().Forget();
        }

        private async UniTask FetchNetworkInfo()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            _cachedNetworkInfo = await ClientContext.VanillaApi.Response.GetGearNetworkInfo(BlockGameObject.BlockInstanceId, ct);
        }

        private new void Update()
        {
            base.Update();

            var state = BlockGameObject.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (state == null)
            {
                Debug.LogError("GearStateDetailが取得できません。");
                return;
            }

            var masterParam = (GearMachineBlockParam)BlockGameObject.BlockMasterElement.BlockParam;
            SetGearText(masterParam, state, _cachedNetworkInfo, torque, rpm, networkInfo);
        }

        // 歯車系UIで共通して使う表示更新ヘルパー
        // Shared display helper reused across gear-based UIs
        public static void SetGearText(IGearMachineParam param, GearStateDetail state, GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack networkInfoResponse, TMP_Text torqueText, TMP_Text rpmText, TMP_Text networkInfoText)
        {
            var requireTorque = param.RequireTorque;
            var requireRpm = param.RequiredRpm;

            var currentTorque = state.CurrentTorque;
            var currentRpm = state.CurrentRpm;

            torqueText.text = $"トルク: {currentTorque:F2} / {requireTorque:F2}";
            if (currentTorque < requireTorque)
            {
                torqueText.text = $"トルク: <color=red>{currentTorque:F2}</color> / {requireTorque:F2}";
            }

            rpmText.text = $"回転数: {currentRpm:F2} / {requireRpm:F2}";
            if (currentRpm < requireRpm)
            {
                rpmText.text = $"回転数: <color=red>{currentRpm:F2}</color> / {requireRpm:F2}";
            }

            // ネットワーク集約情報はUIオープン時に取得したキャッシュを使う。Info が null なら未取得 / 未登録ブロックで空欄
            // Network aggregate info comes from the cache fetched at UI open; Info == null means pending or unregistered
            var snapshot = networkInfoResponse?.Info;
            if (snapshot != null)
            {
                networkInfoText.text = $"{GetStopReasonText(snapshot.StopReason)} 必要力: {snapshot.TotalRequiredGearPower:F2} 生成力: {snapshot.TotalGenerateGearPower:F2}";
            }
            else
            {
                networkInfoText.text = string.Empty;
            }
        }
    }
}
