# uloop v3移行はOpenUPMピン＋owned-wrapper温存で行う

2026-09-06裁定。

## 決定
- CLIとUnity packageを同時にv3.3.0へ上げる（CLIだけ先行の段階移行は採らない）
- Unity packageの参照はOpenUPM scoped registry + `"io.github.hatayama.uloopmcp": "3.3.0"` のバージョンピンにする
- v3 CLI本体は `~/.local/bin` ではなく別ディレクトリへ入れ、`~/.local/bin/uloop` は `uloop-owned-wrapper.py` への symlink のまま維持する

## 棄却した案
- **CLIだけ先に入れる**: v3 dispatcherは旧package版へ自動委譲するため既存17 worktreeは動く見込みだったが、委譲経路はnpm頼りで検証コストが二重になる。一気に上げる方が状態が一本化される
- **git URLを `#v3.3.0` タグ固定にする**: 変更は最小だが、公式の `uloop package install` 経路（OpenUPM前提）から外れ続ける。lock hash頼みのノーピン状態を解消する好機なのでOpenUPMへ寄せる
- **インストーラを既定の `$HOME/.local/bin` へ流す**: wrapper symlinkを上書きし、reaperのUnity所有権receiptが記録されなくなる（Editorが無言で刈られる事故の再発）ため不可

## 理由
v3はnpm配布を廃止しネイティブバイナリ＋プロジェクト毎runnerの2層構成になった。runnerはpackage版に自動追従するため、packageのピンがCLI版の実質的な正本になる。ピンが無いとlock削除のたびに別版へ飛ぶ。

## リンク
- https://github.com/hatayama/unity-cli-loop/releases/tag/v3.3.0
- Packages/src/Documentation~/whats-new-v3.md, migration-v2-to-v3.md
