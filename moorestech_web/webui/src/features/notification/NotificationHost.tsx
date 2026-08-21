import { useEffect, useRef } from "react";
import type { CSSProperties } from "react";
import { useTopicEvents, Topics } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import ItemIcon from "@/shared/ui/ItemIcon";
import { NOTIFICATION_DISPLAY_MS, useNotificationStore } from "./notificationStore";
import type { GameNotification } from "./notificationStore";
import { resolveNotificationText } from "./notificationMessages";
import styles from "./style.module.css";

// 通知ホスト。左端縦中央に浮遊テキストで表示
// Notification host; face-less floating text at the left edge, vertically centered
export default function NotificationHost() {
  const notifications = useNotificationStore((s) => s.notifications);
  const removeNotification = useNotificationStore((s) => s.removeNotification);

  // イベント列で受け連続配信を落とさない
  // Received as an event stream so bursts are not dropped
  useTopicEvents(Topics.notification, (payload) => {
    // 接続直後の空snapshotだけ弾く
    // Only the empty snapshot arriving right after connect is dropped
    if (!("seq" in payload)) return;
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

  return (
    <div className={styles.host} data-testid="notification-host">
      {notifications.map((n) => (
        <NotificationRow key={n.id} notification={n} onRemove={removeNotification} />
      ))}
    </div>
  );
}

// 1行分の表示。生存尺を渡し、加算時は同じ要素のままアニメを頭から回し直す
// A single row; it carries the lifetime and replays its animations in place when the count is merged
function NotificationRow({ notification, onRemove }: { notification: GameNotification; onRemove: (id: number) => void }) {
  const { t } = useI18n();
  const rowRef = useRef<HTMLDivElement>(null);
  const { key, values } = resolveNotificationText(notification, t);
  const lifetimeStyle = { "--notification-lifetime": `${NOTIFICATION_DISPLAY_MS}ms` } as CSSProperties;

  // 初回描画の入場は宣言側が回すので、epochが進んだときだけ再生し直す
  // The first enter is driven declaratively, so only an advanced epoch triggers a replay
  useEffect(() => {
    if (notification.lifetimeEpoch === 0) return;
    for (const animation of rowRef.current?.getAnimations() ?? []) {
      animation.cancel();
      animation.play();
    }
  }, [notification.lifetimeEpoch]);

  // categoryはdata属性で表し、色分けはCSSトークンに委ねる
  // Category goes into a data attribute; token-based CSS handles the coloring
  // 退場アニメの終了が除去の合図。生存尺は行ごとに渡し、面の消失と削除を同じ時計へ載せる
  // The exit animation's end signals removal; the lifetime is fed per row so fade-out and delete share one clock
  return (
    <div
      ref={rowRef}
      className={styles.notification}
      style={lifetimeStyle}
      data-testid="notification-row"
      data-category={notification.category}
      onAnimationEnd={(event) => { if (event.animationName === styles.notificationExit) onRemove(notification.id); }}
    >
      {notification.itemId != null && <ItemIcon itemId={notification.itemId} className={styles.icon} />}
      {t(key, values)}
    </div>
  );
}
