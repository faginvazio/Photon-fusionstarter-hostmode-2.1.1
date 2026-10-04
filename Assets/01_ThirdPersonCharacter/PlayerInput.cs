using UnityEngine;
using Fusion;

namespace Starter.ThirdPersonCharacter
{
    public enum EInputButton
    {
        Jump,
        Sprint,
    }

    /// <summary>
    /// Input structure sent over network to the server.
    /// </summary>
    public struct GameplayInput : INetworkInput
    {
        public Vector2 LookRotation;
        public Vector2 MoveDirection;
        public NetworkButtons Buttons;
    }

    /// <summary>
    /// PlayerInput handles accumulating player input from Unity and passes the accumulated input to Fusion.
    /// </summary>
    public sealed class PlayerInput : NetworkBehaviour
    {
        public Vector2 LookRotation => _input.LookRotation;

        private GameplayInput _input;
        private InputActions _inputActions;

        public override void Spawned()
        {
            if (HasInputAuthority == false)
                return;

            // Register to Fusion input poll callback
            var networkEvents = Runner.GetComponent<NetworkEvents>();
            networkEvents.OnInput.AddListener(OnInput);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (runner == null)
                return;

            var networkEvents = runner.GetComponent<NetworkEvents>();
            if (networkEvents != null)
            {
                networkEvents.OnInput.RemoveListener(OnInput);
            }
        }

        private void OnEnable()
        {
            // InputActions class is auto-generated from the InputSystem_Actions asset.
            _inputActions ??= new InputActions();
            _inputActions.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Disable();
        }

        private void Update()
        {
            // Accumulate input from Keyboard/Mouse. Input accumulation is mandatory (at least for look rotation) as Update can be
            // called multiple times before next OnInput is called - common if rendering speed is faster than Fusion simulation.

            if (HasInputAuthority == false)
                return;

            // Accumulate input only if the cursor is locked.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                _input.MoveDirection = default;
                return;
            }

            var lookValue = _inputActions.Player.Look.ReadValue<Vector2>();
            var lookRotationDelta = new Vector2(-lookValue.y, lookValue.x);
            _input.LookRotation = ClampLookRotation(_input.LookRotation + lookRotationDelta);

            _input.MoveDirection = _inputActions.Player.Move.ReadValue<Vector2>();

            _input.Buttons.Set(EInputButton.Jump, _inputActions.Player.Jump.IsPressed());
            _input.Buttons.Set(EInputButton.Sprint, _inputActions.Player.Sprint.IsPressed());
        }

        // Fusion polls accumulated input. This callback can be executed multiple times in a row if there is a performance spike.
        private void OnInput(NetworkRunner runner, NetworkInput networkInput)
        {
            networkInput.Set(_input);
        }

        private Vector2 ClampLookRotation(Vector2 lookRotation)
        {
            lookRotation.x = Mathf.Clamp(lookRotation.x, -30f, 70f);
            return lookRotation;
        }
    }
}