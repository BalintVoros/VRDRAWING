using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Brush : MonoBehaviour
{
    public enum DrawingMode
    {
        None    = 0,
        Draw    = 1,
        Erase   = 2,
        Grab    = 3
    }

    public enum HandType
    {
        Left,
        Right
    }

    [Header("Brush properties")]
    [SerializeField] private Transform              brushTip;
    public Material                                 tipMaterial;
    public Material                                 paintingMaterial;
    [Range(0.0001f, 1f)] public float               brushStartWidth     = .003f;
    [Range(0.0001f, 1f)] public float               brushEndWidth       = .003f;
    public Color                                    brushStartColor     = Color.blue;
    public Color                                    brushEndColor       = Color.blue;
    public HandType                                 handType;

    [Header("XR interaction")]
    [SerializeField] private InputActionReference   paintingInputAction;

    [Header("Painting properties")]
    [SerializeField] private LineRenderer           currentLine;
    [SerializeField] private List<string>           pointTimestamps     = new();
    [SerializeField, Range(0.0001f, 0.1f)] float    drawingTreshold     = 0.01f;
    private int                                     index               = 0;
    private bool                                    isPainting          = false;
    public DrawingMode                              drawingMode         = DrawingMode.None;
    [SerializeField] private DrawingHandMenuLeft    drawingHandMenuLeft;
    [SerializeField] private DrawingHandMenuRight   drawingHandMenuRight;
    [SerializeField] public GameObject              drawingObject;

    [Header("Desktop Mode Settings")]
    [SerializeField] private bool                   enableDesktopMode   = true;
    private bool                                    isDesktopMode       = false;
    private Vector3                                 desktopBrushPosition;


    public static readonly List<string> DrawingBlockedScenes = new() { "Login" };

    private void Start()
    {
        drawingObject = GameObject.FindGameObjectWithTag("Drawing");
        if (drawingObject == null)
        {
            if (!DrawingBlockedScenes.Contains(SceneManager.GetActiveScene().name))
            {
                Debug.LogError("No GameObject with tag 'Drawing' found in the scene. Please add one to enable drawing functionality.");
            }
        }
        
        tipMaterial.color = brushStartColor;
        drawingMode = DrawingMode.None;

        // Detect if we're in desktop mode
        isDesktopMode = enableDesktopMode && DesktopInputController.Instance != null;
        if (isDesktopMode)
        {
            if (!DrawingBlockedScenes.Contains(SceneManager.GetActiveScene().name))
            {
                drawingMode = DrawingMode.Draw;
            }
            Debug.Log($"[Brush] Desktop mode detected and enabled for {handType} brush.");
        }
    }

    private void Update()
    {
        if (isDesktopMode)
        {
            HandleDesktopModeShortcuts();
        }

        // Handle input based on mode
        if (isDesktopMode && DesktopInputController.Instance != null)
        {
            isPainting = DesktopInputController.Instance.IsDrawingPressed();
            desktopBrushPosition = DesktopInputController.Instance.GetDrawingPosition();
        }
        else
        {
            if (paintingInputAction != null)
            {
                isPainting = (float)paintingInputAction.action.ReadValue<float>() > 0.5f;
            }
        }

        if (!drawingHandMenuLeft.handMenuEnableState && !drawingHandMenuRight.handMenuEnableState)
        {
            if (drawingMode == DrawingMode.Draw)
            {
                if (isPainting) Paint();
                else if (currentLine != null)
                {
                    if (currentLine.gameObject.GetComponent<MeshCollider>() == null)
                    {
                        MeshCollider meshCollider = currentLine.gameObject.AddComponent<MeshCollider>();
                        Mesh bakedMesh = new();
                        currentLine.BakeMesh(bakedMesh, useTransform: true);
                        meshCollider.sharedMesh = bakedMesh;
                    }
                    currentLine.gameObject.tag = "Line";
                    currentLine = null;
                }
            }
            else if (drawingMode == DrawingMode.Erase)
            {
                if (isPainting)
                {
                    Vector3 rayOrigin = isDesktopMode ? desktopBrushPosition : brushTip.position;
                    Vector3 rayDirection = isDesktopMode ? DesktopInputController.Instance.GetDrawingDirection() : brushTip.forward;

                    if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, 100f))
                    {
                        if (hit.collider.CompareTag("Line"))
                        {
                            if (drawingObject.TryGetComponent<VRDrawing>(out VRDrawing vrDrawingData))
                            {
                                for (int idx = 0; idx < vrDrawingData.drawing.lines.Count; ++idx)
                                {
                                    if (vrDrawingData.drawing.lines[idx].id == hit.collider.gameObject.name)
                                    {
                                        vrDrawingData.drawing.lines.RemoveAt(idx);
                                        break;
                                    }
                                }
                            }
                            Destroy(hit.collider.gameObject);
                        }
                    }
                }
            }
        }
    }

    private void HandleDesktopModeShortcuts()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            drawingMode = DrawingMode.Draw;
            Debug.Log($"[Brush] {handType} switched to Draw mode.");
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            drawingMode = DrawingMode.Erase;
            Debug.Log($"[Brush] {handType} switched to Erase mode.");
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            drawingMode = DrawingMode.Grab;
            Debug.Log($"[Brush] {handType} switched to Grab mode.");
        }
    }

    public void SetStartColor(Color color)
    {
        brushStartColor = color;
        if (tipMaterial != null && transform.Find("Tip") != null && transform.Find("Tip").GetComponent<MeshRenderer>() != null)
        {
            Material tipMaterialInstance = gameObject.transform.Find("Tip").GetComponent<MeshRenderer>().material;
            tipMaterialInstance.color = color;
        }
        else Debug.LogWarning("Brush tip material or Tip GameObject/MeshRenderer not found for ColorPickerStart.");
    }

    public void SetEndColor(Color color)
    {
        brushEndColor = color;
    }

    private void Paint()
    {
        Vector3 currentBrushPosition = isDesktopMode ? desktopBrushPosition : brushTip.position;

        if (currentLine == null)
        {
            /* Initialize the LineRenderer with the first position */
            index = 0;
            currentLine = new GameObject(name: $"Line_{GetTimestamp()}").AddComponent<LineRenderer>();
            currentLine.material = paintingMaterial;
            currentLine.startColor = brushStartColor;
            currentLine.endColor = brushEndColor;
            currentLine.startWidth = brushStartWidth;
            currentLine.endWidth = brushEndWidth;
            currentLine.positionCount = 1;
            currentLine.SetPosition(0, currentBrushPosition);
            currentLine.transform.SetParent(drawingObject.transform);

            /* Initialize the stored data of the current line */
            if (drawingObject.TryGetComponent<VRDrawing>(out VRDrawing vrDrawingData))
            {
                vrDrawingData.drawing.lines.Add(new DrawingData.Line
                {
                    id = currentLine.name,
                    points = new List<DrawingData.Point>(),
                    startWidth = brushStartWidth,
                    endWidth = brushEndWidth,
                    startColor = brushStartColor,
                    endColor = brushEndColor,
                    hand = handType.ToString()
                });
                pointTimestamps.Clear();
                pointTimestamps.Add(GetTimestamp());
                vrDrawingData.drawing.lines[^1].points.Add(new DrawingData.Point(currentBrushPosition, pointTimestamps[^1]));
            }
            else
            {
                //Suppress annoying error messages in scenes where drawing is blocked
                if (DrawingBlockedScenes.Contains(SceneManager.GetActiveScene().name)) Debug.LogWarning("Drawing is blocked in this scene.");
                else Debug.LogError("The drawingObject does not have a VRDrawing component attached.");
            }
        }
        else
        {
            /* Add new points to the current line and update the stored data of it */
            var currentPosition = currentLine.GetPosition(index);
            if (Vector3.Distance(currentPosition, currentBrushPosition) > drawingTreshold)
            {
                index++;
                currentLine.positionCount = index + 1;
                currentLine.SetPosition(index, currentBrushPosition);
                if (drawingObject.TryGetComponent<VRDrawing>(out VRDrawing vrDrawingData))
                {
                    pointTimestamps.Add(GetTimestamp());
                    vrDrawingData.drawing.lines[^1].points.Add(new DrawingData.Point(currentBrushPosition, pointTimestamps[^1]));
                }
                else
                {
                    //Suppress annoying error messages in scenes where drawing is blocked
                    if (DrawingBlockedScenes.Contains(SceneManager.GetActiveScene().name)) Debug.LogWarning("Drawing is blocked in this scene.");
                    else Debug.LogError("The drawingObject does not have a VRDrawing component attached.");
                }
            }
        }
    }

    private string GetTimestamp() => DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_") + ((int)(DateTime.Now.Millisecond)).ToString("D3");
}
