using System;
using Mirror;
using Mirror.Components.Experimental;
using UnityEngine;
using UnityEngine.Rendering;

public class NetworkPlayerController : NetworkPlayerControllerBase
{
    private const string MouseXInput = "Mouse X";
    private const string MouseYInput = "Mouse Y";
    private const string MouseScrollInput = "Mouse ScrollWheel";
    private const string HorizontalInput = "Horizontal";
    private const string VerticalInput = "Vertical";

    public Transform characterAimTransform;
    public GameObject gunHolder;
    public Transform gunPosition;
    public GameObject fakeGunHolder;
    public Transform headTransform;
    public Transform armTransform;
    public Transform characterBody;
    public GameObject RightArm;

    public float gravity = 9.81f;
    public float moveSpeed = 10;
    public float sprintSpeed = 15f;
    public float jumpHeight = 3f;
    public float mouseSensitivity = 1f;
    public float aimMouseSensitivity = 0.5f;

    private Vector3 velocity;
    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private Quaternion previousLook;

    private Vector3 transientPosition;
    private Quaternion transientRotation;
    private Quaternion transientLook;
    Vector3 defaultGunPosition;
    Quaternion defaultGunRotation;

    MoveState state = MoveState.IDLE;
    PlayerCamera playerCamera;


    public float maxDistance = 100f;
    private LineRenderer lineRenderer;

    public enum MoveState : byte { IDLE, WALKING, RUNNING, AIRBORNE }

    public enum WeaponType : byte
    {
        Pistol = 0,
        Rifle = 1,
        Shotgun = 2,
        Sniper = 3
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

    PlayerCharacterInputs nextInputs;
    PlayerCharacterInputs tickInputs;
    CharacterController characterController;
    Animator animator;

    WeaponType activeWeapon = WeaponType.Pistol;


    public struct AdditionalPlayerState
    {
        public byte health;
    }

    AdditionalPlayerState playerState = new AdditionalPlayerState();

    // This is here to FORCE Weaver to serialize the AdditionalPlayerState struct
    [ClientRpc]
    void FakeRpc(AdditionalPlayerState state) { }

    // Start is called before the first frame update
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        previousPosition = transform.position;
        previousRotation = transform.rotation;
        previousLook = transform.rotation;

        transientPosition = transform.position;
        transientRotation = transform.rotation;
        transientLook = transform.rotation;
        defaultGunPosition = gunPosition.localPosition;
        defaultGunRotation = gunPosition.localRotation;

