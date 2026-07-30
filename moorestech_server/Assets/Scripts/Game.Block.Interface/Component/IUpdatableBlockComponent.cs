namespace Game.Block.Interface.Component
{
    public interface IUpdatableBlockComponent : IBlockComponent
    {
        public void Update();
    }

    // 共通tickループ（MasterTickUpdaterのブロック更新）へ合流せず、自前のUpdate駆動を維持するコンポーネントが宣言する
    // 対象は内部でアイテム位置が時間進行するベルトコンベア系のみ。segment化での一元化までの暫定契約
    // Declared by components that keep their own update drive instead of joining the central tick loop.
    // Only belt conveyors, whose item positions advance over time internally, qualify; interim contract until segmentation unifies them
    public interface ISelfDrivenUpdatableBlockComponent : IUpdatableBlockComponent
    {
    }
}
