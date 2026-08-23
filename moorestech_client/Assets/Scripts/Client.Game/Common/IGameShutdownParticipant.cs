using Cysharp.Threading.Tasks;

namespace Client.Game.Common
{
    // 終了時に書き出しを終わらせる責務を持つ参加者。完了か上限到達かを戻り値で返す
    // A participant that finishes its writes at shutdown, reporting completion or budget exhaustion
    public interface IGameShutdownParticipant
    {
        UniTask<ShutdownFlushResult> FlushOnShutdownAsync();
    }
}
