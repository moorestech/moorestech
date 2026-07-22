import { useEffect, useRef } from "react";
import { Notification, Stack } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import ItemIcon from "@/shared/ui/ItemIcon";
import { useNotificationStore } from "./notificationStore";
import { resolveNotificationTemplate, buildInterpolationValues } from "./notificationMessages";
import styles from "./style.module.css";

// 実績・操作拒否の通知ホスト。画面右上に表示しトーストと重ならない
// Host for achievement / operation-denied notifications; top-right, kept clear of the toast
export default function NotificationHost() {
  const payload = useTopic(Topics.notification);
  const { t } = useI18n();
  const notifications = useNotificationStore((s) => s.notifications);
  const lastSeq = useRef(0);

  useEffect(() => {
    // snapshotの空オブジェクトや重複配信はseqで弾く
    // Guard against the empty snapshot object and duplicate deliveries via seq
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
        // category で色分け（operationDenied=黄 / achievement=teal）
        // Color by category (operationDenied=yellow / achievement=teal)
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
