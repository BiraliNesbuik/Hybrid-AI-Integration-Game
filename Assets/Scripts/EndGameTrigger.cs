using UnityEngine;

public class EndGameTrigger : MonoBehaviour
{
    [SerializeField] private GameObject endScreenPanel;

    private bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player"))
        {
            return;
        }

        triggered = true;

        if (endScreenPanel != null)
        {
            endScreenPanel.SetActive(true);
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
