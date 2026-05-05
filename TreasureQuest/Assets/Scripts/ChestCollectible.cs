using UnityEngine;
using System.Collections;

public class ChestCollectible : MonoBehaviour
{
    public float     collectRadius = 4f;
    public AudioClip collectSound;

    private Transform player;
    private Animator  animator;
    private bool      collected = false;

    void Start()
    {
        player   = GameObject.FindGameObjectWithTag("Player").transform;
        animator = GetComponent<Animator>();

        if (animator != null) animator.speed = 0f;
    }

    void Update()
    {
        if (collected) return;

        if (Vector3.Distance(transform.position, player.position) < collectRadius)
        {
            collected = true;
            StartCoroutine(CollectSequence());
        }
    }

    IEnumerator CollectSequence()
    {
        GameManager.instance.AddChest();

        // Play sound immediately at chest world position — survives destroy
        if (collectSound != null)
            AudioSource.PlayClipAtPoint(collectSound, transform.position);

        // Play open animation then wait for it to finish
        if (animator != null)
        {
            animator.speed = 1f;
            yield return null;
            yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        Destroy(gameObject);
    }
}
