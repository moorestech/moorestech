import { useEffect, useRef } from "react";
import { useTopic, Topics } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import ItemIcon from "@/shared/ui/ItemIcon";
import { useNotificationStore } from "./notificationStore";
import { resolveNotificationTemplate, buildInterpolationValues } from "./notificationMessages";
import styles from "./style.module.css";

// 通知ホスト。左端縦中央に浮遊テキストで表示
// Notification host; face-less floating text at the left edge, vertically centered
export default function NotificationHost() {
  const payload = useTopic(Topics.notification);
  const { t } = useI18n();
  const notifications = useNotificationStore((s) => s.notifications);
  const lastSeq = useRef(0);

  useEffect(() => {
    // 空/重複配信はseqで弾く
    // Guard against empty/duplicate deliveries via seq
    if (!payload?.seq || payload.seq <= lastSeq.current) return;
    if (payload.category === undefined || payload.messageId === undefined || payload.messageParams === undefined) return;
    lastSeq.current = payload.seq;
    useNotificationStore.getState().addNotification({
      category: payload.category,
      messageId: payload.messageId,
      messageParams: payload.messageParams,
      itemId: payload.itemId ?? null,
    });
  }, [payload]);

  return (
    <div className={styles.host} data-testid="notification-host">
      {notifications.map((n) => (
        // categoryはdata属性で表し、色分けはCSSトークンに委ねる
        // Category goes into a data attribute; token-based CSS handles the coloring
        <div key={n.id} className={styles.notification} data-category={n.category}>
          {n.itemId != null && <ItemIcon itemId={n.itemId} className={styles.icon} />}
          {t(resolveNotificationTemplate(n.messageId), buildInterpolationValues(n.messageParams))}
        </div>
      ))}
    </div>
  );
}
