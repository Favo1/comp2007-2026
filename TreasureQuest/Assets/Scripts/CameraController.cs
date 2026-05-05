using UnityEngine;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Manages the FPS camera and the third-person follow camera.
/// Press V (or the configured key) in-game to switch between them.
/// Attach to any persistent scene GameObject (e.g. GameManager or PlayerCapsule).
public class CameraController : MonoBehaviour
{
    [Header("Virtual Cameras")]
    [Tooltip("Cinemachine camera locked to PlayerCameraRoot (FPS view)")]
    public CinemachineCamera fpsCam;

    [Tooltip("Cinemachine camera that orbits behind the player (follow view)")]
    public CinemachineCamera followCam;

    [Header("Settings")]
    [Tooltip("Key that toggles between FPS and follow camera")]
    public Key switchKey = Key.V;

    // Priority values — the higher one wins the CinemachineBrain
    const int ACTIVE_PRIORITY   = 20;
    const int INACTIVE_PRIORITY = 10;

    bool _isFPS = true;

    void Start()
    {
        ApplyPriority();
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current[switchKey].wasPressedThisFrame)
            Toggle();
#else
        if (Input.GetKeyDown(KeyCode.V))
            Toggle();
#endif
    }

    void Toggle()
    {
        _isFPS = !_isFPS;
        ApplyPriority();
    }

    void ApplyPriority()
    {
        if (fpsCam   != null) fpsCam.Priority   = _isFPS ? ACTIVE_PRIORITY : INACTIVE_PRIORITY;
        if (followCam != null) followCam.Priority = _isFPS ? INACTIVE_PRIORITY : ACTIVE_PRIORITY;
    }

    /// Call from UI button if needed
    public void SwitchToFPS()    { _isFPS = true;  ApplyPriority(); }
    public void SwitchToFollow() { _isFPS = false; ApplyPriority(); }
}
