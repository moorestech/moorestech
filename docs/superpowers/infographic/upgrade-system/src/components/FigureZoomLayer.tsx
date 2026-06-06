import { useEffect, useState } from 'react';
import { TransformWrapper, TransformComponent, useControls } from 'react-zoom-pan-pinch';

export function FigureZoomLayer() {
  const [openHtml, setOpenHtml] = useState<string | null>(null);

  // クリック委譲: 図要素をタップしたらモーダルを開く
  // Click delegation: tap a figure → open modal
  useEffect(() => {
    const handler = (ev: MouseEvent) => {
      const target = ev.target as HTMLElement | null;
      if (!target) return;
      if (target.closest('button, a, input, textarea, select, [contenteditable]')) return;
      if (target.closest('.figure-zoom-modal')) return;
      const figure = target.closest<HTMLElement>('.mermaid-host, .figure');
      if (!figure) return;
      const svg = figure.querySelector('svg');
      setOpenHtml(svg ? svg.outerHTML : figure.innerHTML);
      ev.preventDefault();
    };
    document.addEventListener('click', handler);
    return () => document.removeEventListener('click', handler);
  }, []);

  // ESC で閉じる + 背景スクロール抑止
  // Close on ESC, lock background scroll while modal is open
  useEffect(() => {
    if (!openHtml) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpenHtml(null); };
    document.addEventListener('keydown', onKey);
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = prev;
    };
  }, [openHtml]);

  if (!openHtml) return null;

  return (
    <div
      className="figure-zoom-modal"
      onClick={(e) => {
        if ((e.target as HTMLElement).classList.contains('figure-zoom-modal')) setOpenHtml(null);
      }}
    >
      <TransformWrapper
        initialScale={1}
        minScale={0.5}
        maxScale={8}
        centerOnInit
        wheel={{ step: 0.18 }}
        doubleClick={{ mode: 'reset' }}
        pinch={{ step: 5 }}
        panning={{ velocityDisabled: true }}
      >
        {() => (
          <>
            <ZoomControls onClose={() => setOpenHtml(null)} />
            <TransformComponent
              wrapperClass="figure-zoom-wrapper"
              contentClass="figure-zoom-content"
            >
              <div
                className="figure-zoom-svg"
                dangerouslySetInnerHTML={{ __html: openHtml }}
              />
            </TransformComponent>
          </>
        )}
      </TransformWrapper>
    </div>
  );
}

function ZoomControls({ onClose }: { onClose: () => void }) {
  const { zoomIn, zoomOut, resetTransform } = useControls();
  return (
    <div className="figure-zoom-ui">
      <div className="figure-zoom-actions">
        <button type="button" onClick={() => zoomOut()} aria-label="縮小">−</button>
        <button type="button" onClick={() => resetTransform()} aria-label="リセット">⤾</button>
        <button type="button" onClick={() => zoomIn()} aria-label="拡大">＋</button>
      </div>
      <button type="button" className="figure-zoom-close" onClick={onClose} aria-label="閉じる">×</button>
      <div className="figure-zoom-hint">ピンチ / ダブルタップ / ホイールで拡大</div>
    </div>
  );
}
