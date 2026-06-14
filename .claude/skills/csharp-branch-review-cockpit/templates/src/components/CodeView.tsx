import { useMemo } from 'react';
import { Highlight, type Token } from 'prism-react-renderer';
import type { FileRec } from '../lib/types';
import { buildRows, type Row } from '../lib/rows';
import { useStore } from '../lib/store';
import { Prism, VS_DARK, useCsharpReady } from '../lib/prismSetup';
import { Chevron } from './icons';

type GetTokenProps = (opts: { token: Token }) => { className?: string; style?: React.CSSProperties; children?: React.ReactNode };

function FoldBadge({ count, onClick }: { count: number; onClick: () => void }) {
  // { Nline } を四角で囲んだインラインバッジ(> の隣に出す)。
  return (
    <span className="foldbadge" onClick={(e) => { e.stopPropagation(); onClick(); }} title="展開">
      <span className="fb-b">{'{'}</span><span className="fb-n">{count} line</span><span className="fb-b">{'}'}</span>
    </span>
  );
}

function Src({ row, tokens, getTokenProps, onToggle }: { row: Row; tokens: Token[]; getTokenProps: GetTokenProps; onToggle: () => void }) {
  const strip = 'strip' in row && row.strip;
  const badge = 'badge' in row ? row.badge : undefined;
  return (
    <span className="src">
      {tokens.map((token, i) => {
        if (strip && token.content === '{') return null;             // 末尾の開き { は隠す
        const p = getTokenProps({ token });
        return <span key={i} className={p.className} style={p.style}>{token.content}</span>;
      })}
      {badge != null && <FoldBadge count={badge} onClick={onToggle} />}
    </span>
  );
}

export function CodeView({ file }: { file: FileRec }) {
  const { expanded, toggleFold } = useStore();
  const ready = useCsharpReady();
  const rows = useMemo(() => buildRows(file, expanded), [file, expanded]);

  // difit と同一: prism-react-renderer + csharp 文法 + vsDark テーマ。
  return (
    <Highlight prism={Prism} code={file.text} language={ready ? 'csharp' : 'text'} theme={VS_DARK}>
      {({ tokens, getTokenProps }) => (
        <div className="codeview">
          {rows.map((row, idx) => {
            const toggle = row.kind === 'sig' || row.kind === 'region';
            const foldKey = 'foldKey' in row ? row.foldKey : undefined;
            const onToggle = () => foldKey && toggleFold(foldKey);
            const collapsed = 'collapsed' in row ? row.collapsed : true;
            const lineTokens = tokens[row.n - 1] ?? [];
            return (
              <div
                key={idx}
                className={`cl${row.added ? ' added' : ''}${toggle ? ' foldable' : ''}${row.kind === 'region' ? ' regionline' : ''}`}
                onClick={toggle ? onToggle : undefined}
              >
                <span className="gutter">
                  {toggle ? <span className="chev"><Chevron open={!collapsed} /></span> : null}
                  {row.added ? <span className="plus">+</span> : null}
                </span>
                <span className="ln">{row.n}</span>
                <Src row={row} tokens={lineTokens} getTokenProps={getTokenProps} onToggle={onToggle} />
              </div>
            );
          })}
        </div>
      )}
    </Highlight>
  );
}
