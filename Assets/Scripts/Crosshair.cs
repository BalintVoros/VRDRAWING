using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using UnityEngine.InputSystem;

public class Crosshair : MonoBehaviour
{
    //ROLE: Manages the crosshair UI for desktop mode. Displays a crosshair at the center of the screen
    //      that changes appearance based on context (hovering over drawings, menu interaction, etc.)

    public static Crosshair Instance { get; private set; }

    [Header("Crosshair Settings")]
    [SerializeField] private Image crosshairImage;
    [SerializeField] private Image centerDot;
    
    [Header("Crosshair Appearance")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(0f, 1f, 1f, 1f); // Cyan
    [SerializeField] private Color drawingColor = new Color(1f, 0.5f, 0f, 1f); // Orange
    [SerializeField] private float normalSize = 30f;
    [SerializeField] private float hoverSize = 40f;
    [SerializeField] private float drawingSize = 35f;

    [Header("Detection")]
    [SerializeField] private float raycastDistance = 1000f;

    [Header("Debug")]
    [SerializeField] private bool showDebugKeys = true;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 targetScale = Vector3.one;
    [SerializeField] private float scaleSmoothing = 0.1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Get components
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        // Crosshair should never block UI raycasts behind it.
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("[Crosshair] RectTransform component not found!");
            enabled = false;
            return;
        }

        if (crosshairImage == null)
        {
            crosshairImage = GetComponent<Image>();
        }

        // Add a small HUD text under the crosshair to show current mode/color for desktop users
        Transform textTf = transform.Find("CrosshairHUDText");
        if (textTf == null)
        {
            GameObject textObj = new GameObject("CrosshairHUDText");
            textObj.transform.SetParent(transform, false);
            RectTransform txtRt = textObj.AddComponent<RectTransform>();
            txtRt.anchoredPosition = new Vector2(0, -28);
            txtRt.sizeDelta = new Vector2(200, 40);
            Text txt = textObj.AddComponent<Text>();
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 14;
            txt.color = Color.white;
            txt.raycastTarget = false;
        }
            if (crosshairImage == null)
            {
                Debug.LogWarning("[Crosshair] Crosshair Image not assigned. Creating a simple circle.");
                crosshairImage = gameObject.AddComponent<Image>();
            }

            // Position at center of screen
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(normalSize, normalSize);
        
