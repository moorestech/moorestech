using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Text;
using Game.Gear.Common;
using Server.Protocol.PacketResponse;
using ServerFilterSplitterMode = Game.Block.Blocks.FilterSplitter.FilterSplitterMode;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// 集約キャッシュ（電力網・歯車網・フィルタ分配器）を詳細へ写す。未取得（null）は不在のまま（Web ApplyNetworkCaches と同じ）
    /// Maps the aggregate caches to details; unfetched (null) stays absent (same as Web ApplyNetworkCaches)
    /// </summary>
    public static class SpecialNetworkReader
    {
        public static ElectricNetworkDetail Electric(GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot snapshot)
        {
            // silent-ok: 未取得はポーリングの到着待ちで、区画を出さないのが Web と同じ表示 / 届かないままなら録画（R-12）の電柱の段で気づく
            if (snapshot == null) return null;
            return new ElectricNetworkDetail(UiNumberFormat.FromServerFloat(snapshot.TotalGeneratePower), UiNumberFormat.FromServerFloat(snapshot.TotalRequiredPower), snapshot.ConsumerCount, UiNumberFormat.FromServerFloat(snapshot.PowerRate));
        }

        public static GearNetworkDetail Gear(GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot snapshot)
        {
            // silent-ok: 未取得はポーリングの到着待ち / 届かないままなら録画（R-12）の歯車の段で気づく
            if (snapshot == null) return null;
            return new GearNetworkDetail(UiNumberFormat.FromServerFloat(snapshot.TotalGenerateGearPower), UiNumberFormat.FromServerFloat(snapshot.TotalRequiredGearPower), ToStopReasonKind(snapshot.StopReason));
        }

        public static FilterSplitterDetail FilterSplitter(FilterSplitterStateProtocol.FilterSplitterStateResponse response)
        {
            // silent-ok: 未取得は分配器の初回取得の到着待ち / 届かないままなら録画（R-12）の分配器の段で気づく
            if (response == null) return null;
            var directions = new List<FilterSplitterDirectionDetail>(response.Directions.Count);
            foreach (var direction in response.Directions) directions.Add(new FilterSplitterDirectionDetail(ToClientMode(direction.Mode), direction.FilterItemIds));
            return new FilterSplitterDetail(directions);
        }

        internal static GearNetworkStopReasonKind ToStopReasonKind(GearNetworkStopReason reason)
        {
            return reason switch
            {
                GearNetworkStopReason.None => GearNetworkStopReasonKind.None,
                GearNetworkStopReason.Rocked => GearNetworkStopReasonKind.Rocked,
                GearNetworkStopReason.OverRequirePower => GearNetworkStopReasonKind.OverRequirePower,
                GearNetworkStopReason.NoGeneration => GearNetworkStopReasonKind.NoGeneration,
                GearNetworkStopReason.NoGenerator => GearNetworkStopReasonKind.NoGenerator,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "未知の歯車網の停止理由"),
            };
        }

        internal static FilterSplitterMode ToClientMode(ServerFilterSplitterMode mode)
        {
            return mode switch
            {
                ServerFilterSplitterMode.Default => FilterSplitterMode.Default,
                ServerFilterSplitterMode.Whitelist => FilterSplitterMode.Whitelist,
                ServerFilterSplitterMode.Blacklist => FilterSplitterMode.Blacklist,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知のフィルタ分配器のモード"),
            };
        }

        public static ServerFilterSplitterMode ToServerMode(FilterSplitterMode mode)
        {
            return mode switch
            {
                FilterSplitterMode.Default => ServerFilterSplitterMode.Default,
                FilterSplitterMode.Whitelist => ServerFilterSplitterMode.Whitelist,
                FilterSplitterMode.Blacklist => ServerFilterSplitterMode.Blacklist,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知のフィルタ分配器のモード"),
            };
        }
    }
}
