using System;
using System.Collections.Generic;

namespace Game.Block.Blocks.Machine.Upgrade
{
    /// <summary>
    ///     継承先ファミリーごとの製作上限の表。未登録は初期値、値は上がるだけ（サーバーの DataStore とクライアントの写しが共有する）
    ///     Craft cap table per target family: absent means the initial cap and values only rise (shared by the server DataStore and the client mirror)
    /// </summary>
    public class LevelInheritanceCraftCapTable
    {
        // 研究で解放されていない継承先は Lv1 まで作れる
        // A target family without research can be made up to Lv1
        public const int InitialCraftCap = 1;

        // 継承先ファミリーの基準アイテムGUID → 解放済みの上限（初期値は含まない）
        // Target family base item GUID → unlocked cap (the initial value is excluded)
        public IReadOnlyDictionary<Guid, int> UnlockedCaps => _unlockedCaps;
        private readonly Dictionary<Guid, int> _unlockedCaps = new();

        public int GetCraftCap(Guid familyBaseItemGuid)
        {
            return _unlockedCaps.GetValueOrDefault(familyBaseItemGuid, InitialCraftCap);
        }

        // 上がったときだけ true
        // True only when the cap rose
        public bool TryRaise(Guid familyBaseItemGuid, int level)
        {
            // silent-ok: 同じか下のレベルは研究のロード時再実行・初期データとイベントの重なりで正常に来る / 現在の上限は機械UIの継承表示（C9）に出る
            if (level <= GetCraftCap(familyBaseItemGuid)) return false;
            _unlockedCaps[familyBaseItemGuid] = level;
            return true;
        }
    }
}
