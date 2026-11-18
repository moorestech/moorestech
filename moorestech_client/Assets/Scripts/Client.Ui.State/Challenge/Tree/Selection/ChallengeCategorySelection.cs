using System;

namespace Client.Ui.State.Challenge.Tree.Selection
{
    /// <summary>
    /// 選んだチャレンジのカテゴリ。無いことを型で持つ
    /// The selected challenge category; absence is part of the type
    /// </summary>
    public sealed class ChallengeCategorySelection
    {
        public static readonly ChallengeCategorySelection None = new(false, Guid.Empty);

        private readonly bool _hasCategory;
        private readonly Guid _categoryGuid;

        private ChallengeCategorySelection(bool hasCategory, Guid categoryGuid)
        {
            _hasCategory = hasCategory;
            _categoryGuid = categoryGuid;
        }

        public static ChallengeCategorySelection Of(Guid categoryGuid)
        {
            return new ChallengeCategorySelection(true, categoryGuid);
        }

        public bool Is(Guid categoryGuid)
        {
            return _hasCategory && _categoryGuid == categoryGuid;
        }
    }
}
