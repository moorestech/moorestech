import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { Paper, Portal } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { clampTooltipPosition } from "./tooltipPosition";
import styles from "./style.module.css";

export function CursorTooltip() {
  const data = useTopic(Topics.tooltip);
  const { t } = useI18n();
  const elementRef = useRef<HTMLDivElement>(null);
  const [pointer, setPointer] = useState({ x: 0, y: 0 });
  const [position, setPosition] = useState({ x: 12, y: 12 });

  useEffect(() => {
    const move = (event: PointerEvent) => setPointer({ x: event.clientX, y: event.clientY });
    window.addEventListener("pointermove", move);
    return () => window.removeEventListener("pointermove", move);
  }, []);

  useLayoutEffect(() => {
    const element = elementRef.current;
    if (!element) return;
    setPosition(clampTooltipPosition(pointer.x, pointer.y, element.offsetWidth, element.offsetHeight, window.innerWidth, window.innerHeight));
  }, [pointer, data]);

  if (!data?.visible) return null;
  const text = t(data.textKey);
  return (
    <Portal>
      <Paper ref={elementRef} className={styles.tooltip} style={{ left: position.x, top: position.y, fontSize: data.fontSize }}>
        {text}
      </Paper>
    </Portal>
  );
}
