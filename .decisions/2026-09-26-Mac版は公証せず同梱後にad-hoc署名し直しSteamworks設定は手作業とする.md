決定: Mac 版は公証せず、同梱後に .app 全体を ad-hoc 署名し直してから上げる。Steamworks 側（macOS depot・起動オプション・パッケージへの depot 追加）は README の手作業手順で人が行い、env は MOORESTECH_STEAM_DEPOT_ID_WINDOWS / MOORESTECH_STEAM_DEPOT_ID_MAC に改名・新設し未設定ならビルド前に止まる。
棄却案: Developer ID 署名＋公証／署名し直さない／旧変数名を残し _MAC だけ足す
理由: Steam 経由の配布では quarantine が付かず公証は不要。ビルド後に helper と ffmpeg を入れるため署名を揃え直す。
リンク: docs/adr/0071-playtest-distribution-adds-apple-silicon-mac-depot.md
出所: ユーザー裁定 2026-09-26 質問「Mac版のコード署名と公証をどうしますか？」→ 選択「A」／質問「Steamworks側で必要な設定はどう進めますか？」→ 選択「A」
追記（2026-09-26）: 手元 Mac の確認で起動に回避操作（セキュリティ設定変更・コマンド実行）が要っても配布は止めず、回避手順をキーと一緒に案内する。棄却案: 回避操作なしで起動できるまで反映を止める。出所: ユーザー裁定 2026-09-26 質問「…playtest反映を止めますか？」→ 選択「B：初回は回避手順を案内して配布してよい」
