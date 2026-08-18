import { useRef } from "react";
import type { CSSProperties } from "react";
import { useTopicEvents, Topics } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import ItemIcon from "@/shared/ui/ItemIcon";
import { NOTIFICATION_DISPLAY_MS, useNotificationStore } from "./notificationStore";
import { resolveNotificationKey, resolveNotificationParams, buildInterpolationValues } from "./notificationMessages";
import styles from "./style.module.css";

// 通知ホスト。左端縦中央に浮遊テキストで表示
// Notification host; face-less floating text at the left edge, vertically centered
export default function NotificationHost() {
  const { t } = useI18n();
  const notifications = useNotificationStore((s) => s.notifications);
  const removeNotification = useNotificationStore((s) => s.removeNotification);
  const lastSeq = useRef(0);

  // 最新値ではなくイベント列として受ける。同一レンダー内に連続で届いても取りこぼさない
  // Received as an event stream rather than a latest value, so bursts inside one render are not dropped
  useTopicEvents(Topics.notification, (payload) => {
    // 空スナップショットと重複配信はseqで弾く
    // Guard against the empty snapshot and duplicate deliveries via seq
    if (!("seq" in payload) || payload.seq <= lastSeq.current) return;
    lastSeq.current = payload.seq;
    if (payload.category === "itemEarned") {
      useNotificationStore.getState().addNotification({
        category: "itemEarned",
        messageId: payload.messageId,
        messageParams: payload.messageParams,
        itemId: payload.itemId,
        count: payload.count,
      });
      return;
    }
    useNotificationStore.getState().addNotification({
      category: payload.category,
      messageId: payload.messageId,
      messageParams: payload.messageParams,
      itemId: payload.itemId ?? null,
    });
  });

  const lifetimeStyle = { "--notification-lifetime": `${NOTIFICATION_DISPLAY_MS}ms` } as CSSProperties;

  return (
    <div className={styles.host} data-testid="notification-host">
      {notifications.map((n) => (
        // categoryはdata属性で表し、色分けはCSSトークンに委ねる
        // Category goes into a data attribute; token-based CSS handles the coloring
        // 退場アニメの終了が除去の合図。生存尺は行ごとに渡し、面の消失と削除を同じ時計へ載せる
        // The exit animation's end signals removal; the lifetime is fed per row so fade-out and delete share one clock
        <div
          key={n.id}
          className={styles.notification}
          style={lifetimeStyle}
          data-testid="notification-row"
          data-category={n.category}
          onAnimationEnd={(event) => { if (event.animationName === styles.notificationExit) removeNotification(n.id); }}
        >
          {n.itemId != null && <ItemIcon itemId={n.itemId} className={styles.icon} />}
          {t(
            resolveNotificationKey(n.messageId),
            buildInterpolationValues(n.messageId, resolveNotificationParams(n.messageId, n.messageParams, t), n.category === "itemEarned" ? n.count : null),
          )}
        </div>
      ))}
    </div>
  );
}
