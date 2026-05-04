using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Creates a small fullscreen toggle button at runtime (top-right) and binds F11.
// Attach this script to any persistent object (Player or DesktopInputController) or
// leave it in the scene; it will auto-create a Canvas if none exists.
public class ToggleFullscreenButton : MonoBehaviour
{
    [Header("Button Settings")]
    public bool createIfNoCanvas = true;
    public int buttonWidth = 140;
    public int buttonHeight = 36;
    public int margin = 8;
    public KeyCode toggleKey = KeyCode.F11;
    public int targetWidth = 0; // 0 = keep current
    public int targetHeight = 0; // 0 = keep current
    public bool useExclusiveFullscreen = false;

    private Button toggleButton;

    void Start()
    {
        EnsureButtonExists();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleFullscreen();
        }
    }

    public void ToggleFullscreen()
    {
        bool isFull = Screen.fullScreen;
        if (!isFull)
        {
            if (targetWidth > 0 && targetHeight > 0)
            {
                Screen.SetResolution(targetWidth, targetHeight, useExclusiveFullscreen);
            }
            else
            {
                Screen.fullScreenMode = useExclusiveFullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;
                Screen.fullScreen = true;
            }
        }
        else
        {
            Screen.fullScreen = false;
        }

        if (Crosshair.Instance != null)
        {
            Crosshair.Instance.ShowToast("Fullscreen: " + (Screen.fullScreen ? "ON" : "OFF"), 1.2f);
        }

        Debug.Log($"ToggleFullscreenButton: Fullscreen now = {Screen.fullScreen}");
    }

    private void EnsureButtonExists()
    {
        Canvas canvas = FindObjectOfType<Canvas>(true);
        if (canvas == null && createIfNoCanvas)
        {
            GameObject canvasObj = new GameObject("DesktopUICanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);
        }

        if (canvas == null)
        {
            Debug.LogWarning("ToggleFullscreenButton: No Canvas found and createIfNoCanvas is false.");
            return;
        }

        // Ensure EventSystem
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }

        // Create container
        GameObject btnObj = new GameObject("ToggleFullscreenButton");
        btnObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        rt.anchoredPosition = new Vector2(-margin, -margin);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);

        toggleButton = btnObj.AddComponent<Button>();
        ColorBlock cb = toggleButton.colors;
        cb.normalColor = new Color(0f, 0f, 0f, 0.6f);
        cb.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 0.75f);
        cb.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.75f);
        toggleButton.colors = cb;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform trt = textObj.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        Text txt = textObj.AddComponent<Text>();
        txt.text = "Toggle Fullscreen (F11)";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 14;

        toggleButton.onClick.AddListener(ToggleFullscreen);

        DontDestroyOnLoad(btnObj);
    }
}
