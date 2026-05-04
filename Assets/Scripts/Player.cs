using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Management;

public class Player : MonoBehaviour
{
    //ROLE: The highest level component that represents the player in the scene.
    //      It holds references to the camera, controllers, and avatar, and initializes the avatar with these references.

    public static Player Instance { get; private set; }

    [SerializeField, Tooltip("The Object of XR Device Simulator")]
    private GameObject xrDeviceSimulatorObject;

    [SerializeField, Tooltip("The Transform of the camera")]
    private Transform vrCameraTransform;

    [SerializeField, Tooltip("The Transform of the left controller")]
    private Transform leftControllerTransform;

    [SerializeField, Tooltip("The Transform of the right controller")]
    private Transform rightControllerTransform;

    [SerializeField, Tooltip("The Avatar component")]
    private Avatar avatar;

    [SerializeField, Tooltip("The Character Controller component")]
    private CharacterController characterController;

    [SerializeField, Tooltip("The Left Brush reference")]
    private Brush leftBrush;

    [SerializeField, Tooltip("The Right Brush reference")]
    private Brush rightBrush;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[Player] Duplicated Player object has been destroyed.");
            this.gameObject.SetActive(false);
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(this.gameObject);

        if (characterController == null) characterController = GetComponent<CharacterController>();

        GameObject drawingObject = GameObject.FindGameObjectWithTag("Drawing");
        if (leftBrush.drawingObject != null && rightBrush.drawingObject != null && drawingObject != null)
        {
            leftBrush.drawingObject = drawingObject;
            rightBrush.drawingObject = drawingObject;
        }
    }


    private void Start()
    {
        if (avatar != null) avatar.Initialize(vrCameraTransform, avatar.transform, leftControllerTransform, rightControllerTransform);
        else Debug.LogWarning("Avatar component is not assigned in the Player script.");

        bool isRealHeadset = false;

        if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
        {
            var activeLoader = XRGeneralSettings.Instance.Manager.activeLoader;
            if (activeLoader != null)
            {
                Debug.Log($"[XR] Active Loader: {activeLoader.name}");
                if (!activeLoader.name.ToLower().Contains("mock")) isRealHeadset = true;
            }
            else Debug.LogWarning("[XR] No active XR Loader.");
        }

        Debug.Log($"[XR] Real VR device found: {isRealHeadset}");

        if (isRealHeadset)
        {
            // VR Mode - ensure XR simulator object is disabled
            if (xrDeviceSimulatorObject != null)
            {
                xrDeviceSimulatorObject.SetActive(false);
                Debug.Log("[XR] XR Simulator disabled for VR mode (real headset detected).");
            }
        }
        else
        {
            // Desktop Mode - make sure XR runtime is stopped and deinitialized to avoid stereo rendering
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                try
                {
                    XRGeneralSettings.Instance.Manager.StopSubsystems();
                    XRGeneralSettings.Instance.Manager.DeinitializeLoader();
                    Debug.Log("[XR] XR subsystems stopped and loader deinitialized for desktop mode.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[XR] Failed to stop XR subsystems cleanly: {ex.Message}");
                }
            }

            if (xrDeviceSimulatorObject != null)
            {
                xrDeviceSimulatorObject.SetActive(false);
                Debug.Log("[XR] XR Simulator disabled for desktop mode.");
            }

            SetupDesktopMode();
        }
    }

    private void SetupDesktopMode()
    {
        DisableTrackedPoseDriversForDesktop();

        // Check if DesktopInputController already exists
        if (DesktopInputController.Instance != null)
        {
            Debug.Log("[Player] DesktopInputController already exists in scene.");
            return;
        }

        // Create DesktopInputController as a child of the Player
        GameObject desktopControllerObject = new GameObject("DesktopInputController");
        desktopControllerObject.transform.SetParent(transform);
        desktopControllerObject.transform.localPosition = Vector3.zero;

        DesktopInputController desktopController = desktopControllerObject.AddComponent<DesktopInputController>();

        // Assign references using public methods
        if (vrCameraTransform != null)
        {
            desktopController.SetCameraTransform(vrCameraTransform);
        }
        else
        {
            Debug.LogWarning("[Player] VR Camera Transform not assigned! Desktop camera control may not work.");
        }

        if (characterController != null)
        {
            desktopController.SetCharacterController(characterController);
        }
        else
        {
            Debug.LogWarning("[Player] CharacterController not found on Player! Desktop movement may not work.");
        }

        // Ensure the desktop controller performs its initialization now that references are assigned
        desktopController.InitializeDesktopController();

        // Ensure desktop UI interaction works in all scenes via persistent Player object.
        DesktopUIInteractionManager uiManager = GetComponent<DesktopUIInteractionManager>();
        if (uiManager == null)
        {
            uiManager = gameObject.AddComponent<DesktopUIInteractionManager>();
            Debug.Log("[Player] DesktopUIInteractionManager added to persistent Player.");
        }

        Debug.Log("[Player] Desktop mode initialized successfully.");
    }

    private void DisableTrackedPoseDriversForDesktop()
    {
        if (vrCameraTransform == null)
        {
            Debug.LogWarning("[Player] vrCameraTransform is null; cannot disable tracked pose drivers.");
            return;
        }

        int disabledCount = 0;
        Component[] components = vrCameraTransform.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null) continue;

            // XR camera pose components can overwrite mouse look each frame.
            string typeName = component.GetType().Name;
            if (typeName.Contains("TrackedPoseDriver") || typeName.Contains("PoseDriver"))
            {
                if (component is Behaviour behaviour && behaviour.enabled)
                {
                    behaviour.enabled = false;
                    disabledCount++;
                }
            }
        }

        Debug.Log($"[Player] Desktop mode: disabled {disabledCount} tracked pose component(s) under camera.");
    }


    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "SetupScene") return;

        GameObject spawnPoint = GameObject.FindWithTag("Respawn");
        if (spawnPoint == null) spawnPoint = GameObject.FindWithTag("Anchor");

        if (spawnPoint != null)
        {
            if (characterController != null) characterController.enabled = false;
            transform.SetPositionAndRotation(spawnPoint.transform.position, spawnPoint.transform.rotation);
            Physics.SyncTransforms();
            if (characterController != null) characterController.enabled = true;

            Debug.Log($"[Player] Teleported here: {spawnPoint.name} ({spawnPoint.transform.position})");
        }
        else Debug.LogWarning("[Player] Spawn Point with 'Respawn' or 'Anchor' tag not found!");


        GameObject newDrawingObject = GameObject.FindGameObjectWithTag("Drawing");
        if (newDrawingObject != null)
        {
            if (leftBrush != null) leftBrush.drawingObject = newDrawingObject;
            if (rightBrush != null) rightBrush.drawingObject = newDrawingObject;
            Debug.Log($"[Player] Drawing object successfully updated in scene '{scene.name}'.");
        }
        else
        {
            if (!Brush.DrawingBlockedScenes.Contains(scene.name))
            {
                Debug.LogWarning($"[Player] No GameObject with tag 'Drawing' found in the scene. Please add one to enable drawing functionality.");
            }
        }
    }
}