        if (isLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            SkinnedMeshRenderer skinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            skinnedRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            foreach (MeshRenderer gunRendered in fakeGunHolder.GetComponentsInChildren<MeshRenderer>())
                gunRendered.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            StoreLastInput();
            playerCamera = FindObjectOfType<PlayerCamera>();
        }
        else
        {
            // hide tiny guns inside the head...
            Renderer[] renderers = gunHolder.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = false;
            }
        }


        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        // Set up a gradient for a bullet-fired effect: bright at the start, fading to transparent
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.8f, 0.7f), 0.0f), // bright yellow/orange
                new GradientColorKey(new Color(1f, 0.8f, 0.5f), 1.0f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.3f, 0.0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(1f, 1.0f)
            }
        );
        lineRenderer.colorGradient = gradient;
    }

    public override NetworkPlayerInputs GetPlayerInputs()
    {
        NetworkWriter writer = new NetworkWriter();
        int packedInt = 0;

        // Booleans
        packedInt |= nextInputs.Jump ? 1 << 0 : 0; // Bit 0
        packedInt |= nextInputs.Crouch ? 1 << 1 : 0; // Bit 1
        packedInt |= nextInputs.Run ? 1 << 2 : 0; // Bit 2
        packedInt |= nextInputs.Reload ? 1 << 3 : 0; // Bit 3
        packedInt |= nextInputs.LeftMouse ? 1 << 4 : 0; // Bit 4
        packedInt |= nextInputs.RightMouse ? 1 << 5 : 0; // Bit 5

        // WeaponType in bits 6–7
        int weaponVal = ((int)nextInputs.ActiveWeapon & 0x03) << 6;
        packedInt |= weaponVal;

        writer.WriteByte((byte)packedInt);

        return new NetworkPlayerInputs() { MovementVector = nextInputs.MoveAxis, MouseVector = nextInputs.LookAxis, AdditionalInputs = writer.ToArray() };
    }

    public override void SetPlayerInputs(NetworkPlayerInputs inputs)
    {
        // Update movement and look axes
        if (inputs.MovementVector.HasValue) tickInputs.MoveAxis = inputs.MovementVector.Value;
        if (inputs.MouseVector.HasValue) tickInputs.LookAxis = inputs.MouseVector.Value;

        if (inputs.AdditionalInputs.HasValue && inputs.AdditionalInputs.Value.Length > 0)
        {
            byte packedInput = inputs.AdditionalInputs.Value.Span[0];

            // Extract each boolean value using bit masks.
            tickInputs.Jump = (packedInput & (1 << 0)) != 0;
            tickInputs.Crouch = (packedInput & (1 << 1)) != 0;
            tickInputs.Run = (packedInput & (1 << 2)) != 0;
            tickInputs.Reload = (packedInput & (1 << 3)) != 0;
            tickInputs.LeftMouse = (packedInput & (1 << 4)) != 0;
            tickInputs.RightMouse = (packedInput & (1 << 5)) != 0;

            // Extract the 2-bit WeaponType from bits 6–7.
            int weaponVal = (packedInput >> 6) & 0x03;
            tickInputs.ActiveWeapon = (WeaponType)weaponVal;
        }
    }

    public override NetworkPlayerState GetPlayerState()
    {
        NetworkWriter writer = new NetworkWriter();

        writer.Write<AdditionalPlayerState>(playerState);

        var additionalData = writer.ToArray();
        // Debug.Log(additionalData);
        return new NetworkPlayerState()
        {
            Position = transientPosition, Rotation = transientRotation, BaseVelocity = velocity, AdditionalState = additionalData,
        };
    }

    public override void SetPlayerState(NetworkPlayerState state)
    {
        if (state.Position.HasValue) transientPosition = state.Position.Value;
        if (state.Rotation.HasValue) transientRotation = state.Rotation.Value;
        if (state.BaseVelocity.HasValue) velocity = state.BaseVelocity.Value;
    }

    public override void ResetPlayerState(NetworkPlayerState state) => SetPlayerState(state);

    public override void ResetPlayerInputs(NetworkPlayerInputs inputs) => SetPlayerInputs(inputs);


    private void StoreLastInput()
    {
        nextInputs.MoveAxis = new Vector2(Input.GetAxisRaw(VerticalInput), Input.GetAxisRaw(HorizontalInput));
        nextInputs.Jump = Input.GetKey(KeyCode.Space);
        nextInputs.Run = Input.GetKey(KeyCode.LeftShift);
        nextInputs.Crouch = Input.GetKey(KeyCode.LeftControl);
        nextInputs.LeftMouse = Input.GetMouseButton(0);
        nextInputs.RightMouse = Input.GetMouseButton(1);

        // update rotation incrementally until its needed
        nextInputs.LookAxis.x += Input.GetAxisRaw(MouseXInput) * (nextInputs.RightMouse ? aimMouseSensitivity : mouseSensitivity);
        nextInputs.LookAxis.y -= Input.GetAxisRaw(MouseYInput) * (nextInputs.RightMouse ? aimMouseSensitivity : mouseSensitivity);

        // limit Y camera movement to prevent head spin
        nextInputs.LookAxis.y = Mathf.Clamp(nextInputs.LookAxis.y, -80f, 80f);
    }

    private void FireGun()
    {
        if (NetworkTick.ClientAbsoluteTick % 5 != 0) return;
        RaycastHit hit;
        Transform originTransform = isLocalPlayer ? gunPosition.transform : fakeGunHolder.transform;
        Vector3 origin = originTransform.position;
        Vector3 direction = originTransform.forward;

        // Offset the origin by 1 meter in the forward direction
        Vector3 offsetOrigin = origin + direction * (isLocalPlayer ? 1.5f : 3f);
        // Optionally adjust the y-position if needed
        lineRenderer.SetPosition(0, new Vector3(offsetOrigin.x, offsetOrigin.y + (isLocalPlayer ? 0 : 0.4f), offsetOrigin.z));

        if (Physics.Raycast(characterAimTransform.position, characterAimTransform.forward, out hit, maxDistance))
        {
            lineRenderer.SetPosition(1, hit.point);
        }
        else
        {
            if (isLocalPlayer)
            {
            }
            else
            {
                lineRenderer.SetPosition(1, offsetOrigin + direction * maxDistance);
            }
        }

        lineRenderer.enabled = true;
    }


    private void ApplyMovement()
    {
        if (characterController is null) return;

        // Update aiming rotation correctly
        characterAimTransform.rotation = Quaternion.Euler(tickInputs.LookAxis.y, tickInputs.LookAxis.x, 0f);


        var characterRotation = Quaternion.Euler(0, tickInputs.LookAxis.x, 0);
        transform.rotation = characterRotation;

        // Crouch the controller
        characterController.height = tickInputs.Crouch ? 2.6f : 3.7f;

        // Fake crouch animation!
        characterController.center = new Vector3(0, tickInputs.Crouch ? 2.3f : 1.9f, 0f);
        characterBody.transform.localScale = new Vector3(0.97f, tickInputs.Crouch ? 0.7f : 0.97f, 0.97f);
        characterBody.transform.localPosition = new Vector3(0, tickInputs.Crouch ? 1f : 0, -0.25f);

        // Convert your 2D input to a 3D local move vector
        Vector3 moveDirection = characterRotation * new Vector3(tickInputs.MoveAxis.y, 0, tickInputs.MoveAxis.x).normalized;

        // Use rotatedMove to compute horizontal velocity
        velocity.x = moveDirection.x * (tickInputs.Run ? sprintSpeed : moveSpeed) * (tickInputs.Crouch ? 0.5f : 1f);
        velocity.z = moveDirection.z * (tickInputs.Run ? sprintSpeed : moveSpeed) * (tickInputs.Crouch ? 0.5f : 1f);

        if (!IsGrounded())
            state = MoveState.AIRBORNE;
        else if (moveDirection.magnitude > 0)
            state = tickInputs.Run ? MoveState.RUNNING : MoveState.WALKING;
        else state = MoveState.IDLE;

        if (IsGrounded())
            velocity.y = tickInputs.Jump ? Mathf.Sqrt(2f * jumpHeight * gravity * gravity) : -gravity;
        else
            velocity.y += -gravity * gravity * Time.fixedDeltaTime;
        characterController.Move(velocity * Time.fixedDeltaTime);

        if (tickInputs.LeftMouse)
        {
            FireGun();
        }
    }

    public override void NetworkUpdate(int deltaTicks, float deltaTime)
    {
        if (!NetworkTick.IsReconciling)
        {
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            previousLook = gunHolder.transform.rotation;
        }

        if (lineRenderer is not null) lineRenderer.enabled = false;
        transform.position = transientPosition;
        transform.rotation = transientRotation;

        ApplyMovement();
        SetAnimations();
        transientPosition = transform.position;
        transientRotation = transform.rotation;
        transientLook = gunHolder.transform.rotation;
    }

    bool IsGrounded() => characterController?.isGrounded ?? false;

    void LateUpdate()
    {
        // Interpolate, animate and make things smooth in general
        float t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        transform.position = Vector3.Lerp(previousPosition, transientPosition, t);
        transform.rotation = Quaternion.Lerp(previousRotation, transientRotation, t);
        gunHolder.transform.rotation = Quaternion.Lerp(previousLook, transientLook, t);

        // Apply changes to the camera itself and the gun holder - local player only
        if (isLocalPlayer)
        {
            StoreLastInput();
            playerCamera.transform.position = characterAimTransform.position;
            playerCamera.transform.rotation = Quaternion.Euler(nextInputs.LookAxis.y, nextInputs.LookAxis.x, 0f);
            gunHolder.transform.rotation = playerCamera.transform.rotation;
            // set the gun slightly ahead of the camera to give it some movement when looking up/down
            gunHolder.transform.position = playerCamera.transform.position;
            if (!nextInputs.RightMouse)
            {
                gunPosition.localScale = new Vector3(1f, 1f, 1f);
                gunPosition.localPosition = defaultGunPosition;
                gunHolder.transform.position += Vector3.Normalize(
                    new Vector3(playerCamera.transform.forward.x, 0f, playerCamera.transform.forward.z)) * 0.05f;
            }
            else
            {
                gunPosition.localScale = new Vector3(2f, 2f, 2f);
                gunPosition.localPosition = new Vector3(0, -0.45f, 1.5f);
            }
        }

        RotateHeadAndArm();
    }


    void RotateHeadAndArm()
    {
        // For head, you can directly assign the rotation
        headTransform.rotation = characterAimTransform.transform.rotation;
        // For arm, use the Euler angle and add your offset
        Vector3 armEuler = armTransform.rotation.eulerAngles;
        armEuler.z = characterAimTransform.transform.rotation.eulerAngles.x + 80f;
        armTransform.rotation = Quaternion.Euler(armEuler);
        if (tickInputs.RightMouse)
            RightArm.transform.localPosition += new Vector3(0.002f, -0.0052f, -0.0065f);
    }

    void SetAnimations()
    {
        if (animator is null) return;

        animator.Play(tickInputs.LeftMouse ? "ThirdPersonArmAnimationRecoil" : "New State");
        animator.SetLayerWeight(animator.GetLayerIndex("Arm Layer"), 1.0f);

        animator.SetBool("IDLE", state == MoveState.IDLE);
        animator.SetBool("WALKING", state == MoveState.WALKING);
        animator.SetBool("RUNNING", state == MoveState.RUNNING);
        animator.SetBool("AIRBORNE", state == MoveState.AIRBORNE);
    }
}
