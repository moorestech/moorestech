using System;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    /// <summary>
    /// 部屋欄の各行。台数とこの清浄機の行は清浄機から見た部屋の派生にだけある（ADR 0105 §5）
    /// The room section's rows; the count and own-status rows exist only on the purifier subtype (ADR 0105 §5)
    /// </summary>
    public abstract class CleanRoomDisplay
    {
        public UiText Headline { get; }
        public bool HeadlineInsufficient { get; }
        private readonly CleanRoomHaltReason _haltReason;

        private protected CleanRoomDisplay(UiText headline, bool headlineInsufficient, CleanRoomHaltReason haltReason)
        {
            Headline = headline;
            HeadlineInsufficient = headlineInsufficient;
            _haltReason = haltReason;
        }

        // 停止理由の行。止まっていなければ行なし
        // The halt-reason row; none while not halted
        public bool TryGetHaltReason(out UiText haltReason)
        {
            haltReason = _haltReason switch
            {
                CleanRoomHaltReason.None => null,
                CleanRoomHaltReason.NotEnclosed => new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomHaltNotEnclosed),
                CleanRoomHaltReason.ClassOut => new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomHaltClassOut),
                _ => throw new ArgumentOutOfRangeException(nameof(_haltReason), _haltReason, null),
            };
            return _haltReason != CleanRoomHaltReason.None;
        }
    }

    // 清浄機から見た密閉された部屋。台数の行とこの清浄機の行を持つ
    // An enclosed room seen from a purifier; carries the count row and the own-status row
    public sealed class AirFilterCleanRoomDisplay : CleanRoomDisplay
    {
        public UiText AirFilterCount { get; }
        public UiText QueriedAirFilter { get; }

        internal AirFilterCleanRoomDisplay(UiText headline, bool headlineInsufficient, UiText airFilterCount, UiText queriedAirFilter, CleanRoomHaltReason haltReason) : base(headline, headlineInsufficient, haltReason)
        {
            AirFilterCount = airFilterCount;
            QueriedAirFilter = queriedAirFilter;
        }
    }

    // 室内機械の部屋クラス・密閉されていない・検出中は見出しだけ
    // An interior machine's room class, unsealed or detecting rooms show the headline only
    public sealed class HeadlineOnlyCleanRoomDisplay : CleanRoomDisplay
    {
        internal HeadlineOnlyCleanRoomDisplay(UiText headline, bool headlineInsufficient, CleanRoomHaltReason haltReason) : base(headline, headlineInsufficient, haltReason)
        {
        }
    }
}
