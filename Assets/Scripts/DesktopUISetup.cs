using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DesktopUISetup : MonoBehaviour
{
    //ROLE: Sets up the desktop UI elements (crosshair) for desktop mode.
    //      This script should be placed in scenes that support drawing (DrawingScene, etc.)

    [Header("Canvas References")]
    [SerializeField] private Canvas uiCanvas;

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
                Debug.LogError("[DesktopUISetup] No Canvas found in scene! Cannot setup desktop UI.");
                yield break;
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

        // Create a simple crosshair texture (white circle)
        Texture2D crosshairTexture = CreateCrosshairTexture(32);
        Sprite crosshairSprite = Sprite.Create(crosshairTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        image.sprite = crosshairSprite;

        // Add Crosshair script
        Crosshair crosshairScript = crosshairObject.AddComponent<Crosshair>();
        // Ensure crosshair renders above other UI elements
        crosshairObject.transform.SetAsLastSibling();

        Debug.Log("[DesktopUISetup] Crosshair created successfully and placed on top of Canvas.");
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
