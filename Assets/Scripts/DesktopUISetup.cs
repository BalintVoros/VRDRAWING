using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class DesktopUISetup : MonoBehaviour
{
    //ROLE: Sets up the desktop UI elements (crosshair) for desktop mode.
    //      This script should be placed in scenes that support drawing (DrawingScene, etc.)

    [Header("Canvas References")]
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private bool createMenuButtonIfMissing = true;

    [Header("Crosshair Settings")]
    [SerializeField] private bool createCrosshairIfMissing = true;
    [SerializeField] private Vector2 crosshairSize = new Vector2(30, 30);

    private void Start()
    {
        StartCoroutine(SetupWhenDesktopControllerReady());
    }

    private IEnumerator SetupWhenDesktopControllerReady()
    {
        float timeout = 5f;
        float elapsed = 0f;
        while (DesktopInputController.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (DesktopInputController.Instance == null)
        {
            Debug.Log("[DesktopUISetup] DesktopInputController not found. Skipping desktop UI setup.");
            yield break;
        }

        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
            if (uiCanvas == null)
            {
                GameObject canvasObject = new GameObject("DesktopUICanvas");
                uiCanvas = canvasObject.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                uiCanvas.overrideSorting = true;
                uiCanvas.sortingOrder = 32767;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
                DontDestroyOnLoad(canvasObject);
                Debug.Log("[DesktopUISetup] Created persistent DesktopUICanvas.");
            }
        }

        // Setup or find crosshair
        Crosshair crosshair = FindObjectOfType<Crosshair>();
        if (crosshair == null && createCrosshairIfMissing)
        {
            CreateCrosshair();
        }
        else if (crosshair != null)
        {
            Debug.Log("[DesktopUISetup] Crosshair already exists in scene.");
        }

        if (createMenuButtonIfMissing)
        {
            EnsureMenuButton();
        }

        // Ensure the unified desktop drawing menu exists in desktop scenes
        if (DesktopDrawingMenu.Instance == null)
        {
            var go = new GameObject("DesktopDrawingMenu");
            go.AddComponent<DesktopDrawingMenu>();
            Debug.Log("[DesktopUISetup] Created DesktopDrawingMenu for this scene.");
        }

        yield break;
    }

    private void CreateCrosshair()
    {
        // Create crosshair GameObject
        GameObject crosshairObject = new GameObject("Crosshair");
        crosshairObject.transform.SetParent(uiCanvas.transform, false);

        // Add RectTransform
        RectTransform rectTransform = crosshairObject.AddComponent<RectTransform>();
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = crosshairSize;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        // Add Image component for crosshair appearance
        Image image = crosshairObject.AddComponent<Image>();
        image.color = Color.white;

        // Ensure crosshair does not block UI raycasts
        image.raycastTarget = false;
        CanvasGroup cg = crosshairObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        // Create a simple crosshair texture (white circle)
        Texture2D crosshairTexture = CreateCrosshairTexture(32);
        Sprite crosshairSprite = Sprite.Create(crosshairTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        image.sprite = crosshairSprite;

        // Add Crosshair script
        Crosshair crosshairScript = crosshairObject.AddComponent<Crosshair>();
        // Ensure crosshair renders above other UI elements (but does not block raycasts)
        crosshairObject.transform.SetAsLastSibling();

        Debug.Log("[DesktopUISetup] Crosshair created successfully and placed on top of Canvas. (raycastTarget=false, blocksRaycasts=false)");
    }

    private void EnsureMenuButton()
    {
        if (uiCanvas == null)
            return;

        Transform existing = uiCanvas.transform.Find("DesktopMenuButton");
        if (existing != null)
            return;

        GameObject buttonObject = new GameObject("DesktopMenuButton");
        buttonObject.transform.SetParent(uiCanvas.transform, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(12f, -12f);
        rectTransform.sizeDelta = new Vector2(120f, 34f);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = background.color;
        colors.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
        colors.pressedColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            Debug.Log("[DesktopUISetup] Drawing Menu button clicked.");
            if (DesktopDrawingMenu.Instance == null)
            {
                GameObject menuObject = new GameObject("DesktopDrawingMenu");
                menuObject.AddComponent<DesktopDrawingMenu>();
            }

            DesktopDrawingMenu.Instance.ToggleVisible();
        });

        GameObject labelObject = new GameObject("Text");
        labelObject.transform.SetParent(buttonObject.transform, false);
        Text label = labelObject.AddComponent<Text>();
        label.text = "Drawing Menu";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 16;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private Texture2D CreateCrosshairTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        Color[] pixels = new Color[size * size];

        int center = size / 2;
        int thickness = 1;
        int crosshairLength = size / 4;

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                pixels[i * size + j] = Color.clear;

                // Center dot
                if (Mathf.Abs(i - center) <= thickness && Mathf.Abs(j - center) <= thickness)
                {
                    pixels[i * size + j] = Color.white;
                }

                // Vertical line
                if (Mathf.Abs(j - center) <= thickness && i >= center - crosshairLength && i <= center + crosshairLength)
                {
                    pixels[i * size + j] = Color.white;
                }

                // Horizontal line
                if (Mathf.Abs(i - center) <= thickness && j >= center - crosshairLength && j <= center + crosshairLength)
                {
                    pixels[i * size + j] = Color.white;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
