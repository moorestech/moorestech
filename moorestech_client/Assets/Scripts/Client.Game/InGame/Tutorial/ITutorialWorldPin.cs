namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     ワールド上の対象を指すチュートリアルピン
    ///     Tutorial pin that points at a target in the world
    /// </summary>
    public interface ITutorialWorldPin : ITutorialViewManager, ITutorialView
    {
        // どのtutorialTypeを担うピンかを自身が名乗る。型でピンを見分ける必要をなくす
        // Each pin names the tutorialType it serves, so nothing has to tell pins apart by their type
        string TutorialType { get; }

        void SetActive(bool active);

        // 抑止は入れ子で始まり得るので、真偽の代入ではなく深さの増減で表す
        // Suppression can nest, so it is expressed as depth changes rather than assigning a flag
        void BeginSkitSuppress();
        void EndSkitSuppress();
    }
}
