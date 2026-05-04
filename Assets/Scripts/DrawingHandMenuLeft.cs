using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DrawingHandMenuLeft : MonoBehaviour
{
    [Header("Hand Menu UI Objects")]
    [SerializeField] private GameObject             handMenuObjectColors;
    [SerializeField] private GameObject             handMenuObjectColorPalette;
    [SerializeField] private GameObject             handMenuObjectAdvancedOptions;
    [SerializeField] private GameObject             handMenuObjectGradient;
    [SerializeField] private GameObject             handMenuObjectGradientColorPalette;

    [Header("Hand Menu Color Options")]
    [SerializeField] private LeftStartColorPicker   colorPickerStart; 
    [SerializeField] private Slider                 opacityStartSlider;
    [SerializeField] private TMP_InputField         R_StartValueInput;
    [SerializeField] private TMP_InputField         G_StartValueInput;
    [SerializeField] private TMP_InputField         B_StartValueInput;
    [SerializeField] private TMP_InputField         A_StartValueInput;

    [Header("Hand Menu Advanced Options")]
    [SerializeField] private Slider                 lineStartWidthSlider;
    [SerializeField] private TMP_Text               lineStartWidthValueText;
    [SerializeField] private Slider                 lineEndWidthSlider;
    [SerializeField] private TMP_Text               lineEndWidthValueText;
    [SerializeField] private Button                 equalLineWidthButton;
    [SerializeField] private TMP_Dropdown           drawingModeDropdown;
    [SerializeField] private Toggle                 gradientModeToggle;

    [Header("Hand Menu Gradient Options")]
    [SerializeField] private LeftEndColorPicker     colorPickerEnd;
    [SerializeField] private Slider                 opacityEndSlider;
    [SerializeField] private TMP_InputField         R_EndValueInput;
    [SerializeField] private TMP_InputField         G_EndValueInput;
    [SerializeField] private TMP_InputField         B_EndValueInput;
    [SerializeField] private TMP_InputField         A_EndValueInput;

    [Header("Switch to palette mode buttons")]
    [SerializeField] private Button                 switchToDiscretePaletteModeButton;
    [SerializeField] private Button                 switchToContinuousColorModeButton;
    [SerializeField] private Button                 switchToGradientDiscretePaletteModeButton;
    [SerializeField] private Button                 switchToGradientContinuousColorModeButton;

    [Header("User Display")]
    [SerializeField] private TextMeshProUGUI        loggedInUserTextHandMenu;


    [Header("VR Controller Interactions")]
    [SerializeField] private InputActionReference   enableHandMenuAction;

    // Desktop shortcut is read from `DesktopInputController` when available.

    [SerializeField] private GameObject             brushObject;
    public bool                                     handMenuEnableState = false;
    private bool                                    discretePaletteMode = true;
    private bool                                    gradientPaletteMode = true;
    private bool                                    enableGradientMode = false;
    public Brush.DrawingMode                        drawingMode = Brush.DrawingMode.None;
    private bool subscribedToController = false;


    void Start()
    {
        // Try to subscribe to centralized menu event if available now; if not, Update() will catch it later
        SubscribeToController();

        if (DesktopInputController.Instance != null)
        {
            Debug.Log($"DrawingHandMenuLeft: Configured left menu key = {DesktopInputController.Instance.GetMenuKeyDisplayName(true)}");
        }
        else
        {
            Debug.Log("DrawingHandMenuLeft: DesktopInputController.Instance is null at Start");
        }
        // Initialize the center UI elements
        {
            if (gradientModeToggle != null)
            {
                enableGradientMode = gradientModeToggle.isOn;
                gradientModeToggle.onValueChanged.AddListener(enable =>
                {
                    enableGradientMode = enable;
                    if (handMenuObjectGradient != null) SetPanelActiveStates();
                });
            }
            else { Debug.LogWarning("Gradient Mode Toggle is not assigned."); }
     
            SetPanelActiveStates();

            if (lineStartWidthSlider != null && lineStartWidthValueText != null)
            {
                lineStartWidthValueText.text = lineStartWidthSlider.value.ToString("F4"); 
                lineStartWidthSlider.onValueChanged.AddListener(sliderValue =>
                {
                    lineStartWidthValueText.text = sliderValue.ToString("F4");
                    if (brushObject != null && brushObject.GetComponent<Brush>() != null)
                        brushObject.GetComponent<Brush>().brushStartWidth = sliderValue;
                });
            } else { Debug.LogWarning("Line Start Width Slider or Text is not assigned.");}

            if (lineEndWidthSlider != null && lineEndWidthValueText != null)
            {
                lineEndWidthValueText.text = lineEndWidthSlider.value.ToString("F4");
                lineEndWidthSlider.onValueChanged.AddListener(sliderValue =>
                {
                    lineEndWidthValueText.text = sliderValue.ToString("F4");
                    if (brushObject != null && brushObject.GetComponent<Brush>() != null)
                        brushObject.GetComponent<Brush>().brushEndWidth = sliderValue;
                });
            } else { Debug.LogWarning("Line End Width Slider or Text is not assigned.");}


            if (equalLineWidthButton != null)
            {
                equalLineWidthButton.onClick.AddListener(() =>
                {
                    if (lineStartWidthSlider != null && lineEndWidthSlider != null && brushObject != null && brushObject.GetComponent<Brush>() != null)
                    {
                        lineEndWidthSlider.value = lineStartWidthSlider.value;
                    
                        brushObject.GetComponent<Brush>().brushStartWidth = lineStartWidthSlider.value;
                        brushObject.GetComponent<Brush>().brushEndWidth = lineEndWidthSlider.value;
                        if(lineStartWidthValueText != null) lineStartWidthValueText.text = lineStartWidthSlider.value.ToString("F4");
                        if(lineEndWidthValueText != null) lineEndWidthValueText.text = lineEndWidthSlider.value.ToString("F4");
                    }
                });
            } else { Debug.LogWarning("Equal Line Width Button is not assigned.");}


            // Try to auto-assign brushObject if missing
            if (brushObject == null)
            {
                brushObject = FindObjectOfType<Brush>()?.gameObject;
                if (brushObject == null && Player.Instance != null)
                {
                    var left = Player.Instance.GetType().GetField("leftBrush", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(Player.Instance) as Brush;
                    var right = Player.Instance.GetType().GetField("rightBrush", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(Player.Instance) as Brush;
                    if (left != null) brushObject = left.gameObject;
                    else if (right != null) brushObject = right.gameObject;
                }
            }

            if (drawingModeDropdown != null && brushObject != null && brushObject.GetComponent<Brush>() != null)
            {
                drawingModeDropdown.ClearOptions();
                drawingModeDropdown.AddOptions(new System.Collections.Generic.List<string> { "None", "Draw", "Erase", "Grab" });
                drawingModeDropdown.value = (int)drawingMode;
                brushObject.GetComponent<Brush>().drawingMode = drawingMode;

                drawingModeDropdown.onValueChanged.AddListener(value =>
                {
                    this.drawingMode = (Brush.DrawingMode)value;
                    brushObject.GetComponent<Brush>().drawingMode = this.drawingMode;
                });
            }
            else { Debug.LogWarning("Drawing Mode Dropdown or BrushObject/Brush component is not assigned."); }
        }

        // Initialize UI elements on the left side
        {
            if (colorPickerStart != null) colorPickerStart.OnColorChanged += ColorPickerStartOnColorChanged;
            else Debug.LogWarning("Color Picker Start is not assigned.");

            if (opacityStartSlider != null)
            {
                opacityStartSlider.onValueChanged.AddListener(opacityValue =>
                {
                    if (A_StartValueInput != null) A_StartValueInput.text = opacityValue.ToString("F2"); 
                    if (brushObject != null && brushObject.GetComponent<Brush>() != null)
                    {
                        brushObject.GetComponent<Brush>().brushStartColor.a = opacityValue;
                        if (!enableGradientMode) brushObject.GetComponent<Brush>().brushEndColor.a = opacityValue;
                    } else { Debug.LogError("BrushObject or Brush component missing for opacityStartSlider!");}
                });
            } else { Debug.LogWarning("Opacity Start Slider is not assigned.");}
        }

        // Initialize UI elements on the right side
        {
            if (colorPickerEnd != null) colorPickerEnd.OnColorChanged += ColorPickerEndOnColorChanged;
            else Debug.LogWarning("Color Picker End is not assigned.");
        
            if (opacityEndSlider != null)
            {
                opacityEndSlider.onValueChanged.AddListener(opacityValue =>
                {
                    if (A_EndValueInput != null) A_EndValueInput.text = opacityValue.ToString("F2");
                    if (brushObject != null && brushObject.GetComponent<Brush>() != null)
                    {
                        brushObject.GetComponent<Brush>().brushEndColor.a = opacityValue;
                    } else { Debug.LogError("BrushObject or Brush component missing for opacityEndSlider!");}
                });
            } else { Debug.LogWarning("Opacity End Slider is not assigned.");}
        }

        // Initialize palette mode or continuous colors mode buttons
        {
            if (switchToDiscretePaletteModeButton != null)
            {
                switchToDiscretePaletteModeButton.onClick.AddListener(() =>
                {
                    discretePaletteMode = true;
                    SetPanelActiveStates();
                });
            }
            else { Debug.LogWarning("Switch to Discrete Palette Mode Button is not assigned."); }

            if (switchToContinuousColorModeButton != null)
            {
                switchToContinuousColorModeButton.onClick.AddListener(() =>
                {
                    discretePaletteMode = false;
                    SetPanelActiveStates();
                });
            }
            else { Debug.LogWarning("Switch to Continuous Color Mode Button is not assigned."); }

            if (switchToGradientDiscretePaletteModeButton != null)
            {
                switchToGradientDiscretePaletteModeButton.onClick.AddListener(() =>
                {
                    gradientPaletteMode = true;
                    SetPanelActiveStates();
                });
            }
            else { Debug.LogWarning("Switch to Gradient Discrete Palette Mode Button is not assigned."); }

            if (switchToGradientContinuousColorModeButton != null)
            {
                switchToGradientContinuousColorModeButton.onClick.AddListener(() =>
                {
                    gradientPaletteMode = false;
                    SetPanelActiveStates();
                });
            }
            else { Debug.LogWarning("Switch to Gradient Continuous Color Mode Button is not assigned."); }
        }
    }

    private void SetPanelActiveStates()
    {
        if (handMenuObjectColors != null) handMenuObjectColors.SetActive(handMenuEnableState && !discretePaletteMode);
        if (handMenuObjectColorPalette != null) handMenuObjectColorPalette.SetActive(handMenuEnableState && discretePaletteMode);
        if (handMenuObjectAdvancedOptions != null) handMenuObjectAdvancedOptions.SetActive(handMenuEnableState);
        if (handMenuObjectGradient != null) handMenuObjectGradient.SetActive(handMenuEnableState && enableGradientMode && !gradientPaletteMode);
        if (handMenuObjectGradientColorPalette != null) handMenuObjectGradientColorPalette.SetActive(handMenuEnableState && enableGradientMode && gradientPaletteMode);

        // Refresh color inputs
        R_StartValueInput.text = ((int)(brushObject.GetComponent<Brush>().brushStartColor.r * 255)).ToString();
        G_StartValueInput.text = ((int)(brushObject.GetComponent<Brush>().brushStartColor.g * 255)).ToString();
        B_StartValueInput.text = ((int)(brushObject.GetComponent<Brush>().brushStartColor.b * 255)).ToString();
        A_StartValueInput.text = brushObject.GetComponent<Brush>().brushStartColor.a.ToString("F2");
        R_EndValueInput.text = ((int)(brushObject.GetComponent<Brush>().brushEndColor.r * 255)).ToString();
        G_EndValueInput.text = ((int)(brushObject.GetComponent<Brush>().brushEndColor.g * 255)).ToString();
        B_EndValueInput.text = ((int)(brushObject.GetComponent<Brush>().brushEndColor.b * 255)).ToString();
        A_EndValueInput.text = brushObject.GetComponent<Brush>().brushEndColor.a.ToString("F2");
    }

    public bool IsGradientModeEnabled()
    {
        return enableGradientMode;
    }

    private void ColorPickerStartOnColorChanged(UnityEngine.Color color)
    {
        if (R_StartValueInput != null) R_StartValueInput.text = ((int)(color.r * 255)).ToString();
        if (G_StartValueInput != null) G_StartValueInput.text = ((int)(color.g * 255)).ToString();
        if (B_StartValueInput != null) B_StartValueInput.text = ((int)(color.b * 255)).ToString();
        if (opacityStartSlider != null) color.a = opacityStartSlider.value; 
        else color.a = 1f; 
        
        if (brushObject != null && brushObject.GetComponent<Brush>() != null)
        {
            Brush brush = brushObject.GetComponent<Brush>();
            brush.SetStartColor(color);
            if (!enableGradientMode) brush.SetEndColor(color);
        }
    }

    private void ColorPickerEndOnColorChanged(UnityEngine.Color color)
    {
        if (R_EndValueInput != null) R_EndValueInput.text = ((int)(color.r * 255)).ToString();
        if (G_EndValueInput != null) G_EndValueInput.text = ((int)(color.g * 255)).ToString();
        if (B_EndValueInput != null) B_EndValueInput.text = ((int)(color.b * 255)).ToString();
        if (opacityEndSlider != null) color.a = opacityEndSlider.value;
        else color.a = 1f;

        if (brushObject != null && brushObject.GetComponent<Brush>() != null)
        {
            brushObject.GetComponent<Brush>().SetEndColor(color);
        }
    }

    void Update()
    {
        HandleDesktopToggle();
        if (!subscribedToController)
        {
            SubscribeToController();
        }

        if (enableHandMenuAction != null && enableHandMenuAction.action != null && enableHandMenuAction.action.WasPressedThisFrame())
        {
            handMenuEnableState = !handMenuEnableState;
            SetPanelActiveStates();
        }
    }

    private void OnLeftMenuPressed_Event()
    {
        handMenuEnableState = !handMenuEnableState;
        SetPanelActiveStates();
        if (handMenuEnableState)
        {
            MoveMenuToCameraView(isLeft: true);
            if (Crosshair.Instance != null) Crosshair.Instance.ShowToast("Left menu opened");
        }
    }

    private void HandleDesktopToggle()
    {
        if (Keyboard.current == null) return;

        UnityEngine.InputSystem.Key keyToCheck = UnityEngine.InputSystem.Key.F1;
        if (DesktopInputController.Instance != null)
        {
            keyToCheck = DesktopInputController.Instance.LeftMenuKey;
        }

        var keyControl = Keyboard.current[keyToCheck];
        if (keyControl != null && keyControl.wasPressedThisFrame)
        {
            Debug.Log($"DrawingHandMenuLeft: Detected key press for {keyToCheck}");
            handMenuEnableState = !handMenuEnableState;
            SetPanelActiveStates();
            if (handMenuEnableState)
            {
                MoveMenuToCameraView(isLeft: true);
            }
        }
    }

    private void OnDestroy()
    {
        if (DesktopInputController.Instance != null && subscribedToController)
        {
            DesktopInputController.Instance.LeftMenuPressed -= OnLeftMenuPressed_Event;
            subscribedToController = false;
        }
    }

    private void SubscribeToController()
    {
        if (subscribedToController) return;
        if (DesktopInputController.Instance != null)
        {
            DesktopInputController.Instance.LeftMenuPressed += OnLeftMenuPressed_Event;
            subscribedToController = true;
            Debug.Log("DrawingHandMenuLeft: Subscribed to DesktopInputController.LeftMenuPressed");
        }
    }

    private void MoveMenuToCameraView(bool isLeft)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Transform root = this.transform;
        // Detach briefly to place in front of camera
        root.SetParent(null);

        Vector3 offset = cam.transform.forward * 1.0f;
        offset += cam.transform.up * -0.15f;
        offset += cam.transform.right * (isLeft ? -0.35f : 0.35f);

        root.position = cam.transform.position + offset;
        root.rotation = Quaternion.LookRotation(root.position - cam.transform.position);

        // If there's a Canvas, set its world camera
        var canvas = root.GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.worldCamera = cam;
        }
    }
}