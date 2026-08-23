# KeyControlDescriptionを完全削除する

## 決定
uGUI時代の操作ヒュー機構を完全に撤去する。C#クラス `KeyControlDescription.cs`、各UIStateに残る `KeyControlDescription.Instance.SetText(...)` 全10箇所、`MainGameUI.prefab` 上の `KeyControlDescription` オブジェクト（Unity Editor経由で削除）、`Client.Tests` 側の生成・WebUiゲート分類ルールまで消す。

出所: ユーザー裁定 2026-08-23 「完全削除する（推奨）」

## 棄却案
- **C#呼び出しだけ消してprefabは残す**: Unity作業は不要になるが、Missing ScriptのGameObjectがprefabに残る。
- **今回は触らず放置**: 差分は小さいが、腐った文字列（`Tab/ECS`誤記・Q/E逆）がリポジトリに残り次に読む人が参照してしまう。

## リンク
- [[2026-08-23-操作ヒントの正はC#のUIStateに置きtopicで配る]]
