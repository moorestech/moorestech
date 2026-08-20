# 研究の未対応GameActionTypeは例外で落とす

決定: `ResearchNodeDtoFactory` の解放種別分岐を `switch` へ変え、表示する6種と「解放されるが研究画面には出さない」6種を全て明示caseで列挙し、それ以外は `InvalidOperationException` を投げる。`ToStateString` の暗黙 `_` も同様に潰す

棄却案: `default` をログ出力だけに留める（研究画面が落ちない代わりに、ログを見ていないと解放物の無言欠落に気づけない）／`unlockCraftRecipe` 等を第7の表示種別としてDTO・zod・フィクスチャ・判別union・セクション表まで通す（表示集合6種を定めた [[2026-08-19-解放物の種別はWeb側の判別unionで型に出す]] の範囲を広げるため、別タスクとして積む）

理由: 暗黙elseだと `research.json` に未対応種別を1件足すだけで、サーバーは解放を実行するのにDTOに何も乗らず、C#契約テストもzodもTSの型検査も全部緑のまま詳細ペインからセクションが消える。種別が増えた日に即座に気づける形を優先する

リンク: moores-code-review run 2026-08-19-1640 の C2 / 設計判断3、[[2026-08-19-解放物の種別はWeb側の判別unionで型に出す]]
