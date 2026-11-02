using UnityEngine;

public class StartMenuController : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject menuCamera;
    [SerializeField] private GameObject playerCamera;

    private void Start()
    {
        // Runs after every script's Awake(), so this reliably overrides
        // PlayerController's Awake()-time cursor lock regardless of script order.
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
    }
}
