namespace Client.Game.InGame.BeltTransport.Cover
{
    // 合流マス1つの覆い。ベルコンの BlockGameObject が破棄されると IsAlive が false になる
    // The cover of one merge cell; IsAlive turns false once the belt's BlockGameObject is destroyed
    public interface IBeltMergeCellCover
    {
        bool IsAlive { get; }
        void Remove();
    }
}
