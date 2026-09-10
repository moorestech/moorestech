import { useLayoutEffect, useRef, useState } from "react";
import type { ClipRect } from "@/shared/tutorialAnchor";
import { readTutorialHighlightLabelGapPx } from "./labelGapToken";
import styles from "./style.module.css";

type Props = { box: ClipRect; clip: ClipRect; text: string; uiScale: number };

// 枠線の外側に置くラベル面。clip-pathを持たないため、収まる側を自分で選んで容器の外へ出ない
// The label face outside the ring; it carries no clip-path, so it picks the side that fits and never leaves the container
export default function HighlightLabel({ box, clip, text, uiScale }: Props) {
  const faceRef = useRef<HTMLDivElement>(null);
  const [face, setFace] = useState({ widthPx: 0, heightPx: 0 });
  // 器より広いラベルは折り返して器に収める。既定は1行のままで、この上限に達した時だけ折り返る
  // A label wider than the container wraps to fit it; it stays on one line until it reaches this limit
  const maxWidthPx = clip.right - clip.left;
  // 面はstage同率で拡大されるため、拡大後に器へ収まるよう折り返し上限は拡大前の長さで与える
  // The face is scaled at the stage's rate, so the wrap limit is given pre-scale to land inside the container after scaling
  const layoutMaxWidthPx = maxWidthPx / uiScale;

  // 寸法は文言・字送り・折り返し上限で決まるため実測する。paint前に確定させ、反転が1フレーム見えないようにする
  // The size depends on the text, its metrics and the wrap limit, so measure it before paint and never show the flip for a frame
  useLayoutEffect(() => {
    const rect = faceRef.current!.getBoundingClientRect();
    setFace({ widthPx: rect.width, heightPx: rect.height });
  }, [text, layoutMaxWidthPx, uiScale]);

  // 枠線との隙間はCSSのmarginではなくここで足す。判定と描画が同じ値を見ないと、収まらない側へ反転する
  // The ring gap is added here rather than by a CSS margin: unless the test and the placement share one value, it flips to the side that does not fit
  const gapPx = readTutorialHighlightLabelGapPx() * uiScale;
  // 既定は枠線の下。下に収まらず上には収まる時だけ枠線の上へ反転する（ユーザー裁定 2026-08-22）
  // Below the ring by default, flipped above only when it does not fit below and does fit above (user ruling 2026-08-22)
  const fitsBelow = box.bottom + gapPx + face.heightPx <= clip.bottom;
  const fitsAbove = box.top - gapPx - face.heightPx >= clip.top;
  const top = fitsBelow || !fitsAbove ? box.bottom + gapPx : box.top - gapPx - face.heightPx;
  // 横は反転先が無いため右端で押し戻す。器より広い時は折り返した幅で左端へ収まる
  // Horizontally there is no side to flip to, so push back from the right edge; wider than the container it settles at the left after wrapping
  const left = Math.max(clip.left, Math.min(box.left, clip.right - face.widthPx));

  return <div ref={faceRef} className={styles.highlightLabel} data-testid="tutorial-highlight-label"
    style={{ left, top, maxWidth: layoutMaxWidthPx }}>
    {text}
  </div>;
}
