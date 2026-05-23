namespace Client.Game.InGame.Player.StateController
{
    public enum PlayerStateEnum
    {
        // 通常時：徒歩操作・自由移動。
        // Normal: walking control, free movement.
        Normal,
        // 乗車中：列車車両に親付けされ ThirdPersonController を停止する。
        // Riding: parented to a train car with ThirdPersonController suspended.
        Riding,
    }
}
