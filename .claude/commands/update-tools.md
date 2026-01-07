---
name: update-tools
---

toolsディレクトリのエディタツール（CommandForgeEditor, mooreseditor）を最新版に一括更新してください。

## 対象
- **リポジトリ**: moorestech/CommandForgeEditor, moorestech/mooreseditor
- **更新先**: `tools/mac/` と `tools/windows/`

## ワークフロー

### 1. 最新リリース確認
並列で実行:
```bash
gh release view --repo moorestech/CommandForgeEditor
gh release view --repo moorestech/mooreseditor
```

### 2. 一括ダウンロード（並列実行）
すべてのアセットを `/tmp/tools_update/` に並列ダウンロード:
```bash
mkdir -p /tmp/tools_update
gh release download --repo moorestech/CommandForgeEditor --pattern "*macos*.dmg" --pattern "*windows*.exe" --dir /tmp/tools_update/ --clobber
gh release download --repo moorestech/mooreseditor --pattern "*macos*.dmg" --pattern "*windows*.exe" --dir /tmp/tools_update/ --clobber
```

### 3. 既存ファイル削除
```bash
rm -rf tools/mac/CommandForgeEditor.app tools/mac/mooreseditor.app
rm -f tools/windows/CommandForgeEditor.exe tools/windows/mooreseditor.exe
```

### 4. macOS用アプリ展開
DMGをマウントしてアプリをコピー（順番に実行）:
```bash
# CommandForgeEditor
hdiutil attach /tmp/tools_update/CommandForgeEditor-*-macos*.dmg -mountpoint /tmp/cfe_mount -nobrowse
cp -R "/tmp/cfe_mount/CommandForgeEditor.app" tools/mac/
hdiutil detach /tmp/cfe_mount

# mooreseditor
hdiutil attach /tmp/tools_update/mooreseditor-*-macos*.dmg -mountpoint /tmp/me_mount -nobrowse
cp -R "/tmp/me_mount/mooreseditor.app" tools/mac/
hdiutil detach /tmp/me_mount
```

### 5. Windows用exeリネーム
```bash
mv /tmp/tools_update/CommandForgeEditor-*-windows*.exe tools/windows/CommandForgeEditor.exe
mv /tmp/tools_update/mooreseditor-*-windows*.exe tools/windows/mooreseditor.exe
```

### 6. クリーンアップと検証
```bash
rm -rf /tmp/tools_update
ls -la tools/mac/ tools/windows/
```

更新完了後、バージョンと更新結果のサマリーを表示してください。
