using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    #region MechanicUseBool
    //properties used to help check whether player can use certain mechanics. These are mostly to keep the code clean and organized
    //Kind of a rudimentary/crude state machine
    public bool PlayerCanMove { get; private set; } = true;
    private bool PlayerIsSprinting => playerCanSprint && Input.GetKey(sprintKey) && !playerIsCrouching && currentStamina > 0;
    private bool PlayerShouldJump => Input.GetKeyDown(jumpKey) && characterController.isGrounded && !playerIsCrouching;
    private bool PlayerShouldCrouch => Input.GetKeyDown(crouchKey) && !playerInCrouchAnimation && characterController.isGrounded;
    #endregion

    #region MechanicBools
    //Checks used to see if player is able to use mechanics.
    [Header("Functional Options")]
    [Tooltip("Is the player in the middle of a special movement, i.e. ladder climbing?")]
    [SerializeField] public bool playerOnSpecialMovement = false;
    [SerializeField] private bool playerCanSprint = true;
    [SerializeField] private bool playerCanJump = true;
    [SerializeField] private bool playerCanCrouch = true;
    [SerializeField] private bool playerCanHeadbob = true;
    #endregion

    #region Keybinds
    //The keys that players must press to use mechanics/actions
    [Header("Controls")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    #endregion

    #region MovementMetrics
    //parameters for different movement speeds
    [Header("Movement Parameters")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float fovDefault = 60f;
    [SerializeField] private float fovSprint = 70f;
    [SerializeField] private float fovIncrement = 5f;
    #endregion

    #region Stamina
    [Header("Stamina Parameters")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenRate = 10f;
    [SerializeField] private float staminaDepletionRate = 20f;
    [SerializeField] private float currentStamina;
    [SerializeField] private float staminaRegenDelay = 5f;
    [SerializeField] private float staminaRegenTimer;
    #endregion

    #region CameraParameters
    //Parameters for looking around with mouse
    [Header("Look Parameters")]
    [SerializeField, Range(1, 10)]
    private float lookSpeedX = 2f;
    [SerializeField, Range(1, 10)]
    private float lookSpeedY = 2f;
    [SerializeField, Range(1, 100)]
    private float upperLookLimit = 80f;
    [SerializeField, Range(1, 100)]
    private float lowerLookLimit = 80f;
    #endregion

    #region JumpParameters
    //Parameters for jump height and gravity
    [Header("Jumping Parameters")]
    [SerializeField]
    private float jumpForce = 8f;
    [SerializeField]
    private float gravity = 30f;
    #endregion

    #region CrouchParameters
    //Parameters for crouching. The height and center will directly affect the CharacterController height and center.
    [Header("Crouch Parameters")]
    [SerializeField]
    private float crouchingHeight = 0.5f;
    [SerializeField]
    private float standingHeight = 2f;
    [SerializeField]
    private float timeToCrouch = 0.25f; //How long should the crouching animation take?
    [SerializeField]
    private Vector3 crouchingCenter = new Vector3(0, 0.5f, 0);
    [SerializeField]
    private Vector3 standingCenter = new Vector3(0, 0, 0); //Didn't use Vector3.Zero so that it would be customizable in inspector
    private bool playerIsCrouching; //Is the player currently crouched?
    private bool playerInCrouchAnimation; //Is the player currently in the middle of the crouching animation?
    #endregion

    #region HeadbobParameters
    [Header("Headbob Parameters")]
    [SerializeField]
    private float walkBobSpeed = 14f;
    [SerializeField]
    private float walkBobAmount = 0.05f;
    [SerializeField]
    private float sprintBobSpeed = 18f;
    [SerializeField]
    private float sprintBobAmount = 0.1f;
    [SerializeField]
    private float crouchBobSpeed = 8f;
    [SerializeField]
    private float crouchBobAmount = 0.025f;
    private float defaultYPosCamera = 0;
    private float timer;
    #endregion

    #region Camera
    private Camera playerCamera;
    private CharacterController characterController;
    #endregion

    #region MovementInput
    private Vector3 moveDirection;
    private Vector2 currentInput; //Whether player is moving vertically or horizontally along x and z planes
    
    private float rotationX = 0f; //Camera rotation for clamping
    #endregion


    void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>();
        characterController = GetComponent<CharacterController>();

        defaultYPosCamera = playerCamera.transform.localPosition.y;

        //Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        //Sets the stamina up
        currentStamina = maxStamina;

        playerCamera.fieldOfView = fovDefault;
    }

    // Update is called once per frame
    void Update()
    {
        if (!playerOnSpecialMovement)
        {
            if (PlayerCanMove)
            {
                HandleMovementInput();
                HandleMouseLook();

                if (playerCanJump)
                {
                    HandleJump();
                }

                if (playerCanCrouch)
                {
                    HandleCrouch();
                }

                if (playerCanHeadbob)
                {
                    HandleHeadbob();
                }

                //Apply all the movement parameters that are found earlier in the frame (above in Update())
                ApplyFinalMovements();
            }
        }
        else if (playerOnSpecialMovement)
        {
            HandleMouseLook();
        }

        if (PlayerIsSprinting)
        {
            currentStamina -= staminaDepletionRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }else if (!PlayerIsSprinting)
        {
            RegenerateStamina();
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }
    }

    //COME BACK TO THIS LATER
    //Stamina Regeneration delay so the player cannot infinitely run. 
    public void RegenerateStamina()
    {
        if (PlayerIsSprinting)
        {
            return;
        }

        staminaRegenTimer += Time.deltaTime;

        if (staminaRegenTimer >= staminaRegenDelay)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
        }

    }

    private void HandleMovementInput()
    {
        if (PlayerIsSprinting && playerCamera.fieldOfView < fovSprint && currentStamina > 0f)
        {
            playerCamera.fieldOfView += fovIncrement * Time.deltaTime;
        }
        else if (!PlayerIsSprinting && playerCamera.fieldOfView > fovDefault)
        {
            playerCamera.fieldOfView -= fovIncrement * Time.deltaTime;
        }
        //else
        //{
        //    playerCamera.fieldOfView = fovDefault;
        //}

        //when the player presses W and S or A and D
        //Check to see what speed they are going, based on whether they are crouching, sprinting, or simply walking
        currentInput = new Vector2((playerIsCrouching ? crouchSpeed : PlayerIsSprinting ? sprintSpeed : walkSpeed) * Input.GetAxis("Vertical"),
            (playerIsCrouching ? crouchSpeed : PlayerIsSprinting ? sprintSpeed : walkSpeed) * Input.GetAxis("Horizontal"));

        //The direction in which the player moves based on input
        float moveDirectionY = moveDirection.y;
        moveDirection = (transform.TransformDirection(Vector3.forward) * currentInput.x) + (transform.TransformDirection(Vector3.right) * currentInput.y);
        moveDirection.y = moveDirectionY;

    }

    private void HandleMouseLook()
    {
        //rotate camera around X and Y axis, and rotate player around x axis
        rotationX -= Input.GetAxis("Mouse Y") * lookSpeedY;
        rotationX = Mathf.Clamp(rotationX, -upperLookLimit, lowerLookLimit); //clamp camera
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeedX, 0);
    }

    private void HandleJump()
    {
        //only jump if property conditions are met
        if (PlayerShouldJump)
        {
            moveDirection.y = jumpForce;
        }
    }

    private void HandleCrouch()
    {
        //only crouch if property conditions are met
        if (PlayerShouldCrouch)
        {
            StartCoroutine(CrouchStand());
        }
    }

    private void HandleHeadbob()
    {
        if (!characterController.isGrounded)
        {
            return;
        }

        if (Mathf.Abs(moveDirection.x) > 0.1f || Mathf.Abs(moveDirection.z) > 0.1f)
        {
            timer += Time.deltaTime * (playerIsCrouching ? crouchBobSpeed : PlayerIsSprinting ? sprintBobSpeed : walkBobSpeed);
            playerCamera.transform.localPosition = new Vector3(
                playerCamera.transform.localPosition.x,
                defaultYPosCamera + Mathf.Sin(timer) * (playerIsCrouching ? crouchBobAmount : PlayerIsSprinting ? sprintBobAmount : walkBobAmount),
                playerCamera.transform.localPosition.z);
        }
    }

    private void ApplyFinalMovements()
    {
        //make sure the player is on the ground if applying gravity (after pressing Jump)
        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        //move the player based on the parameters gathered in the "Handle-" functions
        characterController.Move(moveDirection * Time.deltaTime);
    }

    //Coroutine that handles crouching/standing
    private IEnumerator CrouchStand()
    {
        //make sure there is nothing above player's head that should prevent them from standing, if there is, do not allow them to stand
        if (playerIsCrouching && Physics.Raycast(playerCamera.transform.position, Vector3.up, 1f))
        {
            yield break;
        }

        //player is now in crouching animation
        playerInCrouchAnimation = true;

        float timeElapsed = 0; //amount of time elapsed during animation
        float targetHeight = playerIsCrouching ? standingHeight : crouchingHeight; //target height based on the state the player is in when they press crouch button
        float currentHeight = characterController.height; //the player's height when they press the crouch button
        Vector3 targetCenter = playerIsCrouching ? standingCenter : crouchingCenter; //target center based on the state the player is in when they press crouch button
        Vector3 currentCenter = characterController.center; //the player's center when they press the crouch button

        //while the animation is still going
        while (timeElapsed < timeToCrouch)
        {
            characterController.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / timeToCrouch); //change the current height to the target height
            characterController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / timeToCrouch); //change the current center to the target center

            timeElapsed += Time.deltaTime; //increment the time elapsed based on the time it took between frames

            yield return null;
        }

        //Sanity check :P
        characterController.height = targetHeight;
        characterController.center = targetCenter;

        playerIsCrouching = !playerIsCrouching; //update whether or not the player is crouching

        playerInCrouchAnimation = false; //the crouching animation has ended
    }


    /*
    #region OldController
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private float crouchHeight = 0.5f;
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenRate = 10f;
    [SerializeField] private float staminaDepletionRate = 20f;

    private float verticalRotation = 0f;
    private bool isCrouching = false;
    private bool isSprinting = false;
    private float currentStamina;
    private Rigidbody rb;
    private float originalHeight;
    private Vector3 originalCenter;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        rb = GetComponent<Rigidbody>();
        originalHeight = transform.localScale.y;
        originalCenter = rb.centerOfMass;
        currentStamina = maxStamina;
    }

    void Update()
    {
        // Mouse movement
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);

        transform.Rotate(Vector3.up * mouseX);
        Camera.main.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);

        // Player movement
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        bool crouch = Input.GetKey(KeyCode.LeftControl);

        // Calculate movement speed
        float speed = movementSpeed;
        if (sprint && !isCrouching && currentStamina > 0f)
        {
            speed = sprintSpeed;
            isSprinting = true;
            currentStamina -= staminaDepletionRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }
        else
        {
            isSprinting = false;
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }

        // Calculate movement direction
        Vector3 moveDirection = transform.right * moveX + transform.forward * moveZ;
        moveDirection.Normalize();
        moveDirection *= speed;

        // Apply movement
        rb.velocity = new Vector3(moveDirection.x, rb.velocity.y, moveDirection.z);

        // Crouching
        if (crouch)
        {
            if (!isCrouching)
            {
                isCrouching = true;
                transform.localScale = new Vector3(transform.localScale.x, crouchHeight, transform.localScale.z);
                rb.centerOfMass = new Vector3(originalCenter.x, crouchHeight / 2f, originalCenter.z);
            }
        }
        else
        {
            if (isCrouching)
            {
                // Check for obstacles before standing up
                if (!Physics.Raycast(transform.position, Vector3.up, standingHeight))
                {
                    isCrouching = false;
                    transform.localScale = new Vector3(transform.localScale.x, originalHeight, transform.localScale.z);
                    rb.centerOfMass = originalCenter;
                }
            }
        }
    }
    #endregion
    */
}
