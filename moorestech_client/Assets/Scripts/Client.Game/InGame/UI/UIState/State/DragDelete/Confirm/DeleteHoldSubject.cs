using System;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
{
    public enum DeleteHoldSubjectKind
    {
        None,
        Selection,
        Hovered,
    }

    /// <summary>
    ///     F長押しの対象。対象なし／選択集合（Revision で同一性・件数を運ぶ）／ホバー1件（論理キーで同一性）を判別子で表す
    ///     The hold-to-confirm subject: none, the selection (identified by revision, carrying its count) or one hovered target (identified by its logical key)
    /// </summary>
    public readonly struct DeleteHoldSubject : IEquatable<DeleteHoldSubject>
    {
        public static DeleteHoldSubject None => new(DeleteHoldSubjectKind.None, null, 0);
        public static DeleteHoldSubject Selection(int revision, int targetCount) => new(DeleteHoldSubjectKind.Selection, revision, targetCount);
        public static DeleteHoldSubject Hovered(object targetKey) => new(DeleteHoldSubjectKind.Hovered, targetKey, 1);

        internal DeleteHoldSubjectKind Kind { get; }
        internal bool IsNone => Kind == DeleteHoldSubjectKind.None;
        // 満了時間を決める件数（ホバー1件は1件）
        // Count that decides the hold duration (one hovered target counts as one)
        internal int TargetCount { get; }
        private readonly object _key;

        private DeleteHoldSubject(DeleteHoldSubjectKind kind, object key, int targetCount)
        {
            Kind = kind;
            _key = key;
            TargetCount = targetCount;
        }

        public bool Equals(DeleteHoldSubject other)
        {
            return Kind == other.Kind && Equals(_key, other._key) && TargetCount == other.TargetCount;
        }

        public override bool Equals(object obj)
        {
            return obj is DeleteHoldSubject other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Kind, _key, TargetCount);
        }
    }
}
