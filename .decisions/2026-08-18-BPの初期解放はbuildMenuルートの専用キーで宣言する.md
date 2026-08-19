# BPの初期解放はbuildMenuルートの専用キーで宣言する

決定: `buildMenu.yml`のルートへ`blueprintInitialUnlocked`(boolean, default false)を追加し、Holderは1値を読むだけにする。buildTools各要素の`initialUnlocked`からN件をOR集約する形はやめる。

棄却案: 現状維持（toolType==blueprintCopyで絞ってOR集約）— 実害は消えているが、どの1件が機能フラグなのかが構造から読めず複数件が矛盾する値を持てる畳み込み構造が残るため

理由: 表現不能な状態を構造的に排除する。実マスタ未更新の今が最も安い（スキーマ2箇所＋Holder1行＋テストマスタ1行）。
リンク: [[2026-08-18-ブループリントは機能全体を1フラグでロックする]] / moores-code-review run 2026-08-18-1425 C5b
