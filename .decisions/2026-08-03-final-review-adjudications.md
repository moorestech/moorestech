# 最終ブランチレビューの設計判断4件

決定: 辞書取得のHTTP 409は現状維持（watchdogを入れない）。revision pushで必ず回収される作りであることをserver-state-syncレンズが実コードで確認済みで、時限式の防御機構はAGENTS.md「防御的残置はしない」に当たる（AskUserQuestion 2026-08-03）
棄却案: 409時に有限時間のwatchdogを開始し、次pushが届かなければ`error`へ遷移させてリロード手段を出す案（Codex Medium指摘）
理由: 実害が未確認であり、409を`staleRevision`として扱うのはD2実装時の意図的な分類（classifier＋コメント＋テストの3点で明示済み）。その判断を防御機構で上書きしない
リンク: docs/superpowers/plans/2026-08-02-localization-review-remediation.md Task 19

決定: `deleteMode.unavailableReason` はDTOごと削除する（AskUserQuestion 2026-08-03・シミュレーター予測→ユーザー承認）
棄却案: delete側だけキー化して`reasonKey`+`params`にする案／現状維持で別PRへ送る案
理由: `modeHudDesign.test.ts:26` がWeb側の`Topics.deleteMode`消費をテストで禁止しており「今は消費者が無い」ではなく「消費してはいけない」フィールドである。拒否理由は同じメソッド内のTooltipで既にキー経路で届いており二重経路。読むことを禁じられたフィールドをキー化して残すのは防御的残置に当たる（[[2026-08-02-source-locale-wire-and-skit-language-contract]]と同じくデッド要素は消す方針）
リンク: docs/superpowers/plans/2026-08-02-localization-review-remediation.md Task 19

決定: チュートリアル文言（`challengeTutorial.text`）もGuid送出へ移行し、Web側で`t(challengeTutorialTextKey(guid))`で解決する（シミュレーター予測・確信高→前提宣言として提示・ユーザーの拒否権行使なし 2026-08-03）
棄却案: 言語購読が欠落している2 manager（UIHighlightTutorialManager / ItemViewHighLightTutorialManager）に購読を足すだけでホスト解決を維持する案
理由: ADR 0006「マスタ由来表示名はホスト側で文字列解決しない」の直接適用であり、裁定D1（connectTool）・D4（Tooltip）と同型。案Bだと複製が5箇所へ増え、生成済みで未使用の`challengeTutorialTextKey`と予約済みで死んでいる`MessageKey`が残り続ける
リンク: docs/adr/0006-mod-localization-guid-derived-keys-web-side-resolution.md

決定（実装時の範囲確定・裁定の再確認ではなく前提の訂正）: チュートリアル文言のうちWebへ届いているのはワールドピンだけであり、そこはGuid送出へ移行する。ハイライト文言（`TutorialHighlightData.Message` / 予約`MessageKey` / zodの`message`・`messageKey`）はGuid化せず削除する
棄却案: ハイライト文言もGuid化して`callout`描画を残す案
理由: 質問時の前提「チュートリアル文言がホスト翻訳でWebへ送られ表示されている」がハイライトについては不成立。文言を描画するのは`kind==="callout"`のみで、C#の生産者は`AddOutlineHighlight`（`Kind="outline"`固定）だけ。ストアのコメントも「廃止済みkindの再流入を防ぐ」と明記しており、calloutは既に廃止済みkindの残骸。一度も表示されないフィールドをGuid化して残すのは[[2026-08-03-final-review-adjudications]]のdeleteMode裁定（読み手のいないフィールドは消す）と同じ理由で不可
リンク: docs/adr/0006-mod-localization-guid-derived-keys-web-side-resolution.md 決定5追記

決定: trainCar表示名もマスタの`name`から辞書経路へ載せ、Guid導出キー14種→15種で閉じる（シミュレーター予測・確信高→前提宣言として提示・ユーザーの拒否権行使なし 2026-08-03）
棄却案: ADR 0006決定5の「正準source未定のtrainCarは暫定Label維持」をそのまま追認する案
理由: 免除理由が事実として不成立。`VanillaSchema/train.yml`に`trainCars[].name`が必須フィールドとして既に存在しv8実データ3件も記入済みで、正準sourceは実在する。現状のaddressablePath末尾表示ではビルドメニューに"Locomotive"が2件並ぶ実バグがある
リンク: docs/adr/0006-mod-localization-guid-derived-keys-web-side-resolution.md
