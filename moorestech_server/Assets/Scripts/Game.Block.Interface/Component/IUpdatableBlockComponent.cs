namespace Game.Block.Interface.Component
{
    public interface IUpdatableBlockComponent : IBlockComponent
    {
        public void Update();
    }

    // 共通tickループ（MasterTickUpdaterのブロック更新）へ合流せず、自前のUpdate駆動を維持するコンポーネントが宣言する
    // 更新順序の意味論が搬送方向に依存するもの（ベルトコンベア系等）の一元化が済むまでの暫定契約
    // Declared by components that keep their own update drive instead of joining the central tick loop.
    // Interim contract until transport-order-sensitive components (belt conveyors etc.) are unified as well
    public interface ISelfDrivenUpdatableBlockComponent : IUpdatableBlockComponent
    {
    }
}
