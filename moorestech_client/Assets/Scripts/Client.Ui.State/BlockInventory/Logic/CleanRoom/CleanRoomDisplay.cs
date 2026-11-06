using System;
using System.Collections.Generic;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    /// <summary>
    /// 部屋欄の各行（Web の CleanRoomDisplay）。空気・清浄機の行は密閉された部屋の派生にだけある（判断記録 D-5）
    /// The room section's rows (Web CleanRoomDisplay); air and purifier rows exist only on the enclosed subtype (D-5)
    /// </summary>
    public abstract class CleanRoomDisplay
    {
        public UiText Headline { get; }
        public bool HeadlineInsufficient { get; }
        private readonly CleanRoomHaltReason _haltReason;

        protected CleanRoomDisplay(UiText headline, bool headlineInsufficient, CleanRoomHaltReason haltReason)
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

    // 密閉された部屋。清浄機が 1 台も無ければ「清浄機なし」の行を出す
    // An enclosed room; with no purifier at all the "no filter" row appears
    public sealed class EnclosedCleanRoomDisplay : CleanRoomDisplay
    {
        public UiText Air { get; }
        public IReadOnlyList<UiText> Filters { get; }
        public UiText NoFilter { get; }
        public bool ShowsNoFilter => Filters.Count == 0;

        public EnclosedCleanRoomDisplay(UiText headline, bool headlineInsufficient, UiText air, IReadOnlyList<UiText> filters, CleanRoomHaltReason haltReason) : base(headline, headlineInsufficient, haltReason)
        {
            Air = air;
            Filters = filters;
            NoFilter = new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomNoFilter);
        }
    }

    // 密閉されていない・検出中は見出しだけ
    // Unsealed or detecting rooms show the headline only
    public sealed class HeadlineOnlyCleanRoomDisplay : CleanRoomDisplay
    {
        public HeadlineOnlyCleanRoomDisplay(UiText headline, bool headlineInsufficient, CleanRoomHaltReason haltReason) : base(headline, headlineInsufficient, haltReason)
        {
        }
    }
}
