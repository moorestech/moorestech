using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Unity.Profiling;

namespace Client.Game.InGame.Block
{
    // ブロック状態イベントの処理内訳をProfilerで見分けるためのマーカー
    // Markers that split block-state event handling into named rows in the Profiler
    public static class BlockStateProfilerMarkers
    {
        public static readonly ProfilerMarker DeserializeBlockState = new("BlockState.Deserialize");
        public static readonly ProfilerMarker FluidPipeGetStateDetail = new("BlockState.FluidPipe.GetStateDetail");
        public static readonly ProfilerMarker FluidPipeApplyWaterLevel = new("BlockState.FluidPipe.ApplyWaterLevel");

        private static readonly Dictionary<Type, ProfilerMarker> ProcessorMarkers = new();

        // プロセッサ型ごとに1本のマーカーを使い回す
        // Reuse one marker per processor type
        public static ProfilerMarker ForProcessor(IBlockStateChangeProcessor processor)
        {
            var type = processor.GetType();
            if (ProcessorMarkers.TryGetValue(type, out var marker)) return marker;

            marker = new ProfilerMarker($"BlockState.Processor.{type.Name}");
            ProcessorMarkers.Add(type, marker);
            return marker;
        }
    }
}
