using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 3.5f;
    [SerializeField] private float _sprintSpeed = 6.5f;
    [SerializeField] private float _crouchSpeed = 1.8f;
    [SerializeField] private float _walkSprintTransition = 8f;
    [SerializeField] private float _acceleration = 30f;
    [SerializeField] private float _deceleration = 40f;
    [SerializeField] private float _airControl = 0.4f;
    [SerializeField] private float _rotationSmoothTime = 0.08f;

    [Header("Jump")]
    [SerializeField] private float _jumpHeight = 1.6f;
    [SerializeField] private float _fallMultiplier = 1.5f;
    [SerializeField] private float _coyoteTime = 0.12f;
    [SerializeField] private float _jumpBufferTime = 0.12f;

    [Header("Ground & Step")]
    [SerializeField] private Transform _groundDetector;
    [SerializeField] private float _detectorRadius = 0.2f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private Vector3 _upperStepOffset = new Vector3(0f, 0.4f, 0f);
    [SerializeField] private float _stepCheckerDistance = 0.5f;
    [SerializeField] private float _stepSmooth = 3f;

    [Header("Climb")]
    [SerializeField] private Transform _climbDetector;
    [SerializeField] private float _climbCheckDistance = 1f;
    [SerializeField] private LayerMask _climbableLayer;
    [SerializeField] private Vector3 _climbOffset;
    [SerializeField] private float _climbSpeed = 2.5f;

    [Header("Glide")]
    [SerializeField] private float _glideSpeed = 12f;
    [SerializeField] private float _glideFallSpeed = 3f;
    [SerializeField] private Vector3 _glideRotationSpeed = new Vector3(25f, 60f, 25f);
    [SerializeField] private float _minGlideRotationX = -20f;
    [SerializeField] private float _maxGlideRotationX = 25f;

    [Header("Combat")]
    [SerializeField] private float _resetComboInterval = 1.5f;
    [SerializeField] private Transform _hitDetector;
    [SerializeField] private float _hitDetectorRadius = 1f;
    [SerializeField] private LayerMask _hitLayer;

    [Header("Animation")]
    [SerializeField] private float _walkAnimationVelocity = 0.2f;
    [SerializeField] private float _runAnimationVelocity = 2.8f;

    [Header("References")]
    [SerializeField] private InputManager _input;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private CameraManager _cameraManager;
    [SerializeField] private PlayerAudioManager _playerAudioManager;
    [SerializeField] private Transform _resetCheckpointPosition;

    private Coroutine _resetCombo;
    private Rigidbody _rigidbody;
    private float _speed;
    private float _rotationSmoothVelocity;
    private bool _isGrounded;
    private PlayerStance _playerStance;
    private Animator _animator;
    private CapsuleCollider _collider;
    private bool _isPunching;
    private int _combo = 0;
    private Vector2 _axisDirection;
    private Vector3 _movementDirection;
    private float _coyoteCounter;
    private float _jumpBufferCounter;
    private bool _hasJumpTrigger;
    private bool _hasJumpBool;
    private bool _isJumpAnimationActive;
    private Vector3 rotationDegree = Vector3.zero;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<CapsuleCollider>();
        _speed = _walkSpeed;
        _playerStance = PlayerStance.Stand;
        CacheJumpParameter();
        _input.OnMoveInput += Move;
        _input.OnSprintInput += Sprint;
        _input.OnJumpInput += Jump;
        _input.OnClimbInput += StartClimb;
        _input.OnCancelClimb += CancelClimb;
        _input.OnCrouchInput += Crouch;
        _input.OnGlideInput += StartGlide;
        _input.OnCancelGlide += CancelGlide;
        _input.OnPunchInput += Punch;
        _cameraManager.OnChangePerspective += ChangePerspective;
        HideandLockCursor();
    }

    private void OnDestroy()
    {
        _input.OnMoveInput -= Move;
        _input.OnSprintInput -= Sprint;
        _input.OnJumpInput -= Jump;
        _input.OnClimbInput -= StartClimb;
        _input.OnCancelClimb -= CancelClimb;
        _input.OnCrouchInput -= Crouch;
        _input.OnGlideInput -= StartGlide;
        _input.OnCancelGlide -= CancelGlide;
        _input.OnPunchInput -= Punch;
        _cameraManager.OnChangePerspective -= ChangePerspective;
    }

    private void Update()
    {
        CheckisGrounded();
        CountDownJumpBuffer();
    }

    private void FixedUpdate()
    {
        TryJump();
        ApplyMovement();
        ApplyClimb();
        ApplyGlide();
        CheckStep();
        ApplyExtraGravity();
    }

    private void HideandLockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Move(Vector2 axisDirection)
    {
        _axisDirection = axisDirection;
        _movementDirection = Vector3.zero;
        bool isPLayerStanding = _playerStance == PlayerStance.Stand;
        bool isPlayerClimbing = _playerStance == PlayerStance.Climb;
        bool isPlayerCrouch = _playerStance == PlayerStance.Crouch;
        bool isPlayerGlide = _playerStance == PlayerStance.Glide;
        if ((isPLayerStanding || isPlayerCrouch) && !_isPunching)
        {
            switch(_cameraManager.CameraState)
            {
                case CameraState.ThirdPerson:
                    if (axisDirection.magnitude >= 0.1f)
                    {
                        float rotationAngle = Mathf.Atan2(axisDirection.x, axisDirection.y) * Mathf.Rad2Deg + _cameraTransform.eulerAngles.y;
                        float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, rotationAngle, ref _rotationSmoothVelocity, _rotationSmoothTime);
                        transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
                        _movementDirection = Quaternion.Euler(0f, rotationAngle, 0f) * Vector3.forward;
                    }
                    break;
                case CameraState.FirstPerson:
                    transform.rotation = Quaternion.Euler(0f, _cameraTransform.eulerAngles.y, 0f);
                    Vector3 verticalDirection = axisDirection.y * transform.forward;
                    Vector3 horizontalDirection = axisDirection.x * transform.right;
                    _movementDirection = Vector3.ClampMagnitude(verticalDirection + horizontalDirection, 1f);
                    break;
                default:
                    break;
            }
            UpdateMovementAnimation(isPlayerCrouch);
        }
        else if (isPlayerClimbing)
        {
            Vector3 horizontal = Vector3.zero;
            Vector3 vertical = Vector3.zero;
            Vector3 checkerLeftPosition = transform.position + (transform.up * 1) + (-transform.right * .75f);
            Vector3 checkerRightPosition = transform.position + (transform.up * 1) + (transform.right * 1f);
            Vector3 checkerUpPosition = transform.position + (transform.up * 2.5f);
            Vector3 checkerDownPosition = transform.position + (-transform.up * .25f);
            bool isAbleClimbLeft = Physics.Raycast(checkerLeftPosition, transform.forward, _climbCheckDistance, _climbableLayer);
            bool isAbleClimbRight = Physics.Raycast(checkerRightPosition, transform.forward, _climbCheckDistance, _climbableLayer);
            bool isAbleClimbUp = Physics.Raycast(checkerUpPosition, transform.forward, _climbCheckDistance, _climbableLayer);
            bool isAbleClimbDown = Physics.Raycast(checkerDownPosition, transform.forward, _climbCheckDistance, _climbableLayer);
            
            if ((isAbleClimbLeft && (axisDirection.x < 0)) || (isAbleClimbRight && (axisDirection.x > 0)))
            {
            horizontal = axisDirection.x * transform.right;
            }
            
            if ((isAbleClimbUp && (axisDirection.y > 0)) || (isAbleClimbDown && (axisDirection.y < 0)))
            {
            vertical = axisDirection.y * transform.up;
            }
            
            _movementDirection = horizontal + vertical;
            _rigidbody.AddForce(_movementDirection * Time.deltaTime * _climbSpeed);
            Vector3 velocity = new Vector3(_rigidbody.linearVelocity.x, _rigidbody.linearVelocity.y, 0);
            _animator.SetFloat("ClimbVelocityY", velocity.magnitude * axisDirection.y);
            _animator.SetFloat("ClimbVelocityX", velocity.magnitude * axisDirection.x);
        }
        else if (isPlayerGlide)
        {
            Vector3 rotationDegree = transform.rotation.eulerAngles;
            rotationDegree.x = Mathf.DeltaAngle(0f, rotationDegree.x) + _glideRotationSpeed.x * axisDirection.y * Time.deltaTime;
            rotationDegree.x = Mathf.Clamp(rotationDegree.x, _minGlideRotationX, _maxGlideRotationX);
            rotationDegree.z += _glideRotationSpeed.z * axisDirection.x * Time.deltaTime;
            rotationDegree.y += _glideRotationSpeed.y * axisDirection.x * Time.deltaTime;
            transform.rotation = Quaternion.Euler(rotationDegree);
        }
    }

    private void ApplyMovement()
    {
        bool isPLayerStanding = _playerStance == PlayerStance.Stand;
        bool isPlayerCrouch = _playerStance == PlayerStance.Crouch;
        if (!isPLayerStanding && !isPlayerCrouch)
        {
            return;
        }
        Vector3 currentVelocity = _rigidbody.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        Vector3 targetVelocity = _movementDirection * _speed;
        float changeRate = targetVelocity.sqrMagnitude > 0.01f ? _acceleration : _deceleration;
        if (!_isGrounded)
        {
            changeRate = changeRate * _airControl;
        }
        Vector3 newVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, changeRate * Time.fixedDeltaTime);
        _rigidbody.linearVelocity = new Vector3(newVelocity.x, currentVelocity.y, newVelocity.z);
    }

    private void UpdateMovementAnimation(bool isPlayerCrouch)
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        if (isPlayerCrouch)
        {
            _animator.SetFloat("Velocity", horizontalSpeed);
            return;
        }
        float animationVelocity = GetAnimationVelocity(horizontalSpeed);
        _animator.SetFloat("Velocity", animationVelocity);
        _animator.SetFloat("VelocityZ", animationVelocity * _axisDirection.y);
        _animator.SetFloat("VelocityX", animationVelocity * _axisDirection.x);
    }

    private float GetAnimationVelocity(float horizontalSpeed)
    {
        if (horizontalSpeed <= _walkSpeed)
        {
            return Mathf.Lerp(0f, _walkAnimationVelocity, Mathf.InverseLerp(0f, _walkSpeed, horizontalSpeed));
        }
        return Mathf.Lerp(_walkAnimationVelocity, _runAnimationVelocity, Mathf.InverseLerp(_walkSpeed, _sprintSpeed, horizontalSpeed));
    }

    private void Sprint(bool isSprint)
    {
        if (_playerStance != PlayerStance.Stand)
        {
            return;
        }
        float targetSpeed = isSprint ? _sprintSpeed : _walkSpeed;
        _speed = Mathf.MoveTowards(_speed, targetSpeed, _walkSprintTransition * Time.deltaTime);
    }

    private void Jump()
    {
        if (_isGrounded && !_isPunching)
        {
            _jumpBufferCounter = _jumpBufferTime;
            _animator.SetBool("IsJump", true);
            _animator.SetBool("IsJump", false);
        }
    }

    private void CountDownJumpBuffer()
    {
        if (_jumpBufferCounter > 0f)
        {
            _jumpBufferCounter = _jumpBufferCounter - Time.deltaTime;
        }
    }

    private void TryJump()
    {
        bool isStanceAllowed = _playerStance == PlayerStance.Stand || _playerStance == PlayerStance.Crouch;
        if (!isStanceAllowed || _jumpBufferCounter <= 0f || _coyoteCounter <= 0f)
        {
            return;
        }
        if (_playerStance == PlayerStance.Crouch)
        {
            Crouch();
        }
        _jumpBufferCounter = 0f;
        _coyoteCounter = 0f;
        float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * _jumpHeight);
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = jumpVelocity;
        _rigidbody.linearVelocity = velocity;
        PlayJumpAnimation();
    }

    private void ApplyExtraGravity()
    {
        bool isStanceAffected = _playerStance == PlayerStance.Stand || _playerStance == PlayerStance.Crouch;
        if (!isStanceAffected || _rigidbody.linearVelocity.y >= 0f)
        {
            return;
        }
        _rigidbody.AddForce(Physics.gravity * (_fallMultiplier - 1f), ForceMode.Acceleration);
    }

    private void CacheJumpParameter()
    {
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.name == "Jump" && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                _hasJumpTrigger = true;
            }
            if (parameter.name == "IsJump" && parameter.type == AnimatorControllerParameterType.Bool)
            {
                _hasJumpBool = true;
            }
        }
    }

    private void PlayJumpAnimation()
    {
        if (_hasJumpTrigger)
        {
            _animator.SetTrigger("Jump");
        }
        if (_hasJumpBool)
        {
            _animator.SetBool("IsJump", true);
            _isJumpAnimationActive = true;
        }
    }

    private void CheckisGrounded()
    {
        _isGrounded = Physics.CheckSphere(_groundDetector.position, _detectorRadius, _groundLayer);
        _animator.SetBool("IsGrounded", _isGrounded);
        if (_isGrounded)
        {
            _coyoteCounter = _coyoteTime;
            CancelGlide();
        }
        else
        {
            _coyoteCounter = _coyoteCounter - Time.deltaTime;
            if (_isJumpAnimationActive)
            {
                _animator.SetBool("IsJump", false);
                _isJumpAnimationActive = false;
            }
        }
    }

    private void CheckStep()
    {
        bool isPlayerMoving = _movementDirection.sqrMagnitude > 0.01f;
        if (!_isGrounded || !isPlayerMoving)
        {
            return;
        }
        Vector3 upperOrigin = _groundDetector.position + transform.TransformDirection(_upperStepOffset);
        bool isHitLowerStep = Physics.Raycast(_groundDetector.position, transform.forward, _stepCheckerDistance, _groundLayer);
        bool isHitUpperStep = Physics.Raycast(upperOrigin, transform.forward, _stepCheckerDistance, _groundLayer);
        if (isHitLowerStep && !isHitUpperStep)
        {
            _rigidbody.position += Vector3.up * _stepSmooth * Time.fixedDeltaTime;
        }
    }

    private void StartClimb()
    {
        bool isInFrontOfClimbingWall = Physics.Raycast(_climbDetector.position, transform.forward, out RaycastHit hit, _climbCheckDistance, _climbableLayer);
        bool isNotClimbing = _playerStance != PlayerStance.Climb;
        if (isInFrontOfClimbingWall && _isGrounded && isNotClimbing)
        {
            _cameraManager.SetFPSClampedCamera(true, transform.rotation.eulerAngles);
            Vector3 climbablePoint = hit.collider.bounds.ClosestPoint(transform.position);
            Vector3 direction = (climbablePoint - transform.position).normalized;
            direction.y = 0;
            transform.rotation = Quaternion.LookRotation(direction);
            Vector3 offset = (transform.forward * _climbOffset.z) - (Vector3.up * _climbOffset.y);
            transform.position = hit.point - offset;
            _playerStance = PlayerStance.Climb;
            _animator.SetBool("IsClimbing", true);
            _rigidbody.useGravity = false;
            _cameraManager.SetTPSFieldOfView(70);
        }
    }

    private void ApplyClimb()
    {
        if (_playerStance != PlayerStance.Climb)
        {
            return;
        }
        _rigidbody.linearVelocity = _movementDirection * _climbSpeed;
    }

    private void CancelClimb()
    {
        if (_playerStance == PlayerStance.Climb)
        {
            _playerStance = PlayerStance.Stand;
            _rigidbody.useGravity = true;
            transform.position -= transform.forward;
            _speed = _walkSpeed;
            _animator.SetBool("IsClimbing", false);
            _collider.center = Vector3.up * 0.9f;
            _cameraManager.SetFPSClampedCamera(false, transform.rotation.eulerAngles);
            _cameraManager.SetTPSFieldOfView(60);
        }
    }

    private void ChangePerspective()
    {
        _animator.SetTrigger("ChangePerspective");
    }

    private void Crouch()
    {
        Vector3 checkerUpPosition = transform.position + (transform.up * 1.4f);
        bool isCantStand = Physics.Raycast(checkerUpPosition, transform.up, 0.25f, _groundLayer);
        if (_playerStance == PlayerStance.Stand)
        {
            _playerStance = PlayerStance.Crouch;
            _animator.SetBool("IsCrouch", true);
            _speed = _crouchSpeed;
            _collider.height = 1.3f;
            _collider.center = Vector3.up * 0.66f;
        }
        else if (_playerStance == PlayerStance.Crouch && !isCantStand)
        {
            _playerStance = PlayerStance.Stand;
            _animator.SetBool("IsCrouch", false);
            _speed = _walkSpeed;
            _collider.height = 1.8f;
            _collider.center = Vector3.up * 0.9f;
        }
    }

    private void StartGlide()
    {
        if (_playerStance != PlayerStance.Glide && !_isGrounded)
        {
            rotationDegree = transform.rotation.eulerAngles;
            _playerStance = PlayerStance.Glide;
            _animator.SetBool("IsGliding", true);
            _cameraManager.SetFPSClampedCamera(true, transform.rotation.eulerAngles);
            _playerAudioManager.PlayGlideSfx();
        }
    }

    private void CancelGlide()
    {
        if (_playerStance == PlayerStance.Glide)
        {
            Vector3 rotationDegree = transform.rotation.eulerAngles;
            rotationDegree.x = 0f;
            rotationDegree.z = 0f;
            transform.rotation = Quaternion.Euler(rotationDegree);
            _playerStance = PlayerStance.Stand;
            _animator.SetBool("IsGliding", false);
            _cameraManager.SetFPSClampedCamera(false, transform.rotation.eulerAngles);
            _playerAudioManager.StopGlideSfx();
        }
    }

    private void ApplyGlide()
    {
        if (_playerStance != PlayerStance.Glide)
        {
            return;
        }
        Vector3 glideVelocity = transform.forward * _glideSpeed;
        glideVelocity.y = Mathf.Max(_rigidbody.linearVelocity.y, -_glideFallSpeed);
        _rigidbody.linearVelocity = glideVelocity;
    }

    private void Punch()
    {
        if (!_isPunching && _playerStance == PlayerStance.Stand && _isGrounded)
        {
            _isPunching = true;
            if (_combo < 3)
            {
                _combo = _combo + 1;
            }
            else
            {
                _combo = 1;
            }
            _animator.SetInteger("Combo", _combo);
            _animator.SetTrigger("Punch");
        }
    }

    private void EndPunch()
    {
        _isPunching = false;
        if (_resetCombo != null)
        {
            StopCoroutine(_resetCombo);
        }
        _resetCombo = StartCoroutine(ResetCombo());
    }

    private IEnumerator ResetCombo()
    {
        yield return new WaitForSeconds(_resetComboInterval);
        _combo = 0;
    }

    private void Hit()
    {
        Collider[] hitObjects = Physics.OverlapSphere(_hitDetector.position, _hitDetectorRadius, _hitLayer);
        for (int i = 0; i < hitObjects.Length; i++)
        {
            if (hitObjects[i].gameObject != null)
            {
                Destroy(hitObjects[i].gameObject);
            }
        }
    }
    public void ResetPositionToCheckpoint()
    {
        if (_resetCheckpointPosition != null)
        {
            transform.position = _resetCheckpointPosition.position;
            transform.rotation = _resetCheckpointPosition.rotation;
        }
    }
}
