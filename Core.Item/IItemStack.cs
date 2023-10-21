using Core.Item.Util;

namespace Core.Item
{
    public interface IItemStack
    {
        int Id { get; }
        int Count { get; }
        ulong ItemHash { get; }


        ///     ID
        ///     
        ///     ID

        long ItemInstanceId { get; }

        ItemProcessResult AddItem(IItemStack receiveItemStack);
        IItemStack SubItem(int subCount);


        ///     true
        ///     falce

        /// <param name="item"></param>
        /// <returns></returns>
        bool IsAllowedToAdd(IItemStack item);


        ///     true
        ///     true
        ///     IDfalse

        /// <returns>true</returns>
        bool IsAllowedToAddWithRemain(IItemStack item);
    }
}