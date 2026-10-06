using RorType.Gameplay.UI;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RorType.Gameplay.Player
{
    [DefaultExecutionOrder(-100)]
    public sealed class TopDownInputAdapter : MonoBehaviour
    {
        [Header("Local input devices")]
        [SerializeField] private bool enableKeyboardAndMouse = true;
        [SerializeField] private bool enableGamepad = true;
        [SerializeField, Min(0)] private int gamepadIndex;
        [SerializeField, Range(0f, 0.9f)] private float stickDeadzone = 0.15f;
        [SerializeField, Range(0.01f, 1f)] private float triggerThreshold = 0.25f;
        [Header("Keyboard and mouse")]
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode respawnKey = KeyCode.Home;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private KeyCode reloadKey = KeyCode.R;
        [SerializeField] private KeyCode dashKey = KeyCode.Space;
        [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
        [SerializeField] private KeyCode moveRightKey = KeyCode.D;
        [SerializeField] private KeyCode moveForwardKey = KeyCode.W;
        [SerializeField] private KeyCode moveBackwardKey = KeyCode.S;
        [SerializeField] private int fireMouseButton;
        [SerializeField] private int aimMouseButton = 1;
        [SerializeField] private int cameraRotateMouseButton = 2;

        public Vector2 MoveInput { get; private set; }
        public Vector2 AimInput { get; private set; }
        public bool UsesGamepad { get; private set; }
        public bool KeyboardAndMouseEnabled => enableKeyboardAndMouse;
        public bool AimHeld { get; private set; }
        public bool CameraRotateHeld { get; private set; }
        public float CameraRotationInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool RespawnPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool JumpPressed => false;
        public bool DashPressed { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool RadialSkillPressed { get; private set; }
        public bool PointSkillPressed { get; private set; }
        public bool CancelPressed { get; private set; }
        public bool CombatInputBlocked { get; private set; }
        public Vector3 MouseScreenPosition { get; private set; }
        public bool HasMovementInput => MoveInput.sqrMagnitude > 0.0001f;
        private bool padSprint;
        private bool previousPadFire;
        private bool suppressFireUntilRelease;
        private TopDownFacingController weapon;
#if ENABLE_INPUT_SYSTEM
        private Gamepad boundGamepad;
#endif

        private void Awake() => weapon = GetComponent<TopDownFacingController>();

        private void Update()
        {
            var keyboardMove = enableKeyboardAndMouse ? ReadKeyboardMovement() : Vector2.zero;
            var mouse = Input.mousePosition;
            var keyboardActive = enableKeyboardAndMouse && (keyboardMove.sqrMagnitude > 0.001f
                || (mouse - MouseScreenPosition).sqrMagnitude > 4f
                || Input.GetMouseButtonDown(fireMouseButton) || Input.GetMouseButtonDown(aimMouseButton)
                || Input.GetMouseButtonDown(cameraRotateMouseButton)
                || Input.GetKeyDown(reloadKey) || Input.GetKeyDown(interactKey)
                || Input.GetKeyDown(dashKey) || Input.GetKeyDown(sprintKey)
                || Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2)
                || Input.GetKeyDown(KeyCode.Escape));
            var previousUsesGamepad = UsesGamepad;
            MouseScreenPosition = mouse;
            var padMove = Vector2.zero;
            var padAim = Vector2.zero;
            var padAimHeld = false;
            var padFire = false;
            var padReload = false;
            var padInteract = false;
            var padDash = false;
            RadialSkillPressed = PointSkillPressed = false;
            CancelPressed = enableKeyboardAndMouse && Input.GetKeyDown(KeyCode.Escape);
#if ENABLE_INPUT_SYSTEM
            if (boundGamepad != null && (!boundGamepad.added || !enableGamepad)) boundGamepad = null;
            if (boundGamepad == null && enableGamepad && gamepadIndex < Gamepad.all.Count)
                boundGamepad = Gamepad.all[gamepadIndex];
            if (boundGamepad != null)
            {
                var pad = boundGamepad;
                padMove = ApplyDeadzone(pad.leftStick.ReadValue());
                padAim = ApplyDeadzone(pad.rightStick.ReadValue());
                padAimHeld = pad.leftTrigger.ReadValue() >= triggerThreshold;
                padFire = pad.rightTrigger.ReadValue() >= triggerThreshold;
                padReload = pad.buttonWest.wasPressedThisFrame;
                padInteract = pad.buttonSouth.wasPressedThisFrame;
                padDash = pad.leftShoulder.wasPressedThisFrame;
                var padActive = padMove.sqrMagnitude > 0f || padAim.sqrMagnitude > 0f
                    || padAimHeld || padFire || padReload || padInteract || padDash
                    || pad.leftStickButton.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame
                    || pad.dpad.up.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame;
                if (padActive) UsesGamepad = true;
                if (pad.leftStickButton.wasPressedThisFrame) padSprint = !padSprint;
                if (UsesGamepad)
                {
                    RadialSkillPressed = pad.dpad.left.wasPressedThisFrame;
                    PointSkillPressed = pad.dpad.up.wasPressedThisFrame;
                    CancelPressed |= pad.buttonEast.wasPressedThisFrame;
                }
            }
            else UsesGamepad = false;
#endif
            if (keyboardActive) UsesGamepad = false;
            if (previousUsesGamepad != UsesGamepad) FirePressed = false;
            if (!UsesGamepad)
            {
                RadialSkillPressed = PointSkillPressed = false;
                CancelPressed = enableKeyboardAndMouse && Input.GetKeyDown(KeyCode.Escape);
            }
            var controlsBlocked = !Application.isFocused || Time.timeScale <= 0f
                || PortalUiRuntime.IsChoiceOpen || ShopUiPanel.IsAnyOpen;
            CombatInputBlocked = controlsBlocked
                || (!UsesGamepad && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            MoveInput = Vector2.ClampMagnitude(UsesGamepad ? padMove : keyboardMove, 1f);
            AimInput = UsesGamepad ? padAim : Vector2.zero;
            AimHeld = !CombatInputBlocked && (UsesGamepad ? padAimHeld
                : enableKeyboardAndMouse && Input.GetMouseButton(aimMouseButton));
            CameraRotateHeld = !CombatInputBlocked && !UsesGamepad && enableKeyboardAndMouse
                && Input.GetMouseButton(cameraRotateMouseButton);
            CameraRotationInput = CameraRotateHeld ? Input.GetAxisRaw("Mouse X") : 0f;
            RespawnPressed = enableKeyboardAndMouse && Input.GetKeyDown(respawnKey);
            InteractPressed = !CombatInputBlocked && (UsesGamepad ? padInteract
                : enableKeyboardAndMouse && Input.GetKeyDown(interactKey));
            ReloadPressed = !CombatInputBlocked && (UsesGamepad ? padReload
                : enableKeyboardAndMouse && Input.GetKeyDown(reloadKey));
            DashPressed = !CombatInputBlocked && (UsesGamepad ? padDash
                : enableKeyboardAndMouse && Input.GetKeyDown(dashKey));
            var rawFire = UsesGamepad ? padFire : enableKeyboardAndMouse && Input.GetMouseButton(fireMouseButton);
            var fireDown = UsesGamepad ? padFire && !previousPadFire
                : enableKeyboardAndMouse && Input.GetMouseButtonDown(fireMouseButton);
            previousPadFire = padFire;
            if (!rawFire) suppressFireUntilRelease = false;
            FireHeld = !CombatInputBlocked && !suppressFireUntilRelease && rawFire;
            if (CombatInputBlocked || suppressFireUntilRelease) FirePressed = false;
            else if (fireDown) FirePressed = true;
            // Keep sprint off through a queued click and the whole shot animation.
            // A held Shift cannot restart running during weapon preparation.
            var firing = (AimHeld && (FireHeld || FirePressed)) || (weapon != null && weapon.IsFiring);
            if (!HasMovementInput || AimHeld || firing || controlsBlocked) padSprint = false;
            SprintHeld = !controlsBlocked && !AimHeld && !firing && HasMovementInput
                && (UsesGamepad ? padSprint : enableKeyboardAndMouse && Input.GetKey(sprintKey));
            if (controlsBlocked)
            {
                MoveInput = AimInput = Vector2.zero;
            }
            if (CombatInputBlocked)
            {
                RadialSkillPressed = PointSkillPressed = false;
            }
        }

        public void ConsumeFrameActions() { RespawnPressed = InteractPressed = false; }
        public void ConsumeInteractPressed() => InteractPressed = false;
        public void ConsumeJumpPressed() { }
        public void ConsumeDashPressed() => DashPressed = false;
        public void ConsumeFirePressed() => FirePressed = false;
        public void ConsumeReloadPressed() => ReloadPressed = false;
        public void SuppressFireUntilRelease()
        {
            suppressFireUntilRelease = true;
            FireHeld = FirePressed = false;
        }

        private Vector2 ApplyDeadzone(Vector2 value)
        {
            var magnitude = value.magnitude;
            return magnitude <= stickDeadzone ? Vector2.zero
                : value.normalized * Mathf.Clamp01((magnitude - stickDeadzone) / (1f - stickDeadzone));
        }

        private Vector2 ReadKeyboardMovement()
        {
            var x = (Input.GetKey(moveRightKey) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(moveLeftKey) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            var y = (Input.GetKey(moveForwardKey) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(moveBackwardKey) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            return new Vector2(x, y);
        }

        private void OnDisable()
        {
            MoveInput = AimInput = Vector2.zero;
            CameraRotateHeld = false;
            CameraRotationInput = 0f;
            AimHeld = SprintHeld = FireHeld = FirePressed = padSprint = false;
            SuppressFireUntilRelease();
        }

        private void OnValidate()
        {
            gamepadIndex = Mathf.Max(0, gamepadIndex);
            stickDeadzone = Mathf.Clamp(stickDeadzone, 0f, 0.9f);
            triggerThreshold = Mathf.Clamp(triggerThreshold, 0.01f, 1f);
            fireMouseButton = Mathf.Clamp(fireMouseButton, 0, 6);
            aimMouseButton = Mathf.Clamp(aimMouseButton, 0, 6);
            cameraRotateMouseButton = Mathf.Clamp(cameraRotateMouseButton, 0, 6);
        }
    }
}
