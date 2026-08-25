namespace Game.SaveLoad.Interface
{
    public interface IWorldSaveRequest
    {
        // 保存を要求し、その要求番号を返す。完了待ちは番号と完了通知を突き合わせる
        // Requests a save and returns its generation; a flush wait matches this number against the completion notice
        long RequestSave();
    }
}
