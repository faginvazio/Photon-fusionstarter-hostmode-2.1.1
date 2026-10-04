using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    public enum EInputButton
    {
        Jump,
        Fire,
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
    /// This version of PlayerInput showcases usage of IBeforeUpdate and IAfterTick callbacks.
    /// </summary>
    public sealed class PlayerInput : NetworkBehaviour, IBeforeUpdate, IAfterTick
    {
        [Networked]
        public NetworkButtons PreviousButtons { get; private set; }
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

        // BeforeUpdate is called during Unity's Update loop before any OnInput/FixedUpdateNetwork/Render functions are executed.
        // Therefore using BeforeUpdate to accumulate input is slightly more precise than doing so in Update function as the latest input
        // will be already used in FixedUpdateNetwork if it will be called in this update loop. This gets more important the lower render rate the player has.
        void IBeforeUpdate.BeforeUpdate()
        {
            // Accumulate input from Keyboard/Mouse. Input accumulation is mandatory (at least for the look rotation) as Update can be
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
            _input.LookRotation += new Vector2(-lookValue.y, lookValue.x);

            _input.MoveDirection = _inputActions.Player.Move.ReadValue<Vector2>();

            _input.Buttons.Set(EInputButton.Fire, _inputActions.Player.Attack.IsPressed());
            _input.Buttons.Set(EInputButton.Jump, _inputActions.Player.Jump.IsPressed());
        }

        // AfterTick is called after all FixedUpdateNetwork calls on NetworkBehaviours were executed for this tick.
        // It is perfect for actions that should be executed at the end of the tick.
        void IAfterTick.AfterTick()
        {
            // Save current button input (if any) as previous.
            // Previous buttons need to be networked to detect correctly pressed/released events.
            if (GetInput(out GameplayInput input))
            {
                PreviousButtons = input.Buttons;
            }
        }

        // Fusion polls accumulated input. This callback can be executed multiple times in a row if there is a performance spike.
        private void OnInput(NetworkRunner runner, NetworkInput networkInput)
        {
            networkInput.Set(_input);
        }
    }
}