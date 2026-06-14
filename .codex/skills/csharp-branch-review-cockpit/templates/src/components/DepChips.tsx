import type { FileRec } from '../lib/types';
import { useStore } from '../lib/store';
import { asmColor } from '../lib/asm';
import { ArrowDown, ArrowUp } from './icons';

function Chip({ path }: { path: string }) {
  const { byPath, jumpToFile } = useStore();
  const f = byPath.get(path);
  if (!f) return null;
  return (
    <button className="chip" onClick={() => jumpToFile(path)} title={path}>
      <span className="chip-dot" style={{ background: asmColor(f.asmdef) }} />
      {f.name.replace(/\.cs$/, '')}
    </button>
  );
}

// 依存(req2)。依存先↓ / 依存元↑ をチップで。クリックで該当ファイルへ。
export function DepChips({ file }: { file: FileRec }) {
  return (
    <div className="dep">
      <div className="dep-block">
        <div className="dep-h"><ArrowDown /> 依存先 <span className="cnt">{file.depsOut.length}</span></div>
        <div className="chips">
          {file.depsOut.length === 0 && <span className="muted">— なし（他の変更ファイルに依存しない）</span>}
          {file.depsOut.map((p) => <Chip key={p} path={p} />)}
        </div>
      </div>

      <div className="dep-block">
        <div className="dep-h"><ArrowUp /> 依存元 <span className="cnt">{file.depsIn.length}</span></div>
        <div className="chips">
          {file.depsIn.length === 0 && <span className="muted">— なし（この変更内で参照元なし）</span>}
          {file.depsIn.map((p) => <Chip key={p} path={p} />)}
        </div>
      </div>
    </div>
  );
}
