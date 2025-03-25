using System;
using Mirror.Components.Experimental;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Mirror.Examples.FpsArena.Scripts
{
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

        public GunScript pistol;
        public GunScript rifle;
        public GunScript shotgun;
        public GunScript sniper;

        Vector3 velocity;
        Vector3 previousPosition;
        Quaternion previousRotation;
        Quaternion previousLook;

        Vector3 transientPosition;
        Quaternion transientRotation;
        Quaternion transientLook;
        Vector3 defaultGunPosition;

        MoveState moveState = MoveState.IDLE;
        PlayerCamera playerCamera;

        public float maxDistance = 100f;
        LineRenderer lineRenderer;

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

        WeaponType activeWeapon = WeaponType.Sniper;


        AdditionalNetworkPlayerState playerState = new AdditionalNetworkPlayerState();

        void InitLineRenderer()
        {
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

        // Start is called before the first frame update
        void Start()
        {
            if (sendStateChanges)
                Debug.LogWarning("Current weapon setup is not build for 'send state changes'! and will cause a large network overhead.");

            if (isLocalPlayer)
            {
                // Capture mouse and setup camera
                Cursor.lockState = CursorLockMode.Locked;
                playerCamera = FindObjectOfType<PlayerCamera>();

                // make the main body to only cast shadows
                SkinnedMeshRenderer skinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
                skinnedRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

                // Do the same for guns held by the character
                foreach (MeshRenderer gunRendered in fakeGunHolder.GetComponentsInChildren<MeshRenderer>())
                    gunRendered.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

            // Get attached character controller
            characterController = GetComponent<CharacterController>();

            // Get animation and give more weight to the Arm Layer
            animator = GetComponentInChildren<Animator>();
            animator.SetLayerWeight(animator.GetLayerIndex("Arm Layer"), 1.0f);

            // Setup initial start positions for interpolation
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            previousLook = transform.rotation;

            // Setup initial target positions for interpolation
            transientPosition = transform.position;
            transientRotation = transform.rotation;
            transientLook = transform.rotation;

            // Record tiny guns positin and rotation to rever when not zoomed in easely
            defaultGunPosition = gunPosition.localPosition;

            // hide or show tiny guns inside the head based on if local player or not...
            Renderer[] renderers = gunHolder.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers) renderer.enabled = isLocalPlayer;

            // Setup the "bullet" trajectory line renderer
            InitLineRenderer();

            SelectGun(WeaponType.Sniper);
        }

        // Simple 8 flags to bye example for key and click inputs
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

        // Example of reading key inputs from as single byte
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

        public override void ResetPlayerState(NetworkPlayerState state) => SetPlayerState(state);

        public override void ResetPlayerInputs(NetworkPlayerInputs inputs) => SetPlayerInputs(inputs);

        public override NetworkPlayerState GetPlayerState()
        {
            NetworkWriter writer = new NetworkWriter();

            writer.Write(playerState);

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
            if (state.AdditionalState.HasValue)
            {
                playerState = new NetworkReader(state.AdditionalState.Value.ToArray())
                    .Read<AdditionalNetworkPlayerState>();
            }
        }

        GunScript GetActiveGun()
        {
            if (activeWeapon == WeaponType.Rifle) return rifle;
            if (activeWeapon == WeaponType.Shotgun) return shotgun;
            if (activeWeapon == WeaponType.Sniper) return sniper;
            return pistol;
        }

        void SelectGun(WeaponType gunType)
        {
            if (activeWeapon != gunType)
            {
                activeWeapon = gunType;

                pistol.SetEnabled(gunType == WeaponType.Pistol, isLocalPlayer, playerState.SinceReload);
                rifle.SetEnabled(gunType == WeaponType.Rifle, isLocalPlayer, playerState.SinceReload);
                shotgun.SetEnabled(gunType == WeaponType.Shotgun, isLocalPlayer, playerState.SinceReload);
                sniper.SetEnabled(gunType == WeaponType.Sniper, isLocalPlayer, playerState.SinceReload);
            }
        }
        void ReloadGun()
        {
            var gun = GetActiveGun();
            if (gun.CanReload())
            {
                gun.ReloadMagazine();
                playerState.SinceReload = 0;
                Debug.Log("Reloading!");
            }
        }

        private void FireGun()
        {
            var gun = GetActiveGun();
            if (!gun.CanFire(playerState.SinceFire, playerState.SinceReload))
            {
                return;
            }

            nextInputs.LookAxis.x -= gun.recoilX + Random.Range(-gun.recoilX * 0.5f, gun.recoilX * 0.5f);
            nextInputs.LookAxis.y -= gun.recoilY + Random.Range(-gun.recoilY * 0.5f, gun.recoilY * 0.5f);

            playerState.SinceFire = 0;
            gun.ReduceMagazine();
            Debug.Log(gun.name + " " + gun.GetMagazine());

            RaycastHit hit;
            Transform originTransform = isLocalPlayer ? gunPosition.transform : fakeGunHolder.transform;
            Vector3 origin = originTransform.position;
            Vector3 direction = originTransform.forward;

            // Offset the origin by 1 meter in the forward direction
            Vector3 offsetOrigin = origin + direction * (isLocalPlayer ? 1.5f : 3f);
            // Optionally adjust the y-position if needed
            lineRenderer.SetPosition(0, new Vector3(offsetOrigin.x, offsetOrigin.y + (isLocalPlayer ? 0 : 0.4f), offsetOrigin.z));

            if (Physics.Raycast(characterAimTransform.position, characterAimTransform.forward, out hit, maxDistance))
                lineRenderer.SetPosition(1, hit.point);
            else
                lineRenderer.SetPosition(1, offsetOrigin + direction * maxDistance);

            lineRenderer.enabled = true;
        }


        // Basically do the physics and game logic here based on the lastest inputs
        void ApplyMovement()
        {
            if (characterController is null) return;

            SelectGun( (WeaponType)tickInputs.ActiveWeapon);
            // Update gun tickers
            playerState.SinceFire = (byte)Math.Min(playerState.SinceFire + 1, 250);
            playerState.SinceReload = (byte)Math.Min(playerState.SinceReload + 1, 250);

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

            // Determine animation state
            if (!IsGrounded())
                moveState = MoveState.AIRBORNE;
            else if (moveDirection.magnitude > 0)
                moveState = tickInputs.Run ? MoveState.RUNNING : MoveState.WALKING;
            else moveState = MoveState.IDLE;

            // Check if needs to accelerate down or is on the ground ( constant speed )
            if (IsGrounded())
                velocity.y = tickInputs.Jump ? Mathf.Sqrt(2f * jumpHeight * gravity * gravity) : -gravity;
            else
                velocity.y += -gravity * gravity * Time.fixedDeltaTime;

            characterController.Move(velocity * Time.fixedDeltaTime);

            // Check if player wants to fire his gun
            if (tickInputs.LeftMouse) FireGun();
            if (tickInputs.Reload) ReloadGun();
        }

        // Replacement for FixedUpdate that happens once per tick after states and inputs are set
        public override void NetworkUpdate(int deltaTicks, float deltaTime)
        {
            // Dont record offset position and rotation when reconciling to allow for a somewhat smooth after reconcile
            if (!NetworkTick.IsReconciling)
            {
                previousPosition = transform.position;
                previousRotation = transform.rotation;
                previousLook = gunHolder.transform.rotation;
            }

            if(lineRenderer is not null) lineRenderer.enabled = false;
            transform.position = transientPosition;
            transform.rotation = transientRotation;

            ApplyMovement();
            SetAnimations();
            transientPosition = transform.position;
            transientRotation = transform.rotation;
            transientLook = gunHolder.transform.rotation;
        }

        bool IsGrounded() => characterController?.isGrounded ?? false;

        #region Visual and LocalPlayer Inputs

        // Collect current input state at LateUpdate of every frame to ensure the most recent inputs are recorded before any NetworkUpdate is called
        void StoreLastInput()
        {
            // Record any key presses and bools
            nextInputs.Jump = Input.GetKey(KeyCode.Space);
            nextInputs.Run = Input.GetKey(KeyCode.LeftShift);
            nextInputs.Crouch = Input.GetKey(KeyCode.LeftControl);
            nextInputs.Reload = Input.GetKey(KeyCode.R);
            nextInputs.LeftMouse = Input.GetMouseButton(0);
            nextInputs.RightMouse = Input.GetMouseButton(1);

            float scrollInput = Input.GetAxis(MouseScrollInput);
            if (scrollInput > 0)
                nextInputs.ActiveWeapon = (WeaponType)(((byte)activeWeapon + 1) % 4);
            else if (scrollInput < 0)
                nextInputs.ActiveWeapon = (WeaponType)(((byte)activeWeapon == 0 ? 3 : (byte)activeWeapon - 1));
            else if (Input.GetKey(KeyCode.Alpha1)) nextInputs.ActiveWeapon = WeaponType.Pistol;
            else if (Input.GetKey(KeyCode.Alpha2)) nextInputs.ActiveWeapon = WeaponType.Rifle;
            else if (Input.GetKey(KeyCode.Alpha3)) nextInputs.ActiveWeapon = WeaponType.Shotgun;
            else if (Input.GetKey(KeyCode.Alpha4)) nextInputs.ActiveWeapon = WeaponType.Sniper;

            // Store move as 2d axis
            nextInputs.MoveAxis = new Vector2(Input.GetAxisRaw(VerticalInput), Input.GetAxisRaw(HorizontalInput));

            // update rotation with mouse changes incrementally until its needed
            nextInputs.LookAxis.x += Input.GetAxisRaw(MouseXInput) * (nextInputs.RightMouse ? aimMouseSensitivity : mouseSensitivity);
            nextInputs.LookAxis.y -= Input.GetAxisRaw(MouseYInput) * (nextInputs.RightMouse ? aimMouseSensitivity : mouseSensitivity);

            // limit Y camera movement to prevent head spin
            nextInputs.LookAxis.y = Mathf.Clamp(nextInputs.LookAxis.y, -80f, 80f);
        }

        // Collect inputs + Visual stuff to remove jerkiness with interpolation and animations - purely visual
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
                if (isLocalPlayer)
                    playerCamera.setFov(nextInputs.RightMouse ? 40 : 60);

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

            // Ensure the gun and the head of the character are pointing roughly in the right direction of the players aim
            RotateHeadAndArm();
        }


        // Animations related to gun fire and look direction
        void RotateHeadAndArm()
        {
            // For head, you can directly assign the rotation
            headTransform.rotation = characterAimTransform.transform.rotation;
            // For arm, use the Euler angle and add your offset
            Vector3 armEuler = armTransform.rotation.eulerAngles;
            armEuler.z = characterAimTransform.transform.rotation.eulerAngles.x + 80f;
            armTransform.rotation = Quaternion.Euler(armEuler);
            // fancy tiny gun animation
            if (tickInputs.LeftMouse)
                gunPosition.localPosition += new Vector3(
                    RightArm.transform.localPosition.x * 10f,
                    RightArm.transform.localPosition.y,
                    RightArm.transform.localPosition.z * -100f
                );
            // fancy tiny gun zoom in effect
            if (tickInputs.RightMouse)
                RightArm.transform.localPosition += new Vector3(0.002f, -0.0052f, -0.0065f);
        }

        // Character animations togglers
        void SetAnimations()
        {
            if (animator is null) return;

            var isFiring = tickInputs.LeftMouse && GetActiveGun().GetMagazine()>0 && playerState.SinceFire*Time.fixedDeltaTime < 0.12f;

            animator.Play(isFiring ? "ThirdPersonArmAnimationRecoil" : "New State");

            animator.SetBool("IDLE", moveState == MoveState.IDLE);
            animator.SetBool("WALKING", moveState == MoveState.WALKING);
            animator.SetBool("RUNNING", moveState == MoveState.RUNNING);
            animator.SetBool("AIRBORNE", moveState == MoveState.AIRBORNE);
        }

        #endregion
    }
}