        if (crosshairImage != null)
        {
            crosshairImage.color = normalColor;
            crosshairImage.raycastTarget = false;
        }
    }

    private void Update()
    {
        // Auto-show/hide crosshair based on cursor state and game UI
        bool shouldShow = ShouldCrosshairBeVisible();
        SetCrosshairVisibility(shouldShow);

        UpdateCrosshairAppearance();
        SmoothScale();

        UpdateHUD();
    }

    private void UpdateHUD()
    {
        Transform tf = transform.Find("CrosshairHUDText");
        if (tf == null) return;
        Text txt = tf.GetComponent<Text>();
        if (txt == null) return;

        Brush b = FindObjectOfType<Brush>();
        if (b == null)
        {
            txt.text = "Mode: - | Color: -";
            return;
        }

        string mode = b.drawingMode.ToString().ToUpper();
        Color c = b.brushStartColor;
        string colorText = string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", (int)(c.r * 255), (int)(c.g * 255), (int)(c.b * 255));
        // Display names for HUD
        string leftKeyName = "F1";
        string rightKeyName = "F2";
        // Key enums for input checks
        UnityEngine.InputSystem.Key leftKeyEnum = UnityEngine.InputSystem.Key.F1;
        UnityEngine.InputSystem.Key rightKeyEnum = UnityEngine.InputSystem.Key.F2;

        if (DesktopInputController.Instance != null)
        {
            leftKeyName = DesktopInputController.Instance.GetMenuKeyDisplayName(true);
            rightKeyName = DesktopInputController.Instance.GetMenuKeyDisplayName(false);
            leftKeyEnum = DesktopInputController.Instance.LeftMenuKey;
            rightKeyEnum = DesktopInputController.Instance.RightMenuKey;
        }

        string debugLine = string.Empty;
        if (showDebugKeys)
        {
            bool keyboardPresent = Keyboard.current != null;
            string keyboardText = keyboardPresent ? "Keyboard: Present" : "Keyboard: MISSING";

            string leftState = "-";
            string rightState = "-";
            if (keyboardPresent)
            {
                var lKeyControl = Keyboard.current[leftKeyEnum];
                var rKeyControl = Keyboard.current[rightKeyEnum];
                if (lKeyControl != null && lKeyControl.IsPressed()) leftState = "PRESSED";
                if (rKeyControl != null && rKeyControl.IsPressed()) rightState = "PRESSED";
            }

            string leftAlt = string.Empty;
            string rightAlt = string.Empty;
            if (DesktopInputController.Instance != null)
            {
                leftAlt = DesktopInputController.Instance.GetMenuAltKeyDisplayName(true);
                rightAlt = DesktopInputController.Instance.GetMenuAltKeyDisplayName(false);
            }

            string altInfo = string.Empty;
            if (!string.IsNullOrEmpty(leftAlt) || !string.IsNullOrEmpty(rightAlt))
            {
                altInfo = $" (also {leftAlt}/{rightAlt})";
            }

            debugLine = $"\n{keyboardText} | Left({leftKeyName}): {leftState} | Right({rightKeyName}): {rightState}{altInfo}";
        }

        txt.text = $"Mode: {mode} | Color: {colorText}\n{leftKeyName}: Left Menu    {rightKeyName}: Right Menu{debugLine}";
    }

    // Simple toast message display under the HUD
    private Coroutine toastCoroutine;
    public void ShowToast(string message, float duration = 1.5f)
    {
        Transform tf = transform.Find("CrosshairHUDText");
        if (tf == null) return;
        Text txt = tf.GetComponent<Text>();
        if (txt == null) return;

        if (toastCoroutine != null)
        {
            StopCoroutine(toastCoroutine);
            toastCoroutine = null;
        }
        string prev = txt.text;
        txt.text = prev + "\n" + message;
        toastCoroutine = StartCoroutine(ClearToastAfter(txt, prev, duration));
    }

    private System.Collections.IEnumerator ClearToastAfter(Text txt, string previous, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (txt != null)
        {
            txt.text = previous;
        }
        toastCoroutine = null;
    }

    private void UpdateCrosshairAppearance()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        bool isHoveringObject = Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out RaycastHit hit, raycastDistance);
        
        Color targetColor = normalColor;
        float targetSize = normalSize;

        if (isHoveringObject)
        {
            // Check if hovering over a line or drawing object
            if (hit.collider.CompareTag("Line") || hit.collider.CompareTag("Drawing"))
            {
                targetColor = hoverColor;
                targetSize = hoverSize;
            }
        }

        // Check if currently drawing
        if (DesktopInputController.Instance != null && DesktopInputController.Instance.IsDrawingPressed())
        {
            targetColor = drawingColor;
            targetSize = drawingSize;
        }

        // Smoothly update crosshair appearance
        if (crosshairImage != null)
        {
            crosshairImage.color = Color.Lerp(crosshairImage.color, targetColor, Time.deltaTime * 5f);
        }

        targetScale = new Vector3(targetSize / normalSize, targetSize / normalSize, 1f);
    }

    private void SmoothScale()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, scaleSmoothing);
        }
    }

    /// <summary>
    /// Show or hide the crosshair
    /// </summary>
    public void SetCrosshairVisibility(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }
    }

    /// <summary>
    /// Check if crosshair should be visible (e.g., not during menu interaction)
    /// </summary>
    public bool ShouldCrosshairBeVisible()
    {
        // For desktop mode we always want the crosshair visible by default.
        // UI can hide it explicitly by calling `SetCrosshairVisibility(false)` when needed.
        return true;
    }
}
