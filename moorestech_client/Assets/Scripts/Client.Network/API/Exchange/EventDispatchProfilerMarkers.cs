using Unity.Profiling;

namespace Client.Network.API
{
    // 受信パケット配送の内訳をProfilerで見分けるためのマーカー
    // Markers that split received-packet dispatch into named rows in the Profiler
    public static class EventDispatchProfilerMarkers
    {
        public static readonly ProfilerMarker DispatchReceivedPackets = new("PacketExchange.DispatchReceivedPackets");
        public static readonly ProfilerMarker DeserializeProtocolBase = new("PacketExchange.Deserialize.ProtocolBase");
        public static readonly ProfilerMarker DeserializeEventStream = new("PacketExchange.Deserialize.EventStream");
        public static readonly ProfilerMarker RouteEvent = new("PacketExchange.RouteEvent");
        public static readonly ProfilerMarker RouteResponse = new("PacketExchange.RouteResponse");
    }
}
