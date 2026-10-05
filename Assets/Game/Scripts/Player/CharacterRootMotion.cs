using RorType.Gameplay.Rts;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterRootMotion : MonoBehaviour
    {
        private Animator characterAnimator;
        private TopDownPlayerMotor motor;
        private RtsUnit rtsUnit;

        private void Awake()
        {
            characterAnimator = GetComponent<Animator>();
            motor = GetComponentInParent<TopDownPlayerMotor>();
            rtsUnit = GetComponentInParent<RtsUnit>();
        }

        private void OnAnimatorMove()
        {
            if (rtsUnit != null)
            {
                rtsUnit.ApplyInfantryRootMotion(characterAnimator.deltaPosition);
                return;
            }

            if (motor == null)
            {
                characterAnimator.ApplyBuiltinRootMotion();
                return;
            }

            // The motor applies animation travel to the physics body in FixedUpdate.
            // Moving the visual here bypasses wall casts and accumulates clip Y drift.
            motor.QueueRootMotion(characterAnimator.deltaPosition);
        }
    }
}
