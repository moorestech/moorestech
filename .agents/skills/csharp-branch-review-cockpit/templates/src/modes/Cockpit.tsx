import { FileTree } from '../components/FileTree';
import { FileHeader } from '../components/FileHeader';
import { CodeView } from '../components/CodeView';
import { DepChips } from '../components/DepChips';
import { useStore } from '../lib/store';

export function Cockpit() {
  const { byPath, selected } = useStore();
  const file = byPath.get(selected);
  return (
    <div className="cockpit">
      <FileTree />
      <main className="center">
        {file ? (
          <>
            <FileHeader file={file} />
            <div className="code-scroll">
              <CodeView file={file} />
            </div>
          </>
        ) : (
          <div className="empty">ファイルを選択</div>
        )}
      </main>
      <aside className="inspector">
        <div className="insp-title">Dependencies</div>
        {file ? <DepChips file={file} /> : <div className="empty">—</div>}
      </aside>
    </div>
  );
}
