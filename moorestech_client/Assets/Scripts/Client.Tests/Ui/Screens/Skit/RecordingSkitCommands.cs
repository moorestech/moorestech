using Client.Tests.Ui.TestDoubles;
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

    // 発行された操作 1 件
    // One issued operation
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
    /// 会話の操作の記録係
    /// Recorder of skit operations
    /// </summary>
    public sealed class RecordingSkitCommands : CommandRecorder<SkitCommandRecord>, ISkitCommands
    {
        public void Advance(string sessionId, int sceneRevision) => Record(new SkitCommandRecord(SkitCommandKind.Advance, sessionId, sceneRevision, null, false));
        public void Select(string sessionId, int sceneRevision, string choiceId) => Record(new SkitCommandRecord(SkitCommandKind.Select, sessionId, sceneRevision, choiceId, false));
        public void SetAuto(string sessionId, int sceneRevision, bool enabled) => Record(new SkitCommandRecord(SkitCommandKind.SetAuto, sessionId, sceneRevision, null, enabled));
        public void Skip(string sessionId, int sceneRevision) => Record(new SkitCommandRecord(SkitCommandKind.Skip, sessionId, sceneRevision, null, false));
        public void SetUiHidden(string sessionId, int sceneRevision, bool hidden) => Record(new SkitCommandRecord(SkitCommandKind.SetUiHidden, sessionId, sceneRevision, null, hidden));
    }
}
