using UnityEngine;

public class UIAudioManager : MonoBehaviour
{
    public static UIAudioManager instance;

    public AudioClip selectSound;
    private AudioSource audioSource;

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void PlaySelect()
    {
        if (selectSound != null)
            audioSource.PlayOneShot(selectSound);
    }
}
