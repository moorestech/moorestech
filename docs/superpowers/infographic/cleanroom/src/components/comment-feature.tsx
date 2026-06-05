/* ============================================================
   コメント機能 — 単一ファイル版 / Review comment feature (single file)
   ------------------------------------------------------------
   縦スクロール型インフォグラフィックに、本文の選択範囲・図へのコメントと
   全コメントの Markdown 一括コピーを追加する。

   このファイル1つ（+ 同フォルダ comment-feature.css）で完結する。
   使い方は同フォルダの README.md を参照。

   エクスポート:
   - <CommentLayer />  … App.tsx の先頭で1回描画する
   - <Mermaid chart label />  … project-setup.md の Mermaid.tsx の代わりに使う
   - <FigureFrame label>  … Mermaid 以外の図（手書き SVG 等）を包む

   このファイル単体ではコンパイルエラー（Cannot find module 'react' 等）が出るが
   正常 — Vite + React プロジェクトへコピーすれば解決する。
   ============================================================ */

import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import mermaid from 'mermaid';

// ===== 設定 / CONFIG ===========================================
// プロジェクトごとに変えてよい2項目。コピー後にここだけ書き換える。
// Two values you may change per project. Edit only these after copying.
const STORAGE_KEY = 'infographic-review-comments'; // localStorage キー / localStorage key
const COPY_TITLE = '図解レビューコメント'; //「すべてコピー」する Markdown の見出し / heading of the copied Markdown
// ===============================================================

const HIGHLIGHT_NAME = 'review-comment';

/* ---------- 共通アイコン / shared icons ---------- */

// 吹き出しの線画アイコン
// Line-art speech-bubble icon
function CommentIcon({ size = 15 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M21 12a8 8 0 0 1-11.5 7.2L4 20l1-4.5A8 8 0 1 1 21 12Z"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinejoin="round"
      />
    </svg>
  );
}

// 鉛筆（編集）アイコン
// Pencil (edit) icon
function PencilIcon() {
  return (
    <svg width="11" height="11" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M4 20h4L18.5 9.5a2.1 2.1 0 0 0-3-3L5 17v3Z"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/* ---------- Mermaid ラッパ（label 対応） / Mermaid wrapper ---------- */

// Mermaid のテーマ初期化はモジュールロード時に1回だけ
// Initialize the Mermaid theme once at module load time
mermaid.initialize({
  startOnLoad: false,
  theme: 'base',
  fontFamily: '-apple-system, "Hiragino Sans", system-ui, sans-serif',
  themeVariables: {
    primaryColor: '#FFFFFF',
    primaryTextColor: '#0F172A',
    primaryBorderColor: '#CBD5E1',
    lineColor: '#475569',
    fontSize: '14px',
    actorBkg: '#FFFFFF',
    actorBorder: '#1E293B',
    signalColor: '#334155',
    labelBoxBkgColor: '#F8FAFC',
    noteBkgColor: '#FEF3C7',
    noteBorderColor: '#F59E0B',
    activationBkgColor: '#EFF6FF',
    activationBorderColor: '#2563EB',
    sequenceNumberColor: '#FFFFFF',
  },
  sequence: { actorMargin: 90, wrap: true },
  flowchart: { curve: 'basis', htmlLabels: true },
});

let mermaidCounter = 0;
// label: その図が実際に表示している中身を自然言語で簡潔に書いた一文（必須運用）。
//        図の種類・外枠ではなく中身そのもの（シーケンスなら実際の手順）を書く。
// label: a natural-language sentence describing the ACTUAL CONTENT the figure shows.
type MermaidProps = { chart: string; id?: string; label?: string };

export function Mermaid({ chart, id, label }: MermaidProps) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!ref.current) return;
    const uid = id ?? `m-${++mermaidCounter}`;
    mermaid
      .render(uid, chart)
      .then(({ svg }) => {
        if (ref.current) ref.current.innerHTML = svg;
      })
      .catch((err) => {
        if (ref.current) ref.current.textContent = String(err);
      });
  }, [chart, id]);

  const host = <div className="mermaid-host" ref={ref} />;
  // label があれば図コメント用のフレームで包む
  // Wrap in a figure frame so the whole diagram can be commented on
  return label ? <FigureFrame label={label}>{host}</FigureFrame> : host;
}

/* ---------- FigureFrame（図を1単位にする） / FigureFrame ---------- */

// 図コメント要求を CommentLayer へ伝える CustomEvent の detail
// Detail of the CustomEvent that tells CommentLayer a figure comment was requested
type FigureCommentDetail = { figureKey: string; label: string; rect: DOMRect };

