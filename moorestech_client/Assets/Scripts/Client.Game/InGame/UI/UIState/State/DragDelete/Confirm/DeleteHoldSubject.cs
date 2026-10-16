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
    ///     F長押しの対象。対象なし／選択集合（Revision で同一性）／ホバー1件（論理キーで同一性）を判別子で表す
    ///     The hold-to-confirm subject: none, the selection (identified by revision) or one hovered target (identified by its logical key)
    /// </summary>
    public readonly struct DeleteHoldSubject : IEquatable<DeleteHoldSubject>
    {
        public static DeleteHoldSubject None => new(DeleteHoldSubjectKind.None, null);
        public static DeleteHoldSubject Selection(int revision) => new(DeleteHoldSubjectKind.Selection, revision);
        public static DeleteHoldSubject Hovered(object targetKey) => new(DeleteHoldSubjectKind.Hovered, targetKey);

        public DeleteHoldSubjectKind Kind { get; }
        public bool IsNone => Kind == DeleteHoldSubjectKind.None;
        private readonly object _key;

        private DeleteHoldSubject(DeleteHoldSubjectKind kind, object key)
        {
            Kind = kind;
            _key = key;
        }

        public bool Equals(DeleteHoldSubject other)
        {
            return Kind == other.Kind && Equals(_key, other._key);
        }

        public override bool Equals(object obj)
        {
            return obj is DeleteHoldSubject other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Kind, _key);
        }
    }
}
