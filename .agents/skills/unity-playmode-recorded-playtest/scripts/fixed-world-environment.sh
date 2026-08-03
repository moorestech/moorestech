#!/bin/bash

validate_fixed_world_environment() {
    local any_set=false
    local missing_variables=""

    # 3変数が全て未設定の場合だけ従来起動へ分類する
    # Classify as legacy boot only when all three variables are unset
    if [[ -n "${PLAYTEST_WORLD_DIRECTORY+x}" || -n "${PLAYTEST_MAP_MODE+x}" || -n "${PLAYTEST_SEED+x}" ]]; then
        any_set=true
    fi
    if [[ "$any_set" == false ]]; then
        return 1
    fi

    # 部分指定と空値を集約し、preflight前に不足名を明示する
    # Collect partial or empty values and name them before preflight
    [[ -n "${PLAYTEST_WORLD_DIRECTORY:-}" ]] || missing_variables="$missing_variables PLAYTEST_WORLD_DIRECTORY"
    [[ -n "${PLAYTEST_MAP_MODE:-}" ]] || missing_variables="$missing_variables PLAYTEST_MAP_MODE"
    [[ -n "${PLAYTEST_SEED:-}" ]] || missing_variables="$missing_variables PLAYTEST_SEED"
    if [[ -n "$missing_variables" ]]; then
        echo "NG: fixed-world variables must be all unset or all non-empty; missing/empty:$missing_variables" >&2
        return 2
    fi

    if [[ ! "$PLAYTEST_SEED" =~ ^-?[0-9]+$ ]]; then
        echo "NG: PLAYTEST_SEED must be an integer: $PLAYTEST_SEED" >&2
        return 2
    fi
    return 0
}
