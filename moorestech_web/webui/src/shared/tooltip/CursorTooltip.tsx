import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { Paper, Portal } from "@mantine/core";
import { Topics, useTopic, type TooltipData } from "@/bridge";
import { translateExternalKey, useI18n, type TranslationKey } from "@/shared/i18n";
import { clampTooltipPosition } from "./tooltipPosition";
import styles from "./style.module.css";

export function CursorTooltip() {
  const data = useTopic(Topics.tooltip);
  const { locale, t } = useI18n();
  const elementRef = useRef<HTMLDivElement>(null);
  const [pointer, setPointer] = useState({ x: 0, y: 0 });
  const [position, setPosition] = useState({ x: 12, y: 12 });

  useEffect(() => {
    const move = (event: PointerEvent) => setPointer({ x: event.clientX, y: event.clientY });
    window.addEventListener("pointermove", move);
    return () => window.removeEventListener("pointermove", move);
  }, []);

  const text = data?.visible ? resolveTooltipText(data, t) : "";

  useLayoutEffect(() => {
    const element = elementRef.current;
    if (!element) return;
    setPosition(clampTooltipPosition(pointer.x, pointer.y, element.offsetWidth, element.offsetHeight, window.innerWidth, window.innerHeight));
  }, [pointer, data, text, locale]);

  if (!data?.visible) return null;
  return (
    <Portal>
      <Paper ref={elementRef} className={styles.tooltip} style={{ left: position.x, top: position.y, fontSize: data.fontSize }}>
        {text}
      </Paper>
    </Portal>
  );
}

export function resolveTooltipText(
  data: TooltipData,
  translate: (key: TranslationKey) => string,
): string {
  if (!data.isLocalize) return data.textKey;
  return translateExternalKey(data.textKey, translate);
}
