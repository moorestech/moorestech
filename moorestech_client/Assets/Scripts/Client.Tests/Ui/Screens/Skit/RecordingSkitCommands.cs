using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Commands;
using Client.Ui.State.Skit;

namespace Client.Tests.Ui.Screens.Skit
{
    public enum SkitCommandKind
    {
        Advance,
        Select,
        SetAuto,
        Skip,
        SetUiHidden,
    }

    // 発行された会話の操作 1 件。choiceId と真偽は該当する操作だけが持つ
    // One issued skit operation; the choice id and the flag are set only by the operations that carry them
    public readonly struct SkitCommandRecord
    {
        public readonly SkitCommandKind Kind;
        public readonly string SessionId;
        public readonly int SceneRevision;
        public readonly string ChoiceId;
        public readonly bool Flag;

        public SkitCommandRecord(SkitCommandKind kind, string sessionId, int sceneRevision, string choiceId, bool flag)
        {
            Kind = kind;
            SessionId = sessionId;
            SceneRevision = sceneRevision;
            ChoiceId = choiceId;
            Flag = flag;
        }

        public override string ToString() => $"{Kind}({SessionId}, {SceneRevision}, {ChoiceId}, {Flag})";
    }

    /// <summary>
    /// 会話の操作の記録係。どれも受理を返す
    /// Recorder of skit operations; every call returns accepted
    /// </summary>
    public sealed class RecordingSkitCommands : CommandRecorder<SkitCommandRecord>, ISkitCommands
    {
        public UiCommandResult Advance(string sessionId, int sceneRevision) => Accept(new SkitCommandRecord(SkitCommandKind.Advance, sessionId, sceneRevision, null, false));
        public UiCommandResult Select(string sessionId, int sceneRevision, string choiceId) => Accept(new SkitCommandRecord(SkitCommandKind.Select, sessionId, sceneRevision, choiceId, false));
        public UiCommandResult SetAuto(string sessionId, int sceneRevision, bool enabled) => Accept(new SkitCommandRecord(SkitCommandKind.SetAuto, sessionId, sceneRevision, null, enabled));
        public UiCommandResult Skip(string sessionId, int sceneRevision) => Accept(new SkitCommandRecord(SkitCommandKind.Skip, sessionId, sceneRevision, null, false));
        public UiCommandResult SetUiHidden(string sessionId, int sceneRevision, bool hidden) => Accept(new SkitCommandRecord(SkitCommandKind.SetUiHidden, sessionId, sceneRevision, null, hidden));

        private UiCommandResult Accept(SkitCommandRecord record)
        {
            Record(record);
            return UiCommandAccepted.Instance;
        }
    }
}
