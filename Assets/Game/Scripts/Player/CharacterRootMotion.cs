using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterRootMotion : MonoBehaviour
    {
        private Animator characterAnimator;
        private TopDownPlayerMotor motor;

        private void Awake()
        {
            characterAnimator = GetComponent<Animator>();
            motor = GetComponentInParent<TopDownPlayerMotor>();
        }

        private void OnAnimatorMove()
        {
            if (motor == null)
            {
                characterAnimator.ApplyBuiltinRootMotion();
                return;
            }

            // Nonzero motor speeds and dash already move the parent gameplay body.
            if (!motor.UsesDirectRootMotion || motor.IsDashing)
                return;

            var direction = motor.RequestedWorldMoveDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return;
            direction.Normalize();

            // The clip supplies distance, WASD supplies direction, and the facing
            // controller owns cursor aim. Reverse playback must not invert input.
            var delta = characterAnimator.deltaPosition;
            var planarDistance = new Vector2(delta.x, delta.z).magnitude;
            transform.position += direction * planarDistance + Vector3.up * delta.y;
        }
    }
}
