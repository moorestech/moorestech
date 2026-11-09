namespace Client.Game.InGame.UI.Inventory.Main
{
    public enum LocalMoveInventoryType
    {
        MainOrSub, //メインインベントリとサブインベントリの両方（ドラッグアンドドロップなどでは統一して扱うから
        Grab, //持ち手のインベントリ
        Equipment, //装備インベントリ（slotはそのまま装備スロット番号）
    }
}
