using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Gear.Common;
using Game.PlayerInventory.Interface.Subscription;
using Server.Protocol.PacketResponse;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class GearEnergyTransformerUIView : MonoBehaviour, IBlockInventoryView
    {
        [SerializeField] private TMP_Text blockNameText;
        [SerializeField] private TMP_Text torque;
        [SerializeField] private TMP_Text rpm;
        [SerializeField] private TMP_Text networkInfo;

        private BlockGameObject _blockGameObject;
        private GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack _cachedNetworkInfo;

        public void Initialize(BlockGameObject blockGameObject)
        {
            blockNameText.text = blockGameObject.BlockMasterElement.Name;
            _blockGameObject = blockGameObject;

            // UIオープン時に1度だけサーバーへ問い合わせ、ネットワーク集約値を取得
            // Fetch gear network aggregate info once when the UI opens
            FetchNetworkInfo().Forget();
        }

        private async UniTask FetchNetworkInfo()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            _cachedNetworkInfo = await ClientContext.VanillaApi.Response.GetGearNetworkInfo(_blockGameObject.BlockInstanceId, ct);
        }

        private void Update()
        {
            var state = _blockGameObject.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (state == null)
            {
                Debug.LogError("GearStateDetailが取得できません。");
                return;
            }

            torque.text = $"トルク: {state.CurrentTorque}";
            rpm.text = $"回転数: {state.CurrentRpm}";

            // 取得済みのネットワーク情報キャッシュから表示。Info が null なら未取得 / 未登録ブロックのため空欄
            // Display cached network info; Info == null means pending fetch or unregistered block, leave blank
            var snapshot = _cachedNetworkInfo?.Info;
            if (snapshot != null)
            {
                networkInfo.text = $"{GetStopReasonText(snapshot.StopReason)} 必要力: {snapshot.TotalRequiredGearPower:F2} 生成力: {snapshot.TotalGenerateGearPower:F2}";
            }
            else
            {
                networkInfo.text = string.Empty;
            }
        }

        public static string GetStopReasonText(GearNetworkStopReason reason)
        {
            var text = reason switch
            {
                GearNetworkStopReason.None => string.Empty,
                GearNetworkStopReason.OverRequirePower => "パワー不足",
                GearNetworkStopReason.Rocked => "ロック",
                _ => string.Empty
            };

            return text == string.Empty ? string.Empty : $"<color=red>{text} </color>";
        }


        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; } = new List<ItemSlotView>();
        public List<IItemStack> SubInventory { get; } = new();
        public int Count => 0;
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; } = null; // インベントリはないのでnullを入れておく

        public void UpdateItemList(List<IItemStack> response) { }
        public void UpdateInventorySlot(int slot, IItemStack item) { }
        public void DestroyUI()
        {
            Destroy(gameObject);
        }
    }
}
