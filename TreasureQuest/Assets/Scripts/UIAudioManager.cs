using UnityEngine;

/// Attach one instance per scene. Buttons call PlaySelect() via OnClick.
/// Uses PlayClipAtPoint so no persistent AudioSource is needed — works
/// reliably across scene loads without singleton or DontDestroyOnLoad.
public class UIAudioManager : MonoBehaviour
{
    public AudioClip selectSound;

    public void PlaySelect()
    {
        if (selectSound == null) return;
        AudioSource.PlayClipAtPoint(selectSound, Camera.main != null
            ? Camera.main.transform.position
            : Vector3.zero);
    }
}
