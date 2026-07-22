import { useEffect, useRef } from "react";
import { Notification, Stack } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import ItemIcon from "@/shared/ui/ItemIcon";
import { useNotificationStore } from "./notificationStore";
import { resolveNotificationTemplate, buildInterpolationValues } from "./notificationMessages";
import styles from "./style.module.css";

// 通知ホスト。右上に表示
// Notification host; displayed top-right
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
    <Stack gap="xs" className={styles.host} data-testid="notification-host">
      {notifications.map((n) => (
        // categoryで色分け
        // Color by category
        <Notification
          key={n.id}
          color={n.category === "operationDenied" ? "yellow" : "teal"}
          icon={n.itemId != null ? <ItemIcon itemId={n.itemId} /> : undefined}
          withCloseButton={false}
          withBorder
        >
          {t(resolveNotificationTemplate(n.messageId), buildInterpolationValues(n.messageParams))}
        </Notification>
      ))}
    </Stack>
  );
}
