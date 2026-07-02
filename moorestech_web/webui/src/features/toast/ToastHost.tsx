import { Notification, Stack } from "@mantine/core";
import { useToastStore } from "./toastStore";
import styles from "./style.module.css";

// 画面右下にトーストを表示するホスト。自動消滅は store 側（addToast）で管理
// Toast host pinned to the bottom-right; auto-dismiss is handled in the store (addToast)
export default function ToastHost() {
  const toasts = useToastStore((s) => s.toasts);

  return (
    <Stack gap="xs" className={styles.host} data-testid="toast-host">
      {toasts.map((t) => (
        <Notification key={t.id} color="red" withCloseButton={false} withBorder>
          {t.message}
        </Notification>
      ))}
    </Stack>
  );
}
