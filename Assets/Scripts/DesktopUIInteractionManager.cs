using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class DesktopUIInteractionManager : MonoBehaviour
{
    //ROLE: Manages UI interaction for desktop mode. Ensures proper UI navigation, keyboard input,
    //      and visual feedback when in desktop (non-VR) mode. Works for login screens and menus.

    [SerializeField] private bool enableKeyboardNavigation = true;
    [SerializeField] private bool enableCrosshairClick = true;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private EventSystem eventSystem;

    private bool isDesktopMode = false;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Detect desktop mode
        isDesktopMode = DesktopInputController.Instance != null;

        if (!isDesktopMode)
        {
            Debug.Log("[DesktopUIInteractionManager] Desktop mode not detected. Disabling desktop UI management.");
            enabled = false;
            return;
        }

        SetupDesktopUIInteraction();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isDesktopMode)
            return;

        // Scene transitions can destroy/recreate EventSystem and Canvas references.
        eventSystem = null;
        graphicRaycaster = null;
        SetupDesktopUIInteraction();
    }

    private void SetupDesktopUIInteraction()
    {
        // Find or create EventSystem if needed
        if (eventSystem == null)
        {
            eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystem = eventSystemGO.AddComponent<EventSystem>();
                // Prefer Input System UI Input Module when available
                var inputSystemModule = eventSystemGO.AddComponent<InputSystemUIInputModule>();
                if (inputSystemModule == null)
                {
                    eventSystemGO.AddComponent<StandaloneInputModule>();
                    Debug.Log("[DesktopUIInteractionManager] InputSystemUIInputModule not available; added StandaloneInputModule.");
                }
                else
                {
                    Debug.Log("[DesktopUIInteractionManager] Created new EventSystem with InputSystemUIInputModule for UI interaction.");
                }
            }
            else
            {
                // Ensure Input System module exists on the active EventSystem.
                if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                {
                    eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }
            }
        }

        // Find or create GraphicRaycaster on Canvas if needed
        if (graphicRaycaster == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
                if (graphicRaycaster == null)
                {
                    graphicRaycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                    Debug.Log("[DesktopUIInteractionManager] Added GraphicRaycaster to Canvas.");
                }
            }
        }

        // Ensure cursor is visible for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Select the first selectable element if none selected
        if (eventSystem.currentSelectedGameObject == null)
        {
            Selectable[] selectables = FindObjectsOfType<Selectable>();
            if (selectables.Length > 0)
            {
                eventSystem.SetSelectedGameObject(selectables[0].gameObject);
            }
        }

        Debug.Log("[DesktopUIInteractionManager] Desktop UI interaction setup complete.");
    }

    private void Update()
    {
        if (!isDesktopMode)
            return;

        if (eventSystem == null)
        {
            SetupDesktopUIInteraction();
            if (eventSystem == null)
                return;
        }

        if (enableKeyboardNavigation && Keyboard.current != null)
        {
            // Handle Tab for UI navigation
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                HandleTabNavigation();
            }

            // Handle Enter for button activation
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                HandleEnterKey();
            }
        }

        if (enableCrosshairClick && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleCrosshairClick();
        }
    }

    private void HandleCrosshairClick()
    {
        if (eventSystem == null)
            return;

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
            button = PointerEventData.InputButton.Left
        };

        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        if (results.Count == 0)
            return;

        GameObject target = results[0].gameObject;
        eventSystem.SetSelectedGameObject(target);

        // Trigger a full UI click sequence on the target under crosshair.
        ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerClickHandler);
    }

    private void HandleTabNavigation()
    {
        Selectable current = eventSystem.currentSelectedGameObject?.GetComponent<Selectable>();
        if (current == null)
        {
            // Try to select the first selectable element
            Selectable[] selectables = FindObjectsOfType<Selectable>();
            if (selectables.Length > 0)
            {
                eventSystem.SetSelectedGameObject(selectables[0].gameObject);
            }
            return;
        }

        Selectable next = current.FindSelectableOnDown();
        if (next == null)
            next = current.FindSelectableOnRight();

        if (next != null)
        {
            eventSystem.SetSelectedGameObject(next.gameObject);
        }
    }

    private void HandleEnterKey()
    {
        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null)
            return;

        // If it's a button, click it
        Button button = selected.GetComponent<Button>();
        if (button != null && button.interactable)
        {
            button.onClick.Invoke();
        }

        // If it's an input field, try to submit
        InputField inputField = selected.GetComponent<InputField>();
        if (inputField != null)
        {
            inputField.onEndEdit.Invoke(inputField.text);
        }
    }

    /// <summary>
    /// Programmatically select a UI element (useful for focusing input fields)
    /// </summary>
    public void SelectUIElement(GameObject target)
    {
        if (eventSystem != null && target != null)
        {
            eventSystem.SetSelectedGameObject(target);
        }
    }

    /// <summary>
    /// Check if desktop mode is active
    /// </summary>
    public bool IsDesktopModeActive()
    {
        return isDesktopMode;
    }
}
