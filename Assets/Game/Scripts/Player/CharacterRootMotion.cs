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

            // Clip Y motion is baked into the skeleton pose at import, preserving
            // the authored bounce. The motor applies planar travel with collisions.
            motor.QueueRootMotion(characterAnimator.deltaPosition);
        }
    }
}
