using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Runtime quick control panel for desktop users. Creates a small panel with buttons
// to open left/right hand menus and cycle drawing mode. Auto-creates canvas if needed.
public class DesktopQuickControls : MonoBehaviour
{
    private GameObject panel;

    void Start()
    {
        CreatePanel();
        Debug.Log("DesktopQuickControls: Panel created and initialized.");
    }

    private void CreatePanel()
    {
        // Use or create a dedicated root canvas for desktop UI so we can mark it persistent.
        GameObject canvasObj = GameObject.Find("DesktopUICanvas");
        Canvas canvas = null;
        if (canvasObj == null)
        {
            canvasObj = new GameObject("DesktopUICanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);
            Debug.Log("DesktopQuickControls: Created persistent DesktopUICanvas.");
        }
        else
        {
            canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
            // Ensure quick controls are visible on top
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;
            Debug.Log("DesktopQuickControls: Reusing existing DesktopUICanvas.");
        }

        // Ensure EventSystem exists. Prefer the Input System UI module if available.
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("DesktopEventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Try to add the new Input System UI Module if present, otherwise fall back to StandaloneInputModule
            var inputSystemUIModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemUIModuleType != null)
            {
                es.AddComponent(inputSystemUIModuleType);
                Debug.Log("DesktopQuickControls: Added InputSystemUIInputModule for UI interaction.");
            }
            else
            {
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("DesktopQuickControls: InputSystemUIInputModule not found, added StandaloneInputModule.");
            }
            DontDestroyOnLoad(es);
        }

        panel = new GameObject("DesktopQuickControlsPanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(8f, -8f);
        rt.sizeDelta = new Vector2(220f, 140f);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.spacing = 6f;
        layout.padding = new RectOffset(6, 6, 6, 6);

        CreateButton("Toggle Drawing Menu", OnToggleDrawingMenu);
        CreateButton("Pin Drawing Menu", OnPinDrawingMenu);
        CreateButton("Cycle Mode", OnCycleMode);
        CreateButton("Open Color Menu", OnOpenColorMenu);
        CreateButton("Toggle Fullscreen", OnToggleFullscreen);

        // Panel is parented to a persistent root canvas, so we do not call DontDestroyOnLoad on it directly.
        Debug.Log("DesktopQuickControls: Quick controls panel created as child of DesktopUICanvas.");
    }

    private void CreateButton(string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(label + "Btn");
        btnObj.transform.SetParent(panel.transform, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 28f);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = img.color;
        cb.highlightedColor = new Color(0.25f, 0.25f, 0.25f, 0.95f);
        cb.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        Text txt = txtObj.AddComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 14;

        RectTransform trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    private void OnToggleDrawingMenu()
    {
        // Ensure the DesktopDrawingMenu exists and toggle it
        if (DesktopDrawingMenu.Instance == null)
        {
            var go = new GameObject("DesktopDrawingMenu");
            go.AddComponent<DesktopDrawingMenu>();
        }
        DesktopDrawingMenu.Instance.ToggleVisible();
        if (Crosshair.Instance != null) Crosshair.Instance.ShowToast("Drawing menu toggled");
    }

    private void OnPinDrawingMenu()
    {
        // Create or ensure menu exists, then pin by setting visible and leaving it (simple approach)
        if (DesktopDrawingMenu.Instance == null)
        {
            var go = new GameObject("DesktopDrawingMenu");
            go.AddComponent<DesktopDrawingMenu>();
        }
        DesktopDrawingMenu.Instance.Pin();
        if (Crosshair.Instance != null) Crosshair.Instance.ShowToast("Drawing menu pinned (visible)");
    }

    private void OnCycleMode()
    {
        // Try to find a Brush and cycle its drawingMode
        Brush b = FindObjectOfType<Brush>();
        if (b != null)
        {
            var current = b.drawingMode;
            Brush.DrawingMode next = Brush.DrawingMode.Draw;
            switch (current)
            {
                case Brush.DrawingMode.None: next = Brush.DrawingMode.Draw; break;
                case Brush.DrawingMode.Draw: next = Brush.DrawingMode.Erase; break;
                case Brush.DrawingMode.Erase: next = Brush.DrawingMode.Grab; break;
                case Brush.DrawingMode.Grab: next = Brush.DrawingMode.None; break;
            }
            b.drawingMode = next;
            if (Crosshair.Instance != null) Crosshair.Instance.ShowToast($"Mode: {next}");
        }
        else
        {
            if (Crosshair.Instance != null) Crosshair.Instance.ShowToast("No Brush found");
        }
    }

    private void OnOpenColorMenu()
    {
        // Open the unified drawing menu which contains color controls
        if (DesktopDrawingMenu.Instance == null)
        {
            var go = new GameObject("DesktopDrawingMenu");
            go.AddComponent<DesktopDrawingMenu>();
        }
        DesktopDrawingMenu.Instance.SetVisible(true);
        if (Crosshair.Instance != null) Crosshair.Instance.ShowToast("Color menu opened");
    }

    private void OnToggleFullscreen()
    {
        var tfb = FindObjectOfType<ToggleFullscreenButton>();
        if (tfb != null)
        {
            tfb.ToggleFullscreen();
        }
        else
        {
            if (Crosshair.Instance != null) Crosshair.Instance.ShowToast("No fullscreen control found");
        }
    }
}
