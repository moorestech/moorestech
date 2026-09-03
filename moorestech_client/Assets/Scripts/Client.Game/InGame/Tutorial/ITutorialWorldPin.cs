namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     ワールド上の対象を指すチュートリアルピン
    ///     Tutorial pin that points at a target in the world
    /// </summary>
    public interface ITutorialWorldPin : ITutorialViewManager, ITutorialView
    {
        void SetActive(bool active);

        // 抑止は入れ子で始まり得るので、真偽の代入ではなく深さの増減で表す
        // Suppression can nest, so it is expressed as depth changes rather than assigning a flag
        void BeginSkitSuppress();
        void EndSkitSuppress();
    }
}
