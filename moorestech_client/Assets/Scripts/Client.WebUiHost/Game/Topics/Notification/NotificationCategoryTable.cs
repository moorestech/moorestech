using System;
using Server.Event.Notification;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// NotificationCategoryとWeb側category名の唯一の対応表
    /// The only mapping between NotificationCategory and the web-side category names
    /// </summary>
    public static class NotificationCategoryTable
    {
        public static bool TryGetWebName(NotificationCategory category, out string name)
        {
            // 未定義値はWebへ渡さず落とす。定義済み値はdefault無しswitchなので追加漏れがCS8509で見える
            // Undefined values are dropped instead of forwarded; the default-less switch surfaces a missing member as CS8509
            if (!Enum.IsDefined(typeof(NotificationCategory), category))
            {
                name = null;
                return false;
            }

            name = category switch
            {
                NotificationCategory.Achievement => "achievement",
                NotificationCategory.OperationDenied => "operationDenied",
                NotificationCategory.ItemEarned => "itemEarned",
            };
            return true;
        }
    }
}
