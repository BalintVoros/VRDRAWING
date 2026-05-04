using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

// Persistent desktop drawing menu: single unified UI for desktop users.
public class DesktopDrawingMenu : MonoBehaviour
{
    public static DesktopDrawingMenu Instance { get; private set; }

    private GameObject panel;
    private bool pinned = false;
    private Brush targetBrush;
    private Button pinButtonRef;
    private Text startWidthValueText;
    private Text endWidthValueText;
    private Text opacityValueText;
    private Text statusValueText;
    private Brush.DrawingMode currentMode = Brush.DrawingMode.None;
    private Color currentColor = Color.white;
    private float currentStartWidth = 0.01f;
    private float currentEndWidth = 0.01f;
    private float currentOpacity = 1f;
    // debounce to avoid double-invokes from multiple input paths
    private float lastToggleTime = 0f;
    private const float toggleDebounceSeconds = 0.18f;
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this.gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
        CreateMenuUI();
    }

    void Start()
    {
        // Try to find a Brush to control
        targetBrush = FindObjectOfType<Brush>();
    }

    void Update()
    {
        // Toggle menu with G key (desktop)
        bool gPressed = false;
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var key = UnityEngine.InputSystem.Keyboard.current.gKey;
            if (key != null && key.wasPressedThisFrame) gPressed = true;
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.G)) gPressed = true;
        }

        if (gPressed)
        {
            ToggleVisible();
        }

        // Diagnostics: when menu is visible, detect mouse clicks and log EventSystem raycasts
        if (panel != null && panel.activeSelf)
        {
            bool clicked = false;
            Vector2 pos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse.leftButton.wasPressedThisFrame) { clicked = true; pos = mouse.position.ReadValue(); }
            }
            else
            {
                if (Input.GetMouseButtonDown(0)) { clicked = true; pos = Input.mousePosition; }
            }

            if (clicked)
            {
                ProcessClickAt(pos);
            }
        }
    }

    private void ProcessClickAt(Vector2 pos)
    {
        if (EventSystem.current == null)
        {
            Debug.Log("[DesktopDrawingMenu] EventSystem.current == null when processing click");
            return;
        }

        PointerEventData ped = new PointerEventData(EventSystem.current) { position = pos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        Debug.Log($"[DesktopDrawingMenu] ProcessClickAt {pos} RaycastHits={results.Count}");

        for (int i = 0; i < results.Count; i++)
        {
            var go = results[i].gameObject;
            Debug.Log($"[DesktopDrawingMenu] Result[{i}] = {go.name} (root={go.transform.root.name})");
            // Only consider results that are children of our panel
            if (panel != null && !go.transform.IsChildOf(panel.transform))
                continue;

            // If the hit object has a Button, invoke it directly
            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                Debug.Log($"[DesktopDrawingMenu] Invoking Button.onClick on {go.name}");
                btn.onClick.Invoke();
                return;
            }

            // Sometimes the hit is the text; try parent chain
            var parentBtn = go.GetComponentInParent<Button>();
            if (parentBtn != null && parentBtn.transform.IsChildOf(panel.transform))
            {
                Debug.Log($"[DesktopDrawingMenu] Invoking parent Button.onClick on {parentBtn.gameObject.name}");
                parentBtn.onClick.Invoke();
                return;
            }
        }
    }

    private void EnsureBrush()
    {
        if (targetBrush == null)
            targetBrush = FindObjectOfType<Brush>();
    }

    private void CreateMenuUI()
    {
        GameObject canvasObj = GameObject.Find("DesktopUICanvas");
        Canvas canvas = null;
        if (canvasObj == null)
        {
            canvasObj = new GameObject("DesktopUICanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;
            DontDestroyOnLoad(canvasObj);
        }
        // Ensure there is an EventSystem so UI raycasts work
        if (EventSystem.current == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            var es = esGO.AddComponent<EventSystem>();
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Debug.Log("[DesktopDrawingMenu] Created EventSystem for menu interaction.");
            DontDestroyOnLoad(esGO);
        }
        else
        {
            canvas = canvasObj.GetComponent<Canvas>() ?? canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;
            if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }
        }

        panel = new GameObject("DesktopDrawingMenuPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        // Center the menu so it feels like an inventory panel.
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1020f, 420f);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        CanvasGroup canvasGroup = panel.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        canvasGroup.ignoreParentGroups = false;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 10;
        layout.padding = new RectOffset(18,18,18,18);

        ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // First row: header and close/pin
        var header = CreateRow(panel.transform);
        CreateText(header.transform, "Drawing Menu", 22, TextAnchor.MiddleLeft, 320f);
        CreateButton(header.transform, "Close", () => { SetVisible(false); }, 112f);
        pinButtonRef = CreateButton(header.transform, "Pin", () => { pinned = !pinned; UpdatePinLabel(); }, 112f);

        // Status row so changes are visible even before the brush effect is obvious in-scene.
        var statusRow = CreateRow(panel.transform);
        CreateText(statusRow.transform, "Status", 16, TextAnchor.MiddleLeft, 80f);
        statusValueText = CreateText(statusRow.transform, "Mode: - | Color: -", 16, TextAnchor.MiddleLeft, 400f);

        // Mode dropdown
        var modeRow = CreateRow(panel.transform);
        CreateText(modeRow.transform, "Mode", 16, TextAnchor.MiddleLeft, 80f);
        CreateButton(modeRow.transform, "Draw", () => ApplyModeToAll(Brush.DrawingMode.Draw), 96f);
        CreateButton(modeRow.transform, "Erase", () => ApplyModeToAll(Brush.DrawingMode.Erase), 96f);
        CreateButton(modeRow.transform, "Grab", () => ApplyModeToAll(Brush.DrawingMode.Grab), 96f);
        CreateButton(modeRow.transform, "None", () => ApplyModeToAll(Brush.DrawingMode.None), 96f);

        // Color swatches
        var colorRow = CreateRow(panel.transform);
        CreateText(colorRow.transform, "Color", 16, TextAnchor.MiddleLeft, 80f);
        CreateSwatch(colorRow.transform, new Color(1f, 0.2f, 0.2f), () => ApplyColorToAll(new Color(1f, 0.2f, 0.2f, GetCurrentOpacity())));
        CreateSwatch(colorRow.transform, new Color(0.2f, 1f, 0.2f), () => ApplyColorToAll(new Color(0.2f, 1f, 0.2f, GetCurrentOpacity())));
        CreateSwatch(colorRow.transform, new Color(0.2f, 0.5f, 1f), () => ApplyColorToAll(new Color(0.2f, 0.5f, 1f, GetCurrentOpacity())));
        CreateSwatch(colorRow.transform, new Color(1f, 0.9f, 0.2f), () => ApplyColorToAll(new Color(1f, 0.9f, 0.2f, GetCurrentOpacity())));
        CreateSwatch(colorRow.transform, new Color(1f, 1f, 1f), () => ApplyColorToAll(new Color(1f, 1f, 1f, GetCurrentOpacity())));

        // Width controls
        var widthRow = CreateRow(panel.transform);
        CreateText(widthRow.transform, "Start", 16, TextAnchor.MiddleLeft, 64f);
        CreateButton(widthRow.transform, "-", () => AdjustStartWidth(-0.005f), 44f);
        startWidthValueText = CreateValueText(widthRow.transform, brushStartWidthText(), 72f);
        CreateButton(widthRow.transform, "+", () => AdjustStartWidth(0.005f), 44f);
        CreateText(widthRow.transform, "End", 16, TextAnchor.MiddleLeft, 56f);
        CreateButton(widthRow.transform, "-", () => AdjustEndWidth(-0.005f), 44f);
        endWidthValueText = CreateValueText(widthRow.transform, brushEndWidthText(), 72f);
        CreateButton(widthRow.transform, "+", () => AdjustEndWidth(0.005f), 44f);

        // Opacity
        var opacityRow = CreateRow(panel.transform);
        CreateText(opacityRow.transform, "Opacity", 16, TextAnchor.MiddleLeft, 80f);
        CreateButton(opacityRow.transform, "-", () => AdjustOpacity(-0.05f), 44f);
        opacityValueText = CreateValueText(opacityRow.transform, opacityText(), 72f);
        CreateButton(opacityRow.transform, "+", () => AdjustOpacity(0.05f), 44f);

        // initial state
        SyncCurrentStateFromBrush();
        RefreshStatusText();
        SetVisible(false);
    }

    private void SyncCurrentStateFromBrush()
    {
        var brush = FindAnyBrush();
        if (brush == null) return;

        currentMode = brush.drawingMode;
        currentColor = brush.brushStartColor;
        currentStartWidth = brush.brushStartWidth;
        currentEndWidth = brush.brushEndWidth;
        currentOpacity = brush.brushStartColor.a;
    }

    private GameObject CreateRow(Transform parent)
    {
        var go = new GameObject("Row");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(500, 28);
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 6;
        return go;
    }

    private Text CreateText(Transform parent, string text, int size, TextAnchor align, float preferredWidth = 120f)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = size;
        txt.alignment = align;
        txt.color = Color.white;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 10;
        txt.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.preferredHeight = 24f;
        return txt;
    }

    private Text CreateValueText(Transform parent, string text, float preferredWidth)
    {
        var valueText = CreateText(parent, text, 14, TextAnchor.MiddleCenter, preferredWidth);
        return valueText;
    }

    private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float preferredWidth = 84f)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f,0.15f,0.15f,0.95f);
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        // Ensure button is interactable and has a simple navigation to avoid unexpected focus behavior
        btn.interactable = true;
        btn.transition = Selectable.Transition.ColorTint;
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
        // Debug log to verify clicks reach the button
        btn.onClick.AddListener(() => { Debug.Log($"[DesktopDrawingMenu] Button clicked: {label}"); });
        var txtGo = new GameObject("Text"); txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<Text>(); txt.text = label; txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter; txt.resizeTextForBestFit = true; txt.resizeTextMinSize = 10;
        var tr = txt.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(preferredWidth, 26);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.preferredHeight = 26f;
        return btn;
    }

    private Button CreateSwatch(Transform parent, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Swatch");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        btn.interactable = true;
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
        btn.onClick.AddListener(() => { Debug.Log($"[DesktopDrawingMenu] Swatch clicked: {color}"); });
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(28f, 26f);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 28f;
        le.preferredHeight = 26f;
        return btn;
    }

    private Slider CreateSlider(Transform parent, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChanged)
    {
        var go = new GameObject("Slider"); go.transform.SetParent(parent, false);
        var slider = go.AddComponent<Slider>();
        slider.minValue = min; slider.maxValue = max; slider.value = value;
        slider.onValueChanged.AddListener(onChanged);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(120, 20);
        var bg = go.AddComponent<Image>(); bg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        return slider;
    }

    public void ToggleVisible()
    {
        if (Time.realtimeSinceStartup - lastToggleTime < toggleDebounceSeconds) return;
        lastToggleTime = Time.realtimeSinceStartup;
        SetVisible(!panel.activeSelf);
    }

    public void SetVisible(bool visible)
    {
        if (panel != null) panel.SetActive(visible);

        if (DesktopInputController.Instance != null)
        {
            DesktopInputController.Instance.SetMenuOpen(visible);
        }
    }

    public bool IsVisible()
    {
        return panel != null && panel.activeSelf;
    }

    public void Pin()
    {
        pinned = true;
        SetVisible(true);
        UpdatePinLabel();
    }

    private void UpdatePinLabel()
    {
        if (pinButtonRef == null) return;
        var txt = pinButtonRef.GetComponentInChildren<Text>();
        if (txt != null) txt.text = pinned ? "Unpin" : "Pin";
    }

    private string brushStartWidthText()
    {
        return FindAnyBrushWidth(true).ToString("F3");
    }

    private string brushEndWidthText()
    {
        return FindAnyBrushWidth(false).ToString("F3");
    }

    private string opacityText()
    {
        var brush = FindAnyBrush();
        if (brush == null) return "1.00";
        return brush.brushStartColor.a.ToString("F2");
    }

    private float GetCurrentOpacity()
    {
        var brush = FindAnyBrush();
        if (brush == null) return 1f;
        return brush.brushStartColor.a;
    }

    private Brush FindAnyBrush()
    {
        EnsureBrush();
        if (targetBrush != null) return targetBrush;
        var brushes = FindObjectsOfType<Brush>();
        return brushes.Length > 0 ? brushes[0] : null;
    }

    private float FindAnyBrushWidth(bool start)
    {
        var brush = FindAnyBrush();
        if (brush == null) return 0.01f;
        return start ? brush.brushStartWidth : brush.brushEndWidth;
    }

    private void RefreshValueTexts()
    {
        if (startWidthValueText != null) startWidthValueText.text = brushStartWidthText();
        if (endWidthValueText != null) endWidthValueText.text = brushEndWidthText();
        if (opacityValueText != null) opacityValueText.text = opacityText();
    }

    private void AdjustStartWidth(float delta)
    {
        var brushes = FindObjectsOfType<Brush>();
        Debug.Log($"[DesktopDrawingMenu] AdjustStartWidth {delta} on {brushes.Length} brushes");
        currentStartWidth = Mathf.Clamp(currentStartWidth + delta, 0.001f, 0.2f);
        foreach (var brush in brushes)
        {
            brush.brushStartWidth = currentStartWidth;
        }
        RefreshValueTexts();
    }

    private void AdjustEndWidth(float delta)
    {
        var brushes = FindObjectsOfType<Brush>();
        Debug.Log($"[DesktopDrawingMenu] AdjustEndWidth {delta} on {brushes.Length} brushes");
        currentEndWidth = Mathf.Clamp(currentEndWidth + delta, 0.001f, 0.2f);
        foreach (var brush in brushes)
        {
            brush.brushEndWidth = currentEndWidth;
        }
        RefreshValueTexts();
    }

    private void AdjustOpacity(float delta)
    {
        var brushes = FindObjectsOfType<Brush>();
        currentOpacity = Mathf.Clamp01(currentOpacity + delta);
        Debug.Log($"[DesktopDrawingMenu] AdjustOpacity {delta} → {currentOpacity:F2} on {brushes.Length} brushes");
        foreach (var brush in brushes)
        {
            var start = brush.brushStartColor;
            start.a = currentOpacity;
            brush.SetStartColor(start);

            var end = brush.brushEndColor;
            end.a = currentOpacity;
            brush.SetEndColor(end);
        }
        RefreshValueTexts();
        RefreshStatusText();
    }

    // Apply mode to all brushes in scene
    private void ApplyModeToAll(Brush.DrawingMode mode)
    {
        var brushes = FindObjectsOfType<Brush>();
        Debug.Log($"[DesktopDrawingMenu] ApplyModeToAll {mode} on {brushes.Length} brushes");
        currentMode = mode;
        foreach (var b in brushes)
        {
            b.drawingMode = mode;
        }
        RefreshStatusText();
        if (Crosshair.Instance != null) Crosshair.Instance.ShowToast($"Mode set: {mode}");
    }

    private void ApplyStartColorToAll(Color c)
    {
        var brushes = FindObjectsOfType<Brush>();
        foreach (var b in brushes) b.SetStartColor(c);
        if (Crosshair.Instance != null) Crosshair.Instance.ShowToast($"Start color set");
    }

    private void ApplyColorToAll(Color c)
    {
        var brushes = FindObjectsOfType<Brush>();
        Debug.Log($"[DesktopDrawingMenu] ApplyColorToAll {c} on {brushes.Length} brushes");
        currentColor = c;
        foreach (var b in brushes)
        {
            b.SetStartColor(c);
            b.SetEndColor(c);
        }
        RefreshStatusText();
        if (Crosshair.Instance != null) Crosshair.Instance.ShowToast("Color set");
    }

    private void RefreshStatusText()
    {
        if (statusValueText == null)
            return;

        string colorText = $"#{(int)(currentColor.r * 255):X2}{(int)(currentColor.g * 255):X2}{(int)(currentColor.b * 255):X2}";
        statusValueText.text = $"Mode: {currentMode} | Color: {colorText} | W:{currentStartWidth:F3}→{currentEndWidth:F3} | Opacity: {currentOpacity:F2}";
    }

    private void ApplyEndColorToAll(Color c)
    {
        var brushes = FindObjectsOfType<Brush>();
        foreach (var b in brushes) b.SetEndColor(c);
        if (Crosshair.Instance != null) Crosshair.Instance.ShowToast($"End color set");
    }

    public bool IsGradientModeEnabled()
    {
        // naive: try to find a DrawingHandMenuLeft in scene and mirror its state if present
        var left = FindObjectOfType<DrawingHandMenuLeft>();
        if (left != null) return left.IsGradientModeEnabled();
        return false;
    }
}
