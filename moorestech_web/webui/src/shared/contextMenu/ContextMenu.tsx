import { useEffect, useState } from "react";
import { Button, Paper, Portal, Stack } from "@mantine/core";
import { dispatchAction, Topics, useTopic } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { tutorialAnchor } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";
import type { ContextMenuData } from "@/bridge";

export function ContextMenu() {
  const data = useTopic(Topics.contextMenu);
  const { t } = useI18n();
  const [position, setPosition] = useState({ x: 0, y: 0 });

  useEffect(() => {
    const move = (event: PointerEvent) => setPosition({ x: event.clientX, y: event.clientY });
    window.addEventListener("pointermove", move);
    return () => window.removeEventListener("pointermove", move);
  }, []);

  useEffect(() => {
    if (!data?.visible) return;
    const close = () => void dispatchAction("context_menu.close", {});
    window.addEventListener("pointerdown", close);
    return () => window.removeEventListener("pointerdown", close);
  }, [data?.visible]);

  if (!data?.visible) return null;
  return (
    <Portal>
      <Paper className={styles.menu} style={{ left: position.x, top: position.y }} onPointerDown={(event) => event.stopPropagation()} {...tutorialAnchor("context.menu")}>
        <Stack gap={2}>
          {data.items.map((item) => (
            <ContextMenuButton key={item.id} item={item} translate={t} />
          ))}
        </Stack>
      </Paper>
    </Portal>
  );
}

function ContextMenuButton({ item, translate }: {
  item: ContextMenuData["items"][number];
  translate: (key: string) => string;
}) {
  const label = translate(item.titleKey);
  const variant = "subtle";
  const select = () => void dispatchAction("context_menu.select", { id: item.id });
  return <Button variant={variant} onClick={select}>{label}</Button>;
}
