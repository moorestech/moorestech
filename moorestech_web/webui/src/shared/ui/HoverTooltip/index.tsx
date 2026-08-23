import { cloneElement, useEffect, useRef, useState, type ReactElement, type ReactNode, type Ref } from "react";
import { Tooltip } from "@mantine/core";
import styles from "./style.module.css";

type Props = {
  label: ReactNode;
  disabled?: boolean;
  children: ReactElement<{ ref?: Ref<HTMLElement> }>;
};

// スロット共通のホバーツールチップ。Portalへ出るためスクロール祖先のクリップが一切効かず、
// 放置すると内容と一緒に滑ってパネルの外まで出ていく。祖先がスクロールしたら引っ込める契約にする
// The shared slot hover tooltip. It renders into a Portal, so no scrolling ancestor clips it and, left alone,
// it slides along with the content and out of the panel; the contract is to retract once an ancestor scrolls
export default function HoverTooltip({ label, disabled, children }: Props) {
  const targetRef = useRef<HTMLElement | null>(null);
  const [hovering, setHovering] = useState(false);
  const [dismissed, setDismissed] = useState(false);

  // openedを自前で持つ。disabledで畳むとMantineのホバー状態ごと消え、
  // スクロール後にポインタが同じセルへ載ったままだと二度と開かなくなる
  // Own the opened state: collapsing via disabled also drops Mantine's hover state, so a pointer left
  // resting on the same cell after a scroll would never reopen
  useEffect(() => {
    const node = targetRef.current;
    if (!node) return;
    // 実際に動いた時だけ復帰させる。スクロール中にブラウザが出す座標据え置きのmousemoveで
    // 引っ込めた直後に開き直るのを防ぐ
    // Revive only on real movement, so the stationary mousemove the browser emits during a scroll
    // does not reopen the tooltip the instant it retracts
    let lastX = -1;
    let lastY = -1;
    const enter = (event: MouseEvent) => {
      lastX = event.clientX;
      lastY = event.clientY;
      setHovering(true);
      setDismissed(false);
    };
    const move = (event: MouseEvent) => {
      if (event.clientX === lastX && event.clientY === lastY) return;
      lastX = event.clientX;
      lastY = event.clientY;
      setDismissed(false);
    };
    const leave = () => setHovering(false);
    node.addEventListener("mouseenter", enter);
    node.addEventListener("mousemove", move);
    node.addEventListener("mouseleave", leave);
    return () => {
      node.removeEventListener("mouseenter", enter);
      node.removeEventListener("mousemove", move);
      node.removeEventListener("mouseleave", leave);
    };
  }, []);

  // 購読はホバー中だけ。captureで拾うのはスクロールがバブルしないため
  // Subscribe only while hovering; capture is required because scroll does not bubble
  useEffect(() => {
    if (!hovering) return;
    // 引っ込めるのはターゲットを含む器がスクロールした時だけ。無関係な別パネルの
    // スクロールでは引っ込めない（documentはターゲットを含むので頁ごとの移動は対象）
    // Retract only when a container holding the target scrolls, never when an unrelated
    // panel does; document holds the target, so a page-level scroll still counts
    const dismiss = (event: Event) => {
      const node = targetRef.current;
      if (!node) return;
      if (!(event.target instanceof Node) || !event.target.contains(node)) return;
      setDismissed(true);
    };
    document.addEventListener("scroll", dismiss, true);
    return () => document.removeEventListener("scroll", dismiss, true);
  }, [hovering]);

  return (
    <Tooltip classNames={{ tooltip: styles.tooltip }} label={label} disabled={disabled} opened={hovering && !dismissed}>
      {cloneElement(children, { ref: targetRef })}
    </Tooltip>
  );
}
