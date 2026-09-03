using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Tiling
{
    // クラスター中心haloをveinGuidごとに分ける帳面。全鉱脈で共有すると先行エントリの中心が後続エントリの候補を面で締め出す。
    // Per-veinGuid ledger of cluster-center haloes; a shared one lets earlier entries' centers blanket out later entries' candidates.
    public class PlacementHaloChannelMap
    {
        private readonly Dictionary<string, PlacementHaloChannel> _channels = new();

        public PlacementHaloChannel Get(string veinGuid)
        {
            if (!_channels.TryGetValue(veinGuid, out var channel))
            {
                channel = new PlacementHaloChannel();
                _channels[veinGuid] = channel;
            }
            return channel;
        }
    }
}
