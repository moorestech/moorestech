# Task 12 report (DC-30 A)
- 実装: writeOrCheck.mjs 新規、両変換器を writeOrCheck 経由へ（--check 対応。200 行検査は check でも走る）。
- CI: uitk-web-follow-gate-selftest に node --test の step、run_test.yml webui-test に feature/uitk 条件付きの --check step（D-23）。
- テスト: node --test 2/2 pass。convert-web-tokens --check exit 0、pnpm install 後 convert-mantine-tokens --check exit 0（差なし＝変換し忘れ・手変更なし）。yml は ruby で構文確認。
- .cs 変更なし。懸念なし。
