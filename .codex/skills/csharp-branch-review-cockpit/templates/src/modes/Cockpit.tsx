import { useStore } from '../lib/store';
import { FileTree } from '../components/FileTree';
import { FileHeader } from '../components/FileHeader';
import { CodeView } from '../components/CodeView';
import { DepChips } from '../components/DepChips';

// 3 ゾーン: 左=変更ファイル階層(req1)、中央=コード(req3 折りたたみ)、右=依存(req2)。
export function Cockpit() {
  const { byPath, selected } = useStore();
  const file = selected ? byPath.get(selected) : undefined;

  return (
    <div className="cockpit">
      <aside className="zone-tree"><FileTree /></aside>
      <main className="zone-center">
        {file ? (
          <>
            <FileHeader file={file} />
            <div className="zone-code"><CodeView file={file} /></div>
          </>
        ) : (
          <div className="empty">左のツリーからファイルを選択してください</div>
        )}
      </main>
      <aside className="zone-inspector">
        {file ? <DepChips file={file} /> : <div className="empty muted">—</div>}
      </aside>
    </div>
  );
}
