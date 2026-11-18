namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// 1 コストで複数個置けるブロックの 1 セット設置。無いことは BuildMenuSetPlacementNone.Instance で表し、使う側は switch で分ける
    /// Set placement for blocks placed several per cost; absence is BuildMenuSetPlacementNone.Instance and callers branch with switch
    /// </summary>
    public abstract class BuildMenuSetPlacement
    {
        private protected BuildMenuSetPlacement()
        {
        }
    }

    public sealed class BuildMenuSetPlacementNone : BuildMenuSetPlacement
    {
        public static readonly BuildMenuSetPlacementNone Instance = new();

        private BuildMenuSetPlacementNone()
        {
        }
    }

    public sealed class BuildMenuSetPlacementAvailable : BuildMenuSetPlacement
    {
        public int PerCost { get; }
        public int Remaining { get; }

        public BuildMenuSetPlacementAvailable(int perCost, int remaining)
        {
            PerCost = perCost;
            Remaining = remaining;
        }
    }
}
