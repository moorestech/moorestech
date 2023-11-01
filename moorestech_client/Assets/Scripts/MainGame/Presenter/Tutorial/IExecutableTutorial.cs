namespace MainGame.Presenter.Tutorial
{
    /// <summary>
    /// チュートリアルを実行するコードが持つInterface
    /// </summary>
    public interface IExecutableTutorial
    {
        public bool IsFinishTutorial { get; } 
        public void StartTutorial();
        public void Update();
        public void EndTutorial();
    }
}