using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkPlayerController : MonoBehaviour
{
    private const string MouseXInput = "Mouse X";
    private const string MouseYInput = "Mouse Y";
    private const string MouseScrollInput = "Mouse ScrollWheel";
    private const string HorizontalInput = "Horizontal";
    private const string VerticalInput = "Vertical";

    public Transform playerCameraTransform;
    public Transform gunPositionTransform;
    public float gravity = 9.81f;
    public float moveSpeed = 10;
    public float sprintSpeed = 15f;
    public float jumpHeight = 3f;
    public float mouseSensitivity = 1f;


    private Vector3 velocity;
    private Vector3 previousPosition;
    private Vector3 targetPosition;
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

        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        playerCamera = FindObjectOfType<PlayerCamera>();

        previousPosition = transform.position;
        targetPosition = transform.position;
        StoreLastInput();


        if (animator != null)
        {
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                Debug.Log("Parameter: " + param.name + " (Type: " + param.type + ")");
            }
        }
        else
        {
            Debug.Log("No Animator component found on this GameObject.");
        }
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

        float t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        transform.position = Vector3.Lerp(previousPosition, targetPosition, t);

        // Apply changes to the camera itself
        playerCamera.transform.position = playerCameraTransform.position;
        playerCamera.transform.rotation = Quaternion.Euler(_lastInputs.LookAxis.y, _lastInputs.LookAxis.x, 0f);
        gunPositionTransform.rotation = playerCamera.transform.rotation;
    }

    void SetAnimations()
    {
        animator.SetBool("IDLE", state == MoveState.IDLE);
        animator.SetBool("WALKING", state == MoveState.WALKING);
        animator.SetBool("RUNNING", state == MoveState.RUNNING);
        animator.SetBool("AIRBORNE", state == MoveState.AIRBORNE);
    }

    void FixedUpdate()
    {
        transform.position = targetPosition;
        previousPosition = transform.position;
        ApplyMovement();
        SetAnimations();
        targetPosition = transform.position;
    }

    private bool IsGrounded() => characterController.isGrounded;
}
