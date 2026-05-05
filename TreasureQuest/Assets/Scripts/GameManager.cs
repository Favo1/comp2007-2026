using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int totalChests = 5;
    private int chestsCollected = 0;

    public TextMeshProUGUI scoreText;
    public GameObject winPanel;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (scoreText != null)
            scoreText.text = "Chests: 0 / " + totalChests;
    }

    public void AddChest() 
    {
        chestsCollected++;
        scoreText.text = "Chests: " + chestsCollected + " / " + totalChests;
        if (chestsCollected >= totalChests)
            WinGame();
    }

    void WinGame()
    {
        winPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        if (instance != null)
        {
            var fpc = FindObjectOfType<StarterAssets.FirstPersonController>();
            if (fpc != null && fpc.pausePanel != null)
                fpc.pausePanel.SetActive(false);
        }
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene(0);
    }
}
