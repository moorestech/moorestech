# 背景スキットの uGUI プレースホルダ文字は prefab ごと除去する

- **日付**: 2026-09-06
- **文脈**: PR #1325（uGUI退役 PR1・論理モデル抽出）のレビューで、`BackgroundSkitManager` から `backgroundSkitUI.SetTextVisible(!WebUiScreenGate.IsWebUiMode)` の呼び出しが消え、`MainGameUI.prefab` の既定 active な `BackgroundText` を落とす経路が無くなった。結果、背景スキット中に旧仕様のプレースホルダ文字「エレノ：こんにちは」が Web UI の上に描画され、全幅の当たり判定でクリックも吸われる（R7 挙動不変・R3 に対する退行）。

## 決定

`uloop execute-dynamic-code` で `MainGameUI.prefab` の `BackgroundText`（fileID `4363393695022849050`）を除去する。`BackgroundSkitVoicePlayer` は音声専用のまま保つ。同じ手で `ChallengeHudView` の常時活性（W22）も畳み、死値 `skitText` 参照も落とす。

## 棄却した案

- **コード側で常に非表示にする**: `BackgroundSkitVoicePlayer` に `legacyTextRoot` の `[SerializeField]` を足し `SetActive` 内で常に false にする。PR1 の非目標「prefab オブジェクト削除は PR2」を厳密に守れるが、PR2 で必ず消す前提の uGUI 抑止コードを1本増やすことになる。
- **今回は塞がない**: PR2 の prefab 削除まで退行を残す。マージから PR2 までの間、実プレイで文字とクリック吸いが起きるため却下。

## 理由

「PR1 は prefab を触らない」は作業分割の便宜であって守るべき不変条件ではない。退行を残す・使い捨ての抑止コードを増やす、のどちらよりも、原因そのものを断つほうが安い。

## リンク

- [[2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける]]
- `docs/adr/0052-ugui-removal-scope-and-exceptions.md`