// 図（Mermaid / 手書き SVG など）を1単位として扱い「この図にコメント」ボタンを添える。
// label は「その図が実際に表示している中身」を自然言語で簡潔に書いた一文にすること。
// 図の種類・外枠（「〜を表したシーケンス図」等）ではなく、中身そのもの（シーケンスなら実際の手順、
// 状態遷移図なら実際の状態と遷移）を記述する。この label が data-figure-key・コメントの引用・
// コピー後の Markdown にそのまま使われる。
// Treats a figure as one unit. The label MUST describe the ACTUAL CONTENT the figure shows
// (real steps of a sequence, real states/transitions of a state diagram), not the figure type.
export function FigureFrame({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="figure" data-figure-key={label}>
      <button
        className="figure-comment-btn"
        data-comment-ui
        onClick={(e) => {
          const fig = e.currentTarget.parentElement as HTMLElement;
          const detail: FigureCommentDetail = {
            figureKey: label,
            label,
            rect: fig.getBoundingClientRect(),
          };
          document.dispatchEvent(new CustomEvent('figure-comment', { detail }));
        }}
      >
        <CommentIcon size={13} />
        この図にコメント
      </button>
      {children}
    </div>
  );
}

/* ---------- CommentLayer（本体） / CommentLayer ---------- */

// 選択範囲または図に紐づく1件のレビューコメント
// A single review comment bound to a text selection or a figure
type ReviewComment = {
  id: string;
  kind: 'text' | 'figure';
  quote: string; // text: 選択テキスト / figure: 図ラベル
  body: string;
  createdAt: number;
  figureKey?: string; // kind === 'figure' のとき、対象図の data-figure-key
};

type Anchor = { x: number; y: number };
type Pending = {
  anchor: Anchor;
  place: 'above' | 'below';
  quote: string;
  kind: 'text' | 'figure';
  figureKey?: string;
};

// localStorage からコメント一覧を復元（外部データなので防御的に読む）
// Restore comments from localStorage (defensive read for external data)
function loadComments(): ReviewComment[] {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) return [];
  try {
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as ReviewComment[]) : [];
  } catch {
    return [];
  }
}

