import { useLayoutEffect, useRef, useState } from "react";
import type { ClipRect } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

type Props = { box: ClipRect; clip: ClipRect; text: string };

// 枠線の外側に置くラベル面。clip-pathを持たないため、収まる側を自分で選んで容器の外へ出ない
// The label face outside the ring; it carries no clip-path, so it picks the side that fits and never leaves the container
export default function HighlightLabel({ box, clip, text }: Props) {
  const faceRef = useRef<HTMLDivElement>(null);
  const [heightPx, setHeightPx] = useState(0);

  // 高さは文言と字送りで決まるため実測する。paint前に確定させ、反転が1フレーム見えないようにする
  // The height depends on the text and its metrics, so measure it before paint and never show the flip for a frame
  useLayoutEffect(() => {
    setHeightPx(faceRef.current!.getBoundingClientRect().height);
  }, [text]);

  // 既定は枠線の下。下に収まらず上には収まる時だけ枠線の上へ反転する（ユーザー裁定 2026-08-22）
  // Below the ring by default, flipped above only when it does not fit below and does fit above (user ruling 2026-08-22)
  const fitsBelow = box.bottom + heightPx <= clip.bottom;
  const fitsAbove = box.top - heightPx >= clip.top;
  const top = fitsBelow || !fitsAbove ? box.bottom : box.top - heightPx;

  return <div ref={faceRef} className={styles.highlightLabel} data-testid="tutorial-highlight-label"
    style={{ left: box.left, top }}>
    {text}
  </div>;
}
