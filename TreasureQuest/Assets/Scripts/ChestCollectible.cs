using UnityEngine;
using System.Collections;

public class ChestCollectible : MonoBehaviour
{
    public float collectRadius = 4f;

    private Transform  player;
    private Animator   animator;
    private AudioSource audioSource;
    private bool       collected = false;

    void Start()
    {
        player      = GameObject.FindGameObjectWithTag("Player").transform;
        animator    = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();

        // Pause the animator at frame 0 — play only when collected
        if (animator != null) animator.speed = 0f;
    }

    void Update()
    {
        if (collected) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist < collectRadius)
        {
            collected = true;
            StartCoroutine(CollectSequence());
        }
    }

    IEnumerator CollectSequence()
    {
        // Update score immediately so the UI responds right away
        GameManager.instance.AddChest();

        // Play the open animation
        if (animator != null)
        {
            animator.speed = 1f;
            yield return null; // one frame for state info to refresh
            float clipLength = animator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(clipLength);
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        // Play collection sound detached so it survives the destroy
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.transform.SetParent(null);
            audioSource.Play();
            Destroy(audioSource.gameObject, audioSource.clip.length);
        }

        Destroy(gameObject);
    }
}
