using UnityEngine;

namespace RorType.Gameplay.Player
{
    public sealed class TopDownInputAdapter : MonoBehaviour
    {
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode respawnKey = KeyCode.R;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode dashKey = KeyCode.LeftControl;
        [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
        [SerializeField] private KeyCode moveRightKey = KeyCode.D;
        [SerializeField] private KeyCode moveForwardKey = KeyCode.W;
        [SerializeField] private KeyCode moveBackwardKey = KeyCode.S;
        [SerializeField] private int fireMouseButton = 0;
        [SerializeField] private int meleeMouseButton = 1;

        public Vector2 MoveInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool RespawnPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool DashPressed { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool MeleeHeld { get; private set; }
        public bool MeleePressed { get; private set; }
        public Vector3 MouseScreenPosition { get; private set; }
        public bool HasMovementInput => MoveInput.sqrMagnitude > 0.0001f;

        private void Update()
        {
            var rawInput = new Vector2(
                Input.GetAxisRaw(horizontalAxis),
                Input.GetAxisRaw(verticalAxis));

            if (rawInput.sqrMagnitude <= 0.0001f)
            {
                rawInput = ReadKeyboardMovement();
            }

            MoveInput = Vector2.ClampMagnitude(rawInput, 1f);
            SprintHeld = Input.GetKey(sprintKey);
            RespawnPressed = Input.GetKeyDown(respawnKey);
            InteractPressed = Input.GetKeyDown(ResolveInteractKey());
            JumpPressed = Input.GetKeyDown(jumpKey);
            DashPressed = Input.GetKeyDown(dashKey);
            FireHeld = Input.GetMouseButton(fireMouseButton);
            if (Input.GetMouseButtonDown(fireMouseButton))
            {
                FirePressed = true;
            }

            MeleeHeld = Input.GetMouseButton(meleeMouseButton);
            if (Input.GetMouseButtonDown(meleeMouseButton))
            {
                MeleePressed = true;
            }

            MouseScreenPosition = Input.mousePosition;
        }

        public void ConsumeFrameActions()
        {
            RespawnPressed = false;
            InteractPressed = false;
        }

        public void ConsumeInteractPressed()
        {
            InteractPressed = false;
        }

        public void ConsumeJumpPressed()
        {
            JumpPressed = false;
        }

        public void ConsumeDashPressed()
        {
            DashPressed = false;
        }

        public void ConsumeFirePressed()
        {
            FirePressed = false;
        }

        public void ConsumeMeleePressed()
        {
            MeleePressed = false;
        }

        private Vector2 ReadKeyboardMovement()
        {
            var horizontal = 0f;
            if (Input.GetKey(moveLeftKey) || Input.GetKey(KeyCode.LeftArrow))
            {
                horizontal -= 1f;
            }

            if (Input.GetKey(moveRightKey) || Input.GetKey(KeyCode.RightArrow))
            {
                horizontal += 1f;
            }

            var vertical = 0f;
            if (Input.GetKey(moveBackwardKey) || Input.GetKey(KeyCode.DownArrow))
            {
                vertical -= 1f;
            }

            if (Input.GetKey(moveForwardKey) || Input.GetKey(KeyCode.UpArrow))
            {
                vertical += 1f;
            }

            return new Vector2(horizontal, vertical);
        }

        private KeyCode ResolveInteractKey()
        {
            return interactKey == KeyCode.None ? KeyCode.E : interactKey;
        }
    }
}
