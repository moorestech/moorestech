using System;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// 必要素材 1 行（不足は作る側が確定）
    /// One required-material row; shortage is settled by the producer
    /// </summary>
    public sealed class BuildMenuRequiredItem
    {
        public Guid ItemGuid { get; }
        public int Count { get; }
        public int Held { get; }
        public bool Lacking { get; }

        public BuildMenuRequiredItem(Guid itemGuid, int count, int held, bool lacking)
        {
            ItemGuid = itemGuid;
            Count = count;
            Held = held;
            Lacking = lacking;
        }
    }
}
