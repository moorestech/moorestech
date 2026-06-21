using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    /// <summary>
    ///     電力ネットワークの集約情報(現在発電量/現在要求量/供給率)を表示する再利用コンポーネント。
    ///     電気機械・発電機・電柱の各UIに付与して共通化する。
    ///     Reusable component that displays aggregated electric network info (current generation/demand/supply rate);
    ///     attached to electric machine, generator, and pole UIs to share the display.
    /// </summary>
    public class ElectricNetworkInfoView : MonoBehaviour
    {
        // ネットワークはマージ/分割や燃料切れで変化するため一定間隔で再取得する
        // The network changes via merge/split and fuel depletion, so re-fetch at a fixed interval
        private const int FetchIntervalMilliseconds = 1000;

        [SerializeField] private TMP_Text networkInfoText;

        private BlockInstanceId _blockInstanceId;
        private GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack _cachedInfo;

        public void Initialize(BlockInstanceId blockInstanceId)
        {
            _blockInstanceId = blockInstanceId;
            // UIオープン中は定期的に再取得し続ける
            // Keep fetching network info periodically while the UI is open
            FetchLoop().Forget();
        }

        private async UniTask FetchLoop()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            while (!ct.IsCancellationRequested)
            {
                _cachedInfo = await ClientContext.VanillaApi.Response.GetElectricNetworkInfo(_blockInstanceId, ct);
                await UniTask.Delay(FetchIntervalMilliseconds, cancellationToken: ct);
            }
        }

        private void Update()
        {
            networkInfoText.text = BuildText(_cachedInfo?.Info);
        }

        // スナップショットから表示文字列を組み立てる。供給不足は赤、消費者0は「需要なし」
        // Build the display text from the snapshot; shortage shown in red, zero consumers shows "no demand"
        public static string BuildText(GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot snapshot)
        {
            if (snapshot == null) return string.Empty;

            var generate = snapshot.TotalGeneratePower;
            var required = snapshot.TotalRequiredPower;

            // 消費者がいない場合は供給率(0%)ではなく需要なしを表示し供給不足との誤認を防ぐ
            // With no consumers, show "no demand" instead of a 0% rate to avoid being mistaken for a shortage
            if (snapshot.ConsumerCount == 0)
            {
                return $"発電量: {generate:F2} 要求量: {required:F2} 需要なし";
            }

            // 表示する整数%で色判定し、丸めで100%表示なのに赤になる矛盾を防ぐ
            // Decide the color from the displayed integer %, avoiding red on a value that rounds to 100%
            var percent = Mathf.RoundToInt(snapshot.PowerRate * 100f);
            var colorTag = percent < 100 ? "<color=red>" : string.Empty;
            var resetTag = percent < 100 ? "</color>" : string.Empty;
            return $"発電量: {generate:F2} 要求量: {required:F2} 供給率: {colorTag}{percent}%{resetTag}";
        }
    }
}
