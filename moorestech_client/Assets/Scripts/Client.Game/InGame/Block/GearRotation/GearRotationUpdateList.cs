using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor;

namespace Client.Game.InGame.Block.GearRotation
{
    /// <summary>
    /// 回転を進める歯車の一覧。歯車ごとのUpdate呼び出し(数千個)をやめ、GearRotationTickerの1ループで回す
    /// 有効な間だけ載る。距離カリングの半径外は無効になるので自然に外れる
    /// The list of gears whose rotation advances; replaces thousands of per-gear Update calls with one loop in GearRotationTicker
    /// Gears are listed only while enabled, so those disabled outside the distance-culling radius drop out naturally
    /// </summary>
    public static class GearRotationUpdateList
    {
        private static readonly List<GearStateChangeProcessor> Processors = new();
        private static readonly Dictionary<GearStateChangeProcessor, int> IndexByProcessor = new();

        public static void Add(GearStateChangeProcessor processor)
        {
            if (IndexByProcessor.ContainsKey(processor)) return; // silent-ok: OnEnableの重複通知は1件として扱う / 一覧の件数で確認する
            IndexByProcessor[processor] = Processors.Count;
            Processors.Add(processor);
        }

        public static void Remove(GearStateChangeProcessor processor)
        {
            if (!IndexByProcessor.TryGetValue(processor, out var index)) return; // silent-ok: 未登録の解除は何もしない / 一覧の件数で確認する

            // 末尾と入れ替えて消し、解除をO(1)に保つ
            // Swap with the last element before removing to keep removal O(1)
            var lastIndex = Processors.Count - 1;
            var last = Processors[lastIndex];
            Processors[index] = last;
            IndexByProcessor[last] = index;
            Processors.RemoveAt(lastIndex);
            IndexByProcessor.Remove(processor);
        }

        public static void AdvanceAll(float deltaTime)
        {
            foreach (var processor in Processors) processor.AdvanceRotation(deltaTime);
        }
    }
}
