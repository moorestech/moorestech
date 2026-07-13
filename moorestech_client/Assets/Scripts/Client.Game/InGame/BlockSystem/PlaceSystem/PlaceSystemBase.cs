using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    /// 型付きターゲットを受ける設置システム基底。キャストはここで1回だけ行う
    /// Base for place systems with a typed target; the single cast happens here
    /// </summary>
    public abstract class PlaceSystemBase<TTarget> : IPlaceSystem where TTarget : class, IPlacementTarget
    {
        public abstract void Enable();
        public abstract void Disable();

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // Selectorが型を保証する。違えば即例外＝振り分けバグ
            // The selector guarantees the type; a mismatch throws immediately = routing bug
            ManualUpdate((TTarget)context.Target, context.IsSelectionChanged);
        }

        protected abstract void ManualUpdate(TTarget target, bool isSelectionChanged);
    }
}
