namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     毎フレーム1回だけ走査し、その結果を値で返す役。テストは実装を差し替える
    ///     Scans once per frame and hands the result back as a value; tests substitute their own implementation
    /// </summary>
    public interface IInteractTargetSelector
    {
        IInteractSelection Scan();
    }
}
