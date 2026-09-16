using UnityEngine;

namespace NeuroPeak.Core
{
    public sealed class PeakIntentDriver : MonoBehaviour
    {
        public const float MinMoveDuration = 0.1f;
        public const float MaxMoveDuration = 2f;
        public const float DefaultGrabHold = 0.25f;
        public const float MinLookDegrees = 1f;
        public const float MaxLookDegrees = 180f;

        private const float LookInputMagnitude = 20f;
        private const int LookCalibrationFrames = 2;
        private const float LookStallEpsilon = 0.01f;

        private static PeakIntentDriver? _instance;

        private Vector2 _moveInput;
        private float _moveExpiresAt;
        private float _grabExpiresAt;
        private bool _grabStartPending;
        private bool _releasePending;
        private bool _jumpPending;
        private bool _sprintHeld;
        private bool _sprintStartPending;
        private bool _interactStartPending;
        private float _interactUntil;
        private bool _usePrimaryStartPending;
        private float _usePrimaryUntil;
        private bool _useSecondaryStartPending;
        private float _useSecondaryUntil;
        private bool _dropPending;
        private bool _lookActive;
        private bool _lookHorizontal;
        private float _lookRemaining;
        private float _lookSign;
        private float _lookPrevious;
        private int _lookFrames;

        public static PeakIntentDriver? Instance => _instance;

        public bool MoveActive => Time.time < _moveExpiresAt;
        public bool GrabActive => Time.time < _grabExpiresAt;
        public bool SprintHeld => _sprintHeld;

        public bool LookActive => _lookActive;

        public void Move(MoveDirection direction, float duration)
        {
            _moveInput = direction.ToInput();
            _moveExpiresAt = Time.time + Mathf.Clamp(duration, MinMoveDuration, MaxMoveDuration);
        }

        public void StopMoving()
        {
            _moveInput = Vector2.zero;
            _moveExpiresAt = 0f;
        }

        public void Look(LookDirection direction, float degrees)
        {
            _lookActive = true;
            _lookHorizontal = direction.IsHorizontal();
            _lookRemaining = Mathf.Clamp(degrees, MinLookDegrees, MaxLookDegrees);
            _lookSign = direction.NominalSign();
            _lookFrames = 0;
        }

        public void StopLooking() => _lookActive = false;

        public void Interact(float holdSeconds)
        {
            _interactStartPending = true;
            _interactUntil = Time.time + Mathf.Max(holdSeconds, 0.1f);
        }

        public void UsePrimary(float holdSeconds)
        {
            _usePrimaryStartPending = true;
            _usePrimaryUntil = Time.time + Mathf.Max(holdSeconds, 0.1f);
        }

        public void UseSecondary(float holdSeconds)
        {
            _useSecondaryStartPending = true;
            _useSecondaryUntil = Time.time + Mathf.Max(holdSeconds, 0.1f);
        }

        public void DropItem() => _dropPending = true;

        public void Jump() => _jumpPending = true;

        public void Grab(float holdSeconds)
        {
            _grabStartPending = true;
            _releasePending = false;
            _grabExpiresAt = Time.time + Mathf.Max(holdSeconds, DefaultGrabHold);
        }

        public void Release()
        {
            _grabExpiresAt = 0f;
            _grabStartPending = false;
            _releasePending = true;
        }

        public void SetSprint(bool enabled)
        {
            if (enabled && !_sprintHeld) _sprintStartPending = true;
            _sprintHeld = enabled;
        }

        public void ClearAll()
        {
            StopMoving();
            _grabExpiresAt = 0f;
            _grabStartPending = false;
            _releasePending = false;
            _jumpPending = false;
            _sprintHeld = false;
            _sprintStartPending = false;
            _lookActive = false;
            _interactUntil = 0f;
            _usePrimaryUntil = 0f;
            _useSecondaryUntil = 0f;
            _dropPending = false;
        }

        internal void ApplyTo(Character character, CharacterInput input)
        {
            ApplyLook(character, input);

            if (MoveActive) input.movementInput = _moveInput;

            if (_jumpPending)
            {
                input.jumpWasPressed = true;
                input.jumpIsPressed = true;
                _jumpPending = false;
            }

            if (GrabActive)
            {
                input.usePrimaryIsPressed = true;
                if (_grabStartPending)
                {
                    input.usePrimaryWasPressed = true;
                    _grabStartPending = false;
                }
            }

            if (_releasePending)
            {
                input.usePrimaryIsPressed = false;
                input.usePrimaryWasPressed = false;
                input.usePrimaryWasReleased = true;
                _releasePending = false;
            }

            if (Time.time < _interactUntil)
            {
                input.interactIsPressed = true;
                if (_interactStartPending)
                {
                    input.interactWasPressed = true;
                    _interactStartPending = false;
                }
            }

            if (Time.time < _usePrimaryUntil)
            {
                input.usePrimaryIsPressed = true;
                if (_usePrimaryStartPending)
                {
                    input.usePrimaryWasPressed = true;
                    _usePrimaryStartPending = false;
                }
            }

            if (Time.time < _useSecondaryUntil)
            {
                input.useSecondaryIsPressed = true;
                if (_useSecondaryStartPending)
                {
                    input.useSecondaryWasPressed = true;
                    _useSecondaryStartPending = false;
                }
            }

            if (_dropPending)
            {
                input.dropWasPressed = true;
                _dropPending = false;
            }

            if (_sprintHeld)
            {
                input.sprintIsPressed = true;
                if (_sprintStartPending)
                {
                    input.sprintWasPressed = true;
                    _sprintStartPending = false;
                }
            }
        }

        private void ApplyLook(Character character, CharacterInput input)
        {
            if (!_lookActive) return;

            CharacterData data = character.data;
            if (data == null)
            {
                _lookActive = false;
                return;
            }

            float current = _lookHorizontal ? data.lookValues.x : data.lookValues.y;

            if (_lookFrames > 0)
            {
                float travelled = Mathf.DeltaAngle(_lookPrevious, current);
                _lookRemaining -= Mathf.Abs(travelled);

                bool movingWrongWay = travelled * _lookSign < 0f;
                bool stalled = Mathf.Abs(travelled) < LookStallEpsilon;

                if (movingWrongWay) _lookSign = -_lookSign;
                else if (stalled && _lookFrames <= LookCalibrationFrames) _lookSign = -_lookSign;

                if (_lookRemaining <= 0f)
                {
                    _lookActive = false;
                    return;
                }
            }

            _lookPrevious = current;
            _lookFrames++;

            float magnitude = Mathf.Min(LookInputMagnitude, Mathf.Max(_lookRemaining, MinLookDegrees));
            if (_lookHorizontal) input.lookInput = new Vector2(_lookSign * magnitude, input.lookInput.y);
            else input.lookInput = new Vector2(input.lookInput.x, _lookSign * magnitude);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
