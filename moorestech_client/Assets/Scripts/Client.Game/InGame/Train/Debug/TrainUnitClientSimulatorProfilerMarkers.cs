using Unity.Profiling;

namespace Client.Game.InGame.Train.DebugView
{
    // 列車クライアントシミュレータの毎フレーム処理をProfilerで見分けるためのマーカー
    // Markers that split the train client simulator's per-frame work into named rows in the Profiler
    public static class TrainUnitClientSimulatorProfilerMarkers
    {
        public static readonly ProfilerMarker AdvanceTicks = new("TrainSimulator.AdvanceTicks");
        public static readonly ProfilerMarker TrainVisuals = new("TrainSimulator.TrainVisuals");
        public static readonly ProfilerMarker BeltItems = new("TrainSimulator.BeltItems");
        public static readonly ProfilerMarker BeltMergeCellCovers = new("TrainSimulator.BeltMergeCellCovers");
        public static readonly ProfilerMarker FluidPipeWaterLevels = new("TrainSimulator.FluidPipeWaterLevels");
    }
}