export function CommentLayer() {
  const [comments, setComments] = useState<ReviewComment[]>(loadComments);
  const [panelOpen, setPanelOpen] = useState(false);
  const [copied, setCopied] = useState(false);

  const [pending, setPending] = useState<Pending | null>(null);
  const [composerOpen, setComposerOpen] = useState(false);
  const [draftBody, setDraftBody] = useState('');

  // 既存コメントのインライン編集
  // Inline editing of an existing comment
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editBody, setEditBody] = useState('');

  const draftRange = useRef<Range | null>(null);
  const rangeMap = useRef<Map<string, Range>>(new Map());

  // テキストコメントの Range を1つの Highlight にまとめて再登録する
  // Rebuild the single Highlight from every text-comment range
  const refreshHighlights = useCallback(() => {
    const cssAny = CSS as unknown as { highlights?: Map<string, unknown> };
    const HighlightCtor = (window as unknown as { Highlight?: new () => { add(r: Range): void } }).Highlight;
    if (!cssAny.highlights || !HighlightCtor) return;
    const hl = new HighlightCtor();
    for (const r of rangeMap.current.values()) hl.add(r);
    cssAny.highlights.set(HIGHLIGHT_NAME, hl as unknown);
  }, []);

  // 保存済みコメントを localStorage へ書き出す
  // Persist comments to localStorage
  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(comments));
  }, [comments]);

  // コメント済みの図に印を付ける
  // Mark figures that already have a comment
  useEffect(() => {
    const keyed = new Set(comments.filter((c) => c.kind === 'figure').map((c) => c.figureKey));
    document.querySelectorAll('[data-figure-key]').forEach((el) => {
      if (keyed.has(el.getAttribute('data-figure-key') ?? '')) el.setAttribute('data-has-comment', '');
      else el.removeAttribute('data-has-comment');
    });
  }, [comments]);

  // テキスト選択を監視してフローティングボタンを出す
  // Watch text selection and surface the floating button
  useEffect(() => {
    function onMouseUp(e: MouseEvent) {
      const target = e.target;
      if (target instanceof Element && target.closest('[data-comment-ui]')) return;

      const sel = window.getSelection();
      const text = sel?.toString().trim() ?? '';
      if (!sel || sel.isCollapsed || !text) {
        if (!composerOpen) setPending(null);
        return;
      }
      const range = sel.getRangeAt(0);
      const rect = range.getBoundingClientRect();
      draftRange.current = range.cloneRange();
      // ドキュメント座標で保持し、スクロールしても選択範囲に追従させる
      // Store document-space coords so the popup tracks the selection on scroll
      setPending({
        anchor: { x: rect.left + window.scrollX + rect.width / 2, y: rect.top + window.scrollY },
        place: 'above',
        quote: text,
        kind: 'text',
      });
      setComposerOpen(false);
      setDraftBody('');
    }
    document.addEventListener('mouseup', onMouseUp);
    return () => document.removeEventListener('mouseup', onMouseUp);
  }, [composerOpen]);

  // 図コメント要求（FigureFrame からの CustomEvent）を受けて入力フォームを開く
  // Open the composer on a figure comment request from FigureFrame
  useEffect(() => {
    function onFigureComment(e: Event) {
      const d = (e as CustomEvent<FigureCommentDetail>).detail;
      draftRange.current = null;
      // ドキュメント座標で保持し、スクロールしても図に追従させる
      // Store document-space coords so the popup tracks the figure on scroll
      setPending({
        anchor: {
          x: d.rect.left + window.scrollX + d.rect.width / 2,
          y: d.rect.top + window.scrollY + 44,
        },
        place: 'below',
        quote: d.label,
        kind: 'figure',
        figureKey: d.figureKey,
      });
      setComposerOpen(true);
      setDraftBody('');
    }
    document.addEventListener('figure-comment', onFigureComment);
    return () => document.removeEventListener('figure-comment', onFigureComment);
  }, []);

  function closeComposer() {
    setPending(null);
    setComposerOpen(false);
    setDraftBody('');
  }

  function saveComment() {
    const body = draftBody.trim();
    if (!pending || !body) return;
    const id = `c-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
    if (pending.kind === 'text' && draftRange.current) {
      rangeMap.current.set(id, draftRange.current);
      refreshHighlights();
    }
    setComments((prev) => [
      ...prev,
      {
        id,
        kind: pending.kind,
        quote: pending.quote,
        body,
        createdAt: Date.now(),
        figureKey: pending.figureKey,
      },
    ]);
    closeComposer();
    setPanelOpen(true);
    window.getSelection()?.removeAllRanges();
  }

  function deleteComment(id: string) {
    rangeMap.current.delete(id);
    refreshHighlights();
    setComments((prev) => prev.filter((c) => c.id !== id));
    if (editingId === id) setEditingId(null);
  }

  function saveEdit() {
    if (!editingId) return;
    const body = editBody.trim();
    setComments((prev) => prev.map((c) => (c.id === editingId && body ? { ...c, body } : c)));
    setEditingId(null);
    setEditBody('');
  }

  // コメント項目クリックで対象（選択範囲 / 図）までスクロール
  // Clicking a comment scrolls to its target (text range or figure)
  function jumpTo(c: ReviewComment) {
    if (c.kind === 'figure' && c.figureKey) {
      const fig = document.querySelector(`[data-figure-key="${c.figureKey}"]`);
      fig?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      return;
    }
    const el = rangeMap.current.get(c.id)?.startContainer.parentElement;
    el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  // 全コメントを Markdown にまとめてクリップボードへ
  // Collect all comments into Markdown and copy to clipboard
  function copyAll() {
    if (comments.length === 0) return;
    const text = [
      `# ${COPY_TITLE}（${comments.length}件）`,
      '',
      ...comments.flatMap((c, i) => [
        `## コメント ${i + 1}（${c.kind === 'figure' ? '図' : '本文'}）`,
        `> ${c.quote.replace(/\n+/g, ' ')}`,
        '',
        c.body,
        '',
      ]),
    ].join('\n');

    const done = () => {
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    };
    if (navigator.clipboard?.writeText) {
      navigator.clipboard.writeText(text).then(done, () => fallbackCopy(text, done));
    } else {
      fallbackCopy(text, done);
    }
  }

  const anchorTransform =
    pending?.place === 'below' ? 'translate(-50%, 10px)' : 'translate(-50%, calc(-100% - 9px))';

  return (
    <div data-comment-ui>
      {/* 選択直後のフローティング「コメント追加」ボタン（テキスト選択のみ） */}
      {pending && pending.kind === 'text' && !composerOpen && (
        <button
          className="comment-fab"
          style={{ left: pending.anchor.x, top: pending.anchor.y, transform: anchorTransform }}
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => setComposerOpen(true)}
        >
          <CommentIcon />
          コメントを追加
        </button>
      )}

      {/* コメント入力フォーム */}
      {pending && composerOpen && (
        <div
          className="comment-composer"
          style={{ left: pending.anchor.x, top: pending.anchor.y, transform: anchorTransform }}
          onMouseDown={(e) => e.stopPropagation()}
        >
          <div className="composer-quote">
            {pending.kind === 'figure' && <span className="kind-pill fig">図</span>}
            「{pending.quote}」
          </div>
          <textarea
            autoFocus
            className="composer-input"
            placeholder="この範囲へのコメントを入力…（⌘/Ctrl + Enter で保存）"
            value={draftBody}
            onChange={(e) => setDraftBody(e.target.value)}
            onKeyDown={(e) => {
              if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') saveComment();
              if (e.key === 'Escape') closeComposer();
            }}
          />
          <div className="composer-actions">
            <button className="btn-ghost" onClick={closeComposer}>
              キャンセル
            </button>
            <button className="btn-primary" disabled={!draftBody.trim()} onClick={saveComment}>
              保存
            </button>
          </div>
        </div>
      )}

      {/* 左下のコメントパネル */}
      <div className={`comment-panel ${panelOpen ? 'open' : ''}`}>
        {panelOpen && (
          <div className="panel-body">
            {comments.length === 0 ? (
              <p className="panel-empty">
                本文中のテキストを選択すると「コメントを追加」ボタンが出ます。
                図は右上の「この図にコメント」から。書いたコメントはここに集まります。
              </p>
            ) : (
              <ul className="comment-list">
                {comments.map((c, i) => (
                  <li
                    key={c.id}
                    className="comment-item"
                    onClick={() => editingId !== c.id && jumpTo(c)}
                    title="クリックで対象へジャンプ"
                  >
                    <div className="ci-head">
                      <span className="ci-num">#{i + 1}</span>
                      <span className={`kind-pill ${c.kind === 'figure' ? 'fig' : 'txt'}`}>
                        {c.kind === 'figure' ? '図' : '本文'}
                      </span>
                      {editingId !== c.id && (
                        <button
                          className="ci-act"
                          title="コメントを編集"
                          onClick={(e) => {
                            e.stopPropagation();
                            setEditingId(c.id);
                            setEditBody(c.body);
                          }}
                        >
                          <PencilIcon />
                          編集
                        </button>
                      )}
                      <button
                        className="ci-del"
                        title="削除"
                        onClick={(e) => {
                          e.stopPropagation();
                          deleteComment(c.id);
                        }}
                      >
                        ×
                      </button>
                    </div>
                    <div className="ci-quote">「{c.quote}」</div>
                    {editingId === c.id ? (
                      <div onClick={(e) => e.stopPropagation()}>
                        <div className="ci-edit-label">コメントを編集</div>
                        <textarea
                          autoFocus
                          className="composer-input ci-edit"
                          value={editBody}
                          onChange={(e) => setEditBody(e.target.value)}
                          onKeyDown={(e) => {
                            if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') saveEdit();
                            if (e.key === 'Escape') setEditingId(null);
                          }}
                        />
                        <div className="composer-actions">
                          <button className="btn-ghost" onClick={() => setEditingId(null)}>
                            キャンセル
                          </button>
                          <button
                            className="btn-primary"
                            disabled={!editBody.trim()}
                            onClick={saveEdit}
                          >
                            更新
                          </button>
                        </div>
                      </div>
                    ) : (
                      <div className="ci-body">{c.body}</div>
                    )}
                  </li>
                ))}
              </ul>
            )}

            <div className="panel-foot">
              <button
                className="btn-primary wide"
                disabled={comments.length === 0}
                onClick={copyAll}
              >
                {copied ? 'コピーしました' : `すべてコピー（${comments.length}件）`}
              </button>
            </div>
          </div>
        )}

        <button className="panel-toggle" onClick={() => setPanelOpen((v) => !v)}>
          <CommentIcon />
          コメント
          <span className="panel-count">{comments.length}</span>
          <span className="panel-caret">{panelOpen ? '▾' : '▴'}</span>
        </button>
      </div>
    </div>
  );
}

// 一時 textarea を使った旧来のコピー手段（Clipboard API 不可時）
// Legacy copy via a temporary textarea, used when the Clipboard API is unavailable
function fallbackCopy(text: string, done: () => void) {
  const ta = document.createElement('textarea');
  ta.value = text;
  ta.style.position = 'fixed';
  ta.style.opacity = '0';
  document.body.appendChild(ta);
  ta.select();
  document.execCommand('copy');
  document.body.removeChild(ta);
  done();
}
