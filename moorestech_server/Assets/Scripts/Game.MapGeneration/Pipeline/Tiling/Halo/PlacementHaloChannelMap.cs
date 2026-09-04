using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Tiling
{
    // クラスター中心haloをveinGuidごとに分ける帳面。全鉱脈で共有すると先行エントリの中心が後続エントリの候補を面で締め出す。
    // Per-veinGuid ledger of cluster-center haloes; a shared one lets earlier entries' centers blanket out later entries' candidates.
    public class PlacementHaloChannelMap
    {
        private readonly Dictionary<string, PlacementHaloChannel> _channels = new();

        // 未登録キーは空チャネルを作って返す。最初のタイルは種なしで始まるのが正常系。
        // An unknown key creates and returns an empty channel; the first tile legitimately starts with no seeds.
        public PlacementHaloChannel GetOrCreate(string veinGuid)
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
