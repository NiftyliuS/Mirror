using UnityEngine;
using UnityEngine.Rendering;

public class NetworkPlayerController : MonoBehaviour
{
    private const string MouseXInput = "Mouse X";
    private const string MouseYInput = "Mouse Y";
    private const string MouseScrollInput = "Mouse ScrollWheel";
    private const string HorizontalInput = "Horizontal";
    private const string VerticalInput = "Vertical";

    public Transform playerCameraTransform;
    public Transform gunPositionTransform;
    public GameObject fakeGunHolder;
    public Transform headTransform;
    public Transform armTransform;

    public float gravity = 9.81f;
    public float moveSpeed = 10;
    public float sprintSpeed = 15f;
    public float jumpHeight = 3f;
    public float mouseSensitivity = 1f;


    private Vector3 velocity;
    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private Quaternion previousLook;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Quaternion targetLook;

    MoveState state = MoveState.IDLE;
    PlayerCamera playerCamera;


    public enum MoveState : byte { IDLE, WALKING, RUNNING, AIRBORNE }

    public enum WeaponType
    {
        Pistol,
        Rifle,
        Shotgun,
        Sniper
    }

    public struct PlayerCharacterInputs
    {
        public Vector2 MoveAxis;
        public Vector2 LookAxis;
        public WeaponType ActiveWeapon;
        public bool Jump;
        public bool Crouch;
        public bool Run;
        public bool Reload;
        public bool LeftMouse;
        public bool RightMouse;
    }

    PlayerCharacterInputs _lastInputs;
    CharacterController characterController;
    Animator animator;

    private WeaponType activeWeapon = WeaponType.Pistol;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        // local player stuff
        SkinnedMeshRenderer skinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        skinnedRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

        foreach (MeshRenderer gunRendered in fakeGunHolder.GetComponentsInChildren<MeshRenderer>())
            gunRendered.shadowCastingMode = ShadowCastingMode.ShadowsOnly;



        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        playerCamera = FindObjectOfType<PlayerCamera>();

        previousPosition = transform.position;
        previousRotation = transform.rotation;
        previousLook = transform.rotation;

        targetPosition = transform.position;
        targetRotation = transform.rotation;
        targetLook = transform.rotation;

        StoreLastInput();
    }

    void SelectWeapon()
    {
    }

    private void StoreLastInput()
    {
        _lastInputs.MoveAxis = new Vector2(Input.GetAxisRaw(VerticalInput), Input.GetAxisRaw(HorizontalInput));
        _lastInputs.Jump = Input.GetKey(KeyCode.Space);
        _lastInputs.Run = Input.GetKey(KeyCode.LeftShift);

        // update rotation incrementally until its needed
        _lastInputs.LookAxis.x += Input.GetAxisRaw(MouseXInput) * mouseSensitivity;
        _lastInputs.LookAxis.y -= Input.GetAxisRaw(MouseYInput) * mouseSensitivity;

        // limit Y camera movement to prevent head spin
        _lastInputs.LookAxis.y = Mathf.Clamp(_lastInputs.LookAxis.y, -80f, 80f);
    }

    private void ApplyMovement()
    {
        var characterRotation = Quaternion.Euler(0, _lastInputs.LookAxis.x, 0);
        gunPositionTransform.rotation = Quaternion.Euler(_lastInputs.LookAxis.y, _lastInputs.LookAxis.x, 0f);

        transform.rotation = characterRotation;

        // Convert your 2D input to a 3D local move vector
        Vector3 moveDirection = characterRotation * new Vector3(_lastInputs.MoveAxis.y, 0, _lastInputs.MoveAxis.x).normalized;

        // Use rotatedMove to compute horizontal velocity
        velocity.x = moveDirection.x * (_lastInputs.Run ? sprintSpeed : moveSpeed);
        velocity.z = moveDirection.z * (_lastInputs.Run ? sprintSpeed : moveSpeed);

        if (!IsGrounded())
            state = MoveState.AIRBORNE;
        else if (moveDirection.magnitude > 0)
            state = _lastInputs.Run ? MoveState.RUNNING : MoveState.WALKING;
        else state = MoveState.IDLE;

        if (IsGrounded())
            velocity.y = _lastInputs.Jump ? Mathf.Sqrt(2f * jumpHeight * gravity * gravity) : -gravity;
        else
            velocity.y += -gravity * gravity * Time.fixedDeltaTime;

        characterController.Move(velocity * Time.fixedDeltaTime);
    }


    // Update is called once per frame
    void Update()
    {
    }

    void LateUpdate()
    {
        StoreLastInput();

        // Interpolate
        float t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        transform.position = Vector3.Lerp(previousPosition, targetPosition, t);
        transform.rotation = Quaternion.Lerp(previousRotation, targetRotation, t);
        gunPositionTransform.rotation = Quaternion.Lerp(previousLook, targetLook, t);

        // Apply changes to the camera itself and the gun holder - local player only
        playerCamera.transform.position = playerCameraTransform.position;
        playerCamera.transform.rotation = Quaternion.Euler(_lastInputs.LookAxis.y, _lastInputs.LookAxis.x, 0f);
        gunPositionTransform.rotation = playerCamera.transform.rotation;

        RotateHeadAndArm();
    }

    void RotateHeadAndArm()
    {
        // For head, you can directly assign the rotation
        headTransform.rotation = gunPositionTransform.rotation;
        // For arm, use the Euler angle and add your offset
        Vector3 armEuler = armTransform.rotation.eulerAngles;
        armEuler.z = gunPositionTransform.rotation.eulerAngles.x + 90f;
        armTransform.rotation = Quaternion.Euler(armEuler);
    }

    void SetAnimations()
    {
        // animator.Play("ThirdPersonArmAnimationRecoil");
        animator.Play("New State");

        animator.SetLayerWeight(animator.GetLayerIndex("Arm Layer"), 1.0f);
        animator.SetBool("IDLE", state == MoveState.IDLE);
        animator.SetBool("WALKING", state == MoveState.WALKING);
        animator.SetBool("RUNNING", state == MoveState.RUNNING);
        animator.SetBool("AIRBORNE", state == MoveState.AIRBORNE);
    }

    void FixedUpdate()
    {
        transform.position = targetPosition;
        previousPosition = transform.position;
        previousRotation = transform.rotation;
        previousLook = gunPositionTransform.rotation;
        ApplyMovement();
        SetAnimations();
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        targetLook = gunPositionTransform.rotation;
    }

    private bool IsGrounded() => characterController.isGrounded;
}
