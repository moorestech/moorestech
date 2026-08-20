import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { Paper, Portal } from "@mantine/core";
import { Topics, useTopic, type TooltipData } from "@/bridge";
import { buildPositionalInterpolationValues, translateExternalKey, useI18n, type InterpolationValues, type TranslationKey } from "@/shared/i18n";
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
      <Paper ref={elementRef} className={styles.tooltip} data-testid="cursor-tooltip" style={{ left: position.x, top: position.y }}>
        {text}
      </Paper>
    </Portal>
  );
}

// ホストはキーと位置パラメータだけを送るため、常に辞書解決＋{p0}補間で表示文字列を作る
// The host sends only a key and positional params, so the display text is always dictionary-resolved and interpolated
export function resolveTooltipText(
  data: TooltipData,
  translate: (key: TranslationKey, values: InterpolationValues) => string,
): string {
  return translateExternalKey(
    data.textKey,
    translate,
    buildPositionalInterpolationValues(data.textParams),
  );
}
