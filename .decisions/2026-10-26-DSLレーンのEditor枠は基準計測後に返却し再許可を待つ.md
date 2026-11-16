決定: DSLレーンはTask1基準計測完了後に自Editorをkillし、`.superpowers/sdd/editor-released`へ時刻と停止PIDを記録してレーンBへ枠を返す。Editor不要タスクを先行し、次回起動は`.superpowers/sdd/editor-slot-granted`が置かれるまで前景で待つ。基準計測途中の再起動は従来どおりとする。
棄却案: レーン完了までEditorを保持する。
理由: 指揮によるEditor枠の割り当て。
リンク: ユーザーの指揮moorestech-f2からの指示、bd `moorestech-albof.6`。
