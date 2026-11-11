using Client.Game.InGame.BeltTransport;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Source;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// パネル・追跡ブロック・状態・集約・ベルトの窓の進みのたびに同期で詳細を組み直す（間引かない [A-57]。Web BlockInventoryTopic の再発行と同じ契機）
    /// Rebuilds the details synchronously on every panel, tracked block, state, aggregate and belt-window change (no thinning [A-57]; same triggers as Web BlockInventoryTopic)
    /// </summary>
    public sealed class SpecialBlockDetailModel : ISpecialBlockDetailModel, IInitializable
    {
        private readonly IBlockInventoryPanelSource _panels;
        private readonly OpenBlockDetailTracker _tracker;
        private readonly BlockNetworkInfoCache _networkCache;
        private readonly BeltTransportClientCache _beltCache;
        private readonly ReactiveProperty<SpecialBlockDetailViewModel> _current = new(SpecialBlockDetailViewModel.Empty);
        private string _lastReportedMismatch;

        public IReadOnlyReactiveProperty<SpecialBlockDetailViewModel> Current => _current;

        public SpecialBlockDetailModel(IBlockInventoryPanelSource panels, OpenBlockDetailTracker tracker, BlockNetworkInfoCache networkCache, BeltTransportClientCache beltCache)
        {
            _panels = panels;
            _tracker = tracker;
            _networkCache = networkCache;
            _beltCache = beltCache;
        }

        public void Initialize()
        {
            // 変化を起こす源を購読し、そのたびに組み直す
            // Subscribe to every source of change and rebuild each time
            _panels.Current.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockChanged.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => Rebuild());
            _networkCache.OnUpdated.Subscribe(_ => Rebuild());
            _beltCache.ExitStatistics.OnWindowAdvanced.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            // 開いたブロックの識別は alpha のパネル、中身は追跡ブロック。インスタンスが一致する間だけ読む（D-26）
            // Identity comes from alpha's panel and contents from the tracked block; read only while the instances match (D-26)
            var block = _tracker.TrackedBlock;
            var open = _panels.Current.Value as OpenBlockInventoryPanel;
            var opened = open is { Error: BlockInventoryPanelError.None } && block != null;
            var matches = opened && block.BlockInstanceId.Equals(open.BlockInstanceId);
            ReportMismatch(opened && !matches ? $"panel {open.BlockInstanceId} != tracked {block.BlockInstanceId}" : null);
            // silent-ok: 閉じている間・開閉と差し替えの途中（どちらかが null・エラー）は空にする（区画は出ず本文は取得待ち） / 両方そろって食い違う場合は ReportMismatch が警告ログに出す
            _current.Value = matches ? SpecialBlockDetailReader.Read(block, _networkCache, _beltCache) : SpecialBlockDetailViewModel.Empty;
        }

        private void ReportMismatch(string mismatch)
        {
            // 開いたパネルと追跡ブロックの食い違いは、組み合わせが変わったときだけ警告する（集約の更新ごとに出さない）
            // Warn only when the panel/tracked mismatch pair changes, not on every aggregate update
            if (mismatch != null && mismatch != _lastReportedMismatch) Debug.LogWarning($"[SpecialBlockDetailModel] {mismatch}");
            _lastReportedMismatch = mismatch;
        }
    }
}
