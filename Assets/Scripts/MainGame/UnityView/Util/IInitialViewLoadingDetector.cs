namespace MainGame.UnityView.Util
{
    /// <summary>
    /// todo これを廃止して、プレゼンターでイベントをキャッチするようにする
    /// </summary>
    public interface IInitialViewLoadingDetector
    {
        public void FinishItemTextureLoading();
        public void FinishMapTileTextureLoading();
        public void FinishBlockModelLoading();
    }
}