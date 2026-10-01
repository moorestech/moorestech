using UnityEngine;

namespace StarterAssets
{
    // 飛行中の速度規則と上下入力を持つ。いつ飛ぶかは外から決める
    // Owns flight speed rules and vertical input; when to fly is decided outside
    public class PlayerFlightMotion
    {
        // 走り(SprintSpeed)に掛ける倍率。ADR 0075
        // Multipliers applied to SprintSpeed (ADR 0075)
        private const float FlightSpeedMultiplier = 2f;
        private const float FlightSprintSpeedMultiplier = 6f;

        public bool IsFlying { get; private set; }
        private float _verticalInput;

        public void SetFlying(bool isFlying)
        {
            // 入り直しで前回の上下入力を持ち越さない
            // Never carry the previous vertical input across re-entries
            IsFlying = isFlying;
            _verticalInput = 0f;
        }

        public void SetVerticalInput(float verticalInput)
        {
            _verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
        }

        public float ResolveSpeed(bool sprint, float sprintSpeed)
        {
            return sprintSpeed * (sprint ? FlightSprintSpeedMultiplier : FlightSpeedMultiplier);
        }

        public float ResolveVerticalVelocity(float gravityVerticalVelocity, float flightSpeed, bool movementLocked)
        {
            // 非飛行時は重力で積算した値をそのまま使う
            // Outside flight, keep the gravity-integrated value as is
            if (!IsFlying) return gravityVerticalVelocity;

            // 移動ロック中は水平と同じく上下も止める
            // Halt vertical motion while locked, same as horizontal
            return movementLocked ? 0f : _verticalInput * flightSpeed;
        }
    }
}
