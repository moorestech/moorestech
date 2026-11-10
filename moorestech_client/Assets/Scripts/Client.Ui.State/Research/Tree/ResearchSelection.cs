using System;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 詳細ペインに出す研究ノードの選択。無いことを型で持つ
    /// The research node selected for the detail pane; absence is part of the type
    /// </summary>
    public sealed class ResearchSelection
    {
        public static readonly ResearchSelection None = new(false, Guid.Empty);

        private readonly bool _hasNode;
        private readonly Guid _guid;

        private ResearchSelection(bool hasNode, Guid guid)
        {
            _hasNode = hasNode;
            _guid = guid;
        }

        public static ResearchSelection Of(Guid guid)
        {
            return new ResearchSelection(true, guid);
        }

        public bool Is(Guid guid)
        {
            return _hasNode && _guid == guid;
        }

        // 同じノードの再タップで閉じる（Web の toggleSelect）
        // Tapping the same node again closes it (the Web toggleSelect)
        public ResearchSelection Toggled(Guid guid)
        {
            return Is(guid) ? None : Of(guid);
        }
    }
}
