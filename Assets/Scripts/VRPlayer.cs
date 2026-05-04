using UnityEngine;
using UnityEngine.XR.Management;

public class VRPlayer : MonoBehaviour
{ 
    void Start()
    {
        // Detect if we're in VR mode or desktop mode
        bool isRealHeadset = DetectVRHeadset();
        
        if (!isRealHeadset)
        {
            Debug.Log("[VRPlayer] No VR headset detected. Desktop mode will be enabled when Player initializes.");
            InitializeDesktopMode();
        }
        else
        {
            Debug.Log("[VRPlayer] VR headset detected. Using VR mode.");
        }
        
        Debug.Log("VRPlayer: Initialized and set to persist across scenes.");
    }

    private bool DetectVRHeadset()
    {
        if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
        {
            var activeLoader = XRGeneralSettings.Instance.Manager.activeLoader;
            if (activeLoader != null)
            {
                Debug.Log($"[VRPlayer] Active XR Loader: {activeLoader.name}");
                // Return true if it's NOT a mock loader (i.e., it's a real headset)
                return !activeLoader.name.ToLower().Contains("mock");
            }
            else
            {
                Debug.LogWarning("[VRPlayer] No active XR Loader found.");
                return false;
            }
        }

        Debug.LogWarning("[VRPlayer] XRGeneralSettings not available. Assuming desktop mode.");
        return false;
    }

    private void InitializeDesktopMode()
    {
        // Desktop mode will be initialized by Player.cs when it's loaded
        // This is just a heads up in the setup scene
        Debug.Log("[VRPlayer] Desktop mode will be initialized in the next scene.");
    }
}

