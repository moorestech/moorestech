#!/bin/bash
# uitk-event-language-gate.cs の運転役（呼び方は不変）。中身は系とシナリオを引数に取る exhibition-mode-run.sh へ委ねる
# Windows の GUI Editor で流すときは driver/winunity-scenario-run.sh <branch> GameUi exhibition <この .cs> <name> を使う
# Driver for uitk-event-language-gate.cs (same invocation); the work is delegated to exhibition-mode-run.sh, which takes the system and scenario
# On the Windows GUI Editor use driver/winunity-scenario-run.sh <branch> GameUi exhibition <this .cs> <name>
#
# usage: uitk-event-language-gate.sh <unity-project-path>
HERE="$(cd "$(dirname "$0")" && pwd)"
exec bash "$HERE/exhibition-mode-run.sh" "$1" GameUi "$HERE/uitk-event-language-gate.cs"
