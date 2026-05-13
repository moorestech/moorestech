using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class GearMinerBlockInventoryView : MinerBlockInventoryView
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

            var masterParam = (GearMinerBlockParam)BlockGameObject.BlockMasterElement.BlockParam;
            GearMachineBlockInventoryView.SetGearText(masterParam.GearConsumption, state, _cachedNetworkInfo, torque, rpm, networkInfo);
        }
    }
}
