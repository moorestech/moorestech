using System;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// 必要素材 1 行（不足は Held < Count で BuildMenuShortage が判定）
    /// One required-material row; shortage is Held < Count, judged by BuildMenuShortage
    /// </summary>
    public sealed class BuildMenuRequiredItem
    {
        public Guid ItemGuid { get; }
        public int Count { get; }
        public int Held { get; }

        public BuildMenuRequiredItem(Guid itemGuid, int count, int held)
        {
            ItemGuid = itemGuid;
            Count = count;
            Held = held;
        }
    }
}
