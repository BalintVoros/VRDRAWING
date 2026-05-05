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

    public void ConfigureRuntimeBrush(Transform tipTransform, Material tipMat, Material paintMat, GameObject targetDrawingObject, HandType runtimeHandType)
    {
        brushTip = tipTransform;
        tipMaterial = tipMat;
        paintingMaterial = paintMat;
        drawingObject = targetDrawingObject;
        handType = runtimeHandType;
        enableDesktopMode = true;
        isDesktopMode = true;
        drawingMode = DrawingMode.Draw;
    }

    private void Awake()
    {
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log($"[Brush] Awake for {handType} on {name}");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        drawingObject = GameObject.FindGameObjectWithTag("Drawing");
        if (drawingObject != null)
        {
            Debug.Log($"[Brush] {handType} re-bound drawingObject to {drawingObject.name} on scene {scene.name}");
        }
        else if (!DrawingBlockedScenes.Contains(scene.name))
        {
            Debug.LogWarning($"[Brush] {handType} could not find Drawing object on scene {scene.name}");
        }
    }

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

        Debug.Log($"[Brush] Start complete for {handType}: drawingObject={(drawingObject != null ? drawingObject.name : "NULL")}, mode={drawingMode}, desktop={isDesktopMode}");
    }

    private void Update()
    {
        // DesktopInputController can be created after this Brush starts, so re-check every frame.
        bool desktopModeNow = enableDesktopMode && DesktopInputController.Instance != null;
        if (desktopModeNow && !isDesktopMode)
        {
            isDesktopMode = true;
            if (!DrawingBlockedScenes.Contains(SceneManager.GetActiveScene().name))
            {
                drawingMode = DrawingMode.Draw;
            }
            Debug.Log($"[Brush] Desktop mode became available for {handType} brush.");
        }

        if (isDesktopMode)
        {
            HandleDesktopModeShortcuts();
        }

        // Handle input based on mode
        if (isDesktopMode && DesktopInputController.Instance != null)
        {
            isPainting = DesktopInputController.Instance.IsDrawingPressed();
            desktopBrushPosition = DesktopInputController.Instance.GetDrawingPosition();
            if (Time.frameCount % 30 == 0)
                Debug.Log($"[Brush] {handType}: desktop input={isPainting}, mode={drawingMode}, pos={desktopBrushPosition}, drawingObject={(drawingObject != null ? drawingObject.name : "NULL")}");
        }
        else
        {
            if (paintingInputAction != null)
            {
                isPainting = (float)paintingInputAction.action.ReadValue<float>() > 0.5f;
            }
        }

        // In desktop mode, ignore hand menu state and allow drawing directly
        bool canDraw = isDesktopMode || (!drawingHandMenuLeft.handMenuEnableState && !drawingHandMenuRight.handMenuEnableState);
        if (canDraw)
        {
            if (drawingMode == DrawingMode.Draw)
            {
                if (isPainting) 
                {
                    Debug.Log($"[Brush] {handType} calling Paint at {desktopBrushPosition}");
                    Paint();
                }
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
        if (drawingObject == null)
        {
            Debug.LogWarning($"[Brush] {handType} Paint() aborted: drawingObject is null");
            return;
        }

        Vector3 currentBrushPosition = isDesktopMode ? desktopBrushPosition : brushTip.position;

        if (currentLine == null)
        {
            /* Initialize the LineRenderer with the first position */
            index = 0;
            currentLine = new GameObject(name: $"Line_{GetTimestamp()}").AddComponent<LineRenderer>();
            Debug.Log($"[Brush] {handType} created new line object {currentLine.name} at {currentBrushPosition}");
            if (paintingMaterial != null)
            {
                currentLine.material = paintingMaterial;
            }
            else
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    currentLine.material = new Material(shader);
                    Debug.LogWarning($"[Brush] {handType} paintingMaterial missing, using fallback Sprites/Default material.");
                }
            }
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
                // Only warn once per missing VRDrawing setup instead of spamming every frame.
                if (Time.frameCount % 120 == 0)
                {
                    Debug.LogWarning("The drawingObject does not have a VRDrawing component attached.");
                }
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
                else if (Time.frameCount % 120 == 0)
                {
                    Debug.LogWarning("The drawingObject does not have a VRDrawing component attached.");
                }
            }
        }
    }

    private string GetTimestamp() => DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_") + ((int)(DateTime.Now.Millisecond)).ToString("D3");
}
