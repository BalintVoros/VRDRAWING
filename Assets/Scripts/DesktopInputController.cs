using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DesktopInputController : MonoBehaviour
{
    //ROLE: Handles desktop input for player movement and camera control.
    //      This allows the VR drawing application to work with mouse and keyboard on desktop.
    //      Manages camera rotation with mouse, WASD movement, and provides drawing raycast position.

    public static DesktopInputController Instance { get; private set; }

    [Header("Camera Settings")]
    [SerializeField] private Transform vrCameraTransform;
    [SerializeField] private Transform yawTransform;
    [SerializeField] private float horizontalSensitivity = 0.25f;
    [SerializeField] private float verticalSensitivity = 0.18f;
    [SerializeField] private float maxLookAngle = 90f;

    [Header("Movement Settings")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundDrag = 0.1f;

    [Header("Drawing Settings")]
    [SerializeField] private float drawingRaycastDistance = 1000f;
    [Header("Debug")]
    [SerializeField] private bool enableMouseDebug = true;

    [Header("Desktop Shortcuts")]
    [SerializeField] private UnityEngine.InputSystem.Key leftMenuKey = UnityEngine.InputSystem.Key.F1;
    [SerializeField] private UnityEngine.InputSystem.Key rightMenuKey = UnityEngine.InputSystem.Key.F2;
    [SerializeField] private UnityEngine.InputSystem.Key leftMenuAltKey = UnityEngine.InputSystem.Key.M;
    [SerializeField] private UnityEngine.InputSystem.Key rightMenuAltKey = UnityEngine.InputSystem.Key.N;
    [SerializeField] private UnityEngine.InputSystem.Key leftMenuFallbackKey = UnityEngine.InputSystem.Key.G;
    [SerializeField] private UnityEngine.InputSystem.Key rightMenuFallbackKey = UnityEngine.InputSystem.Key.H;
    [SerializeField] private bool enableMiddleMouseToggle = true;

    private float currentVerticalRotation = 0f;
    private Vector3 currentVelocity = Vector3.zero;
    private bool isGrounded = true;
    private bool isInitialized = false;
    private Vector3 horizontalVelocitySmooth = Vector3.zero;
    private Vector3 horizontalVelocitySmoothVel = Vector3.zero;
    private InputAction lookAction;
    private InputAction leftMenuAction;
    private InputAction rightMenuAction;

    public event System.Action LeftMenuPressed;
    public event System.Action RightMenuPressed;
    private bool crosshairEnsured = false;
    private const string DesktopCanvasName = "DesktopUICanvas";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        // Dedicated mouse-look action is more reliable than direct polling in mixed UI/XR setups.
        lookAction = new InputAction("DesktopLook", InputActionType.Value, "<Mouse>/delta");
        lookAction.Enable();

        // Create menu toggle actions (keyboard bindings) using configured keys.
        leftMenuAction = new InputAction("LeftMenu", InputActionType.Button);
        rightMenuAction = new InputAction("RightMenu", InputActionType.Button);

        // Add primary bindings based on enum names (e.g., F1 -> f1, A -> a)
        leftMenuAction.AddBinding($"<Keyboard>/{leftMenuKey.ToString().ToLower()}");
        rightMenuAction.AddBinding($"<Keyboard>/{rightMenuKey.ToString().ToLower()}");
        // Add fallback letter bindings in case function keys are intercepted by OS/editor
        leftMenuAction.AddBinding($"<Keyboard>/{leftMenuAltKey.ToString().ToLower()}");
        rightMenuAction.AddBinding($"<Keyboard>/{rightMenuAltKey.ToString().ToLower()}");
        // Add additional fallback letter keys
        leftMenuAction.AddBinding($"<Keyboard>/{leftMenuFallbackKey.ToString().ToLower()}");
        rightMenuAction.AddBinding($"<Keyboard>/{rightMenuFallbackKey.ToString().ToLower()}");
        // Optionally add middle mouse button bindings
        if (enableMiddleMouseToggle)
        {
            leftMenuAction.AddBinding("<Mouse>/middleButton");
            rightMenuAction.AddBinding("<Mouse>/middleButton");
        }

        leftMenuAction.performed += ctx => {
            Debug.Log($"DesktopInputController: LeftMenuAction performed via {ctx.control}");
            if (Crosshair.Instance != null) Crosshair.Instance.ShowToast($"Left menu key: {ctx.control}");
            LeftMenuPressed?.Invoke();
        };
        rightMenuAction.performed += ctx => {
            Debug.Log($"DesktopInputController: RightMenuAction performed via {ctx.control}");
            if (Crosshair.Instance != null) Crosshair.Instance.ShowToast($"Right menu key: {ctx.control}");
            RightMenuPressed?.Invoke();
        };

        leftMenuAction.Enable();
        rightMenuAction.Enable();
    }

    private void Start()
    {
        // Initialization is handled by the Player when camera and CharacterController
        // references are available. Avoid auto-initializing here to prevent duplicate initialization warnings.
        if (enableMouseDebug)
        {
            Debug.Log($"[DesktopInputController] Start: Mouse present={(UnityEngine.InputSystem.Mouse.current != null)}; CameraAssigned={(vrCameraTransform != null)}; CharacterControllerAssigned={(characterController != null)}");
        }
    }

    /// <summary>
    /// Initialize the desktop controller with optional references
    /// </summary>
    public void InitializeDesktopController()
    {
        if (isInitialized)
        {
            Debug.LogWarning("[DesktopInputController] Already initialized!");
            return;
        }

        if (vrCameraTransform == null)
        {
            // Try to find the camera if not assigned
            vrCameraTransform = Camera.main?.transform;
            if (vrCameraTransform == null)
            {
                Debug.LogError("[DesktopInputController] VR Camera Transform not assigned and Camera.main not found!");
                enabled = false;
                return;
            }
        }

        if (characterController == null)
        {
            // Try to find it on parent or this object
            characterController = GetComponentInParent<CharacterController>();
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (characterController == null)
            {
                Debug.LogError("[DesktopInputController] CharacterController not found on Player or parents!");
                enabled = false;
                return;
            }
        }

        if (yawTransform == null)
        {
            // Rotate the player root (CharacterController owner) for horizontal look.
            yawTransform = characterController.transform;
        }

        // Desktop mode keeps cursor visible/unlocked for UI interaction.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isInitialized = true;
        EnsureCrosshairExists();
        // Auto-create quick controls panel for desktop users
        if (FindObjectOfType<DesktopQuickControls>() == null)
        {
            GameObject qc = new GameObject("DesktopQuickControls");
            qc.AddComponent<DesktopQuickControls>();
            DontDestroyOnLoad(qc);
            Debug.Log("DesktopInputController: Created DesktopQuickControls panel.");
        }
        // Auto-create test buttons to help debug input wiring (only if not already present)
        if (FindObjectOfType<DesktopInputTestButtons>() == null)
        {
            GameObject testButtons = new GameObject("DesktopInputTestButtons");
            testButtons.AddComponent<DesktopInputTestButtons>();
            DontDestroyOnLoad(testButtons);
            Debug.Log("DesktopInputController: Created DesktopInputTestButtons for debugging.");
        }
        Debug.Log("[DesktopInputController] Initialization complete. Ready for desktop input.");
    }

    /// <summary>
    /// Public method to assign camera transform
    /// </summary>
    public void SetCameraTransform(Transform camera)
    {
        if (camera != null)
        {
            vrCameraTransform = camera;
            Debug.Log("[DesktopInputController] Camera transform assigned.");
        }
    }

    /// <summary>
    /// Public method to assign character controller
    /// </summary>
    public void SetCharacterController(CharacterController controller)
    {
        if (controller != null)
        {
            characterController = controller;
            Debug.Log("[DesktopInputController] CharacterController assigned.");
        }
    }

    private void Update()
    {
        if (Crosshair.Instance == null)
        {
            crosshairEnsured = false;
        }

        // Always handle mouse look (no cursor lock required)
        HandleMouseLook();
        HandleMovement();

        if (!crosshairEnsured)
        {
            EnsureCrosshairExists();
        }

        // Update crosshair visibility if present
        if (Crosshair.Instance != null)
        {
            Crosshair.Instance.SetCrosshairVisibility(Crosshair.Instance.ShouldCrosshairBeVisible());
        }
    }

    private void HandleMouseLook()
    {
        if (vrCameraTransform == null)
        {
            if (enableMouseDebug)
                Debug.LogWarning("[DesktopInputController] vrCameraTransform is null. Can't apply mouse look.");
            return;
        }

        Vector2 mouseDelta = Vector2.zero;
        if (lookAction != null)
        {
            mouseDelta = lookAction.ReadValue<Vector2>();
        }

        // Fallback if action did not return movement on this frame.
        if (mouseDelta == Vector2.zero && UnityEngine.InputSystem.Mouse.current != null)
        {
            mouseDelta = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
        }

        float mouseX = mouseDelta.x * horizontalSensitivity;
        float mouseY = mouseDelta.y * verticalSensitivity;

        // Rotate body left/right
        yawTransform.Rotate(Vector3.up * mouseX, Space.World);

        // Rotate camera up/down
        currentVerticalRotation -= mouseY;
        currentVerticalRotation = Mathf.Clamp(currentVerticalRotation, -maxLookAngle, maxLookAngle);
        vrCameraTransform.localRotation = Quaternion.Euler(currentVerticalRotation, 0f, 0f);

        if (enableMouseDebug && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[DesktopInputController] MouseDelta={mouseDelta}, Camera={vrCameraTransform.name}");
        }
    }

    private void HandleMovement()
    {
        // Get input from Keyboard (manual key checking for Input System)
        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.dKey.isPressed) horizontal += 1f;
        if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.wKey.isPressed) vertical += 1f;
        if (Keyboard.current.sKey.isPressed) vertical -= 1f;

        // Determine speed (check if Shift is pressed)
        float currentSpeed = Keyboard.current.leftShiftKey.isPressed ? sprintSpeed : moveSpeed;

        // Calculate movement direction relative to camera (ignore vertical component)
        Vector3 camForward = vrCameraTransform.forward;
        camForward.y = 0f;
        camForward.Normalize();
        Vector3 camRight = vrCameraTransform.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 moveDirection = (camForward * vertical + camRight * horizontal);
        if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

        // Target horizontal velocity
        Vector3 targetHorizontal = moveDirection * currentSpeed;

        // Smooth horizontal velocity to avoid sliding
        horizontalVelocitySmooth = Vector3.SmoothDamp(horizontalVelocitySmooth, targetHorizontal, ref horizontalVelocitySmoothVel, 0.12f);
        
        // Apply gravity
        if (isGrounded && currentVelocity.y < 0)
        {
            currentVelocity.y = -2f; // Small negative value to keep grounded
        }
        
        currentVelocity.y += gravity * Time.deltaTime;
        
        // Apply ground drag to horizontal velocity when grounded
        if (isGrounded && groundDrag > 0f)
        {
            horizontalVelocitySmooth *= Mathf.Clamp01(1f - groundDrag * Time.deltaTime * 10f);
        }

        // Combine velocities
        Vector3 finalVelocity = horizontalVelocitySmooth + Vector3.up * currentVelocity.y;

        // Apply to character controller
        characterController.Move(finalVelocity * Time.deltaTime);

        // Check if grounded
        isGrounded = characterController.isGrounded;
    }

    // Cursor lock/unlock and debug helpers removed — desktop mode uses visible cursor and mouse look without locking.

    /// <summary>
    /// Gets the drawing position for desktop input (raycast from camera center)
    /// </summary>
    public Vector3 GetDrawingPosition()
    {
        if (vrCameraTransform == null)
            return Vector3.zero;

        // Raycast from camera center forward
        if (Physics.Raycast(vrCameraTransform.position, vrCameraTransform.forward, out RaycastHit hit, drawingRaycastDistance))
        {
            return hit.point;
        }
        else
        {
            // If no hit, return point along the forward ray
            return vrCameraTransform.position + vrCameraTransform.forward * 2f;
        }
    }

    /// <summary>
    /// Gets the drawing direction for desktop input
    /// </summary>
    public Vector3 GetDrawingDirection()
    {
        return vrCameraTransform.forward;
    }

    /// <summary>
    /// Gets whether the drawing button is pressed (left mouse button)
    /// </summary>
    public bool IsDrawingPressed()
    {
        return Mouse.current.leftButton.isPressed;
    }

    /// <summary>
    /// Gets whether drawing was just started
    /// </summary>
    public bool IsDrawingPressedDown()
    {
        return Mouse.current.leftButton.wasPressedThisFrame;
    }

    /// <summary>
    /// Gets whether drawing was just released
    /// </summary>
    public bool IsDrawingPressedUp()
    {
        return Mouse.current.leftButton.wasReleasedThisFrame;
    }

    public Transform GetCameraTransform() => vrCameraTransform;
    public CharacterController GetCharacterController() => characterController;

    // Expose configured shortcut keys
    public UnityEngine.InputSystem.Key LeftMenuKey => leftMenuKey;
    public UnityEngine.InputSystem.Key RightMenuKey => rightMenuKey;

    public string GetMenuKeyDisplayName(bool left)
    {
        var key = left ? leftMenuKey : rightMenuKey;
        return key.ToString();
    }

    // Allow other classes to trigger the menu events via a public method
    public void TriggerLeftMenu()
    {
        LeftMenuPressed?.Invoke();
    }

    public void TriggerRightMenu()
    {
        RightMenuPressed?.Invoke();
    }

    private void EnsureCrosshairExists()
    {
        if (Crosshair.Instance != null)
        {
            crosshairEnsured = true;
            return;
        }

        Canvas targetCanvas = null;
        GameObject existingCanvas = GameObject.Find(DesktopCanvasName);
        if (existingCanvas != null)
        {
            targetCanvas = existingCanvas.GetComponent<Canvas>();
        }

        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject(DesktopCanvasName);
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.overrideSorting = true;
            targetCanvas.sortingOrder = 32767;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);
        }

        GameObject crosshairObject = new GameObject("Crosshair");
        crosshairObject.transform.SetParent(targetCanvas.transform, false);
        crosshairObject.transform.SetAsLastSibling();

        RectTransform rectTransform = crosshairObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(24f, 24f);

        Image image = crosshairObject.AddComponent<Image>();
        image.sprite = CreateCrosshairSprite(24);
        image.color = Color.white;
        image.raycastTarget = false;

        CanvasGroup group = crosshairObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        crosshairObject.AddComponent<Crosshair>();
        crosshairEnsured = true;
        Debug.Log("[DesktopInputController] Crosshair created automatically.");
    }

    private Sprite CreateCrosshairSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.filterMode = FilterMode.Point;
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        int center = size / 2;
        int thickness = 1;
        int length = size / 3;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool horizontal = Mathf.Abs(y - center) <= thickness && Mathf.Abs(x - center) <= length;
                bool vertical = Mathf.Abs(x - center) <= thickness && Mathf.Abs(y - center) <= length;
                if (horizontal || vertical)
                {
                    pixels[y * size + x] = Color.white;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void OnDestroy()
    {
        if (lookAction != null)
        {
            lookAction.Disable();
            lookAction.Dispose();
            lookAction = null;
        }
        if (leftMenuAction != null)
        {
            leftMenuAction.Disable();
            leftMenuAction.Dispose();
            leftMenuAction = null;
        }
        if (rightMenuAction != null)
        {
            rightMenuAction.Disable();
            rightMenuAction.Dispose();
            rightMenuAction = null;
        }
    }

    public UnityEngine.InputSystem.Key LeftMenuAltKey => leftMenuAltKey;
    public UnityEngine.InputSystem.Key RightMenuAltKey => rightMenuAltKey;

    public string GetMenuAltKeyDisplayName(bool left)
    {
        var key = left ? leftMenuAltKey : rightMenuAltKey;
        return key.ToString();
    }
}
