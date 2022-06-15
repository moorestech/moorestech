namespace MainGame.UnityView.Util
{
    public interface IInitialViewLoadingDetector
    {
        public void FinishItemTextureLoading();
        public void FinishMapTileTextureLoading();
        public void FinishBlockModelLoading();
    }
}