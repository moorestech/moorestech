using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Tiling
{
    // クラスター中心haloをエントリごとに分ける帳面。全鉱脈で共有すると先行エントリの中心が後続エントリの候補を面で締め出す。
    // 鍵はエントリ配列上の位置。veinGuidを鍵にすると同じ鉱脈を複数エントリで撒く構成がタイル跨ぎだけ相互排他する非対称になる。
    // Per-entry ledger of cluster-center haloes; a shared one lets earlier entries' centers blanket out later entries' candidates.
    // The key is the entry's slot in its array; keying by veinGuid would make two entries of one vein exclude each other across tiles but not inside one.
    public class PlacementHaloChannelMap
    {
        private readonly Dictionary<int, PlacementHaloChannel> _channels = new();

        // 未登録キーは空チャネルを作って返す。最初のタイルは種なしで始まるのが正常系。
        // An unknown key creates and returns an empty channel; the first tile legitimately starts with no seeds.
        public PlacementHaloChannel GetOrCreate(int entryIndex)
        {
            if (!_channels.TryGetValue(entryIndex, out var channel))
            {
                channel = new PlacementHaloChannel();
                _channels[entryIndex] = channel;
            }
            return channel;
        }
    }
}
