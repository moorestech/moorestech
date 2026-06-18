import type { FileRec } from '../lib/types';
import { useStore } from '../lib/store';
import { asmColor } from '../lib/asm';
import { Collapse, Check } from './icons';

export function FileHeader({ file, showFolds = true }: { file: FileRec; showFolds?: boolean }) {
  const { setFileFolds, reviewed, toggleReviewed } = useStore();
  const isReviewed = reviewed.has(file.path);
  // 既知の repo 接頭辞(server/client/web)を畳んでディレクトリだけ表示。
  const dir = file.path
    .replace(/^moorestech_(server|client)\/Assets\/Scripts\//, '')
    .replace(/^moorestech_web\//, '')
    .replace(/\/[^/]+$/, '');
  return (
    <div className="fhead">
      <div className="fhead-main">
        <span className="status" data-s={file.status}>{file.status}</span>
        <span className="fhead-name">{file.name}</span>
        <span className="asm" style={{ color: asmColor(file.asmdef), borderColor: asmColor(file.asmdef) }}>{file.asmdef}</span>
        <span className="numstat">
          <span className="add">+{file.add}</span>
          {file.del > 0 && <span className="del">−{file.del}</span>}
        </span>
      </div>
      <div className="fhead-sub">
        <span className="fhead-dir">{dir}/</span>
        <span className="spacer" />
        {showFolds && (
          <>
            <button className="hbtn" onClick={() => setFileFolds(file.path, false)} title="全て折りたたむ"><Collapse /> fold</button>
            <button className="hbtn" onClick={() => setFileFolds(file.path, true)} title="全て展開">unfold</button>
          </>
        )}
        <button className={`hbtn reviewed${isReviewed ? ' on' : ''}`} onClick={() => toggleReviewed(file.path)}>
          <Check /> {isReviewed ? 'reviewed' : 'mark reviewed'}
        </button>
      </div>
    </div>
  );
}
