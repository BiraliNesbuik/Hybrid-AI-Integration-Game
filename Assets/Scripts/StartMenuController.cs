using UnityEngine;

public class StartMenuController : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject menuCamera;
    [SerializeField] private GameObject playerCamera;

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameplayMusic;

    private void Start()
    {
        Time.timeScale = 0f;

        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
        }

        if (menuCamera != null)
        {
            menuCamera.SetActive(true);
        }

        if (playerCamera != null)
        {
            playerCamera.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PlayMusic(menuMusic);
    }

    public void OnStartButtonClicked()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        if (menuCamera != null)
        {
            menuCamera.SetActive(false);
        }

        if (playerCamera != null)
        {
            playerCamera.SetActive(true);
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PlayMusic(gameplayMusic);
    }

    private void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null || musicSource.clip == clip)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }
}
