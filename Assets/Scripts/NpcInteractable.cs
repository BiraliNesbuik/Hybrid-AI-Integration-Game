using UnityEngine;
using UnityEngine.InputSystem;

public class NpcInteractable : MonoBehaviour
{
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private GameObject chatPanel;

    private bool playerInRange;
    private bool chatOpen;

    private void Start()
    {
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }

        if (chatPanel != null)
        {
            chatPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!playerInRange || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (chatOpen)
            {
                CloseChat();
            }
            else
            {
                OpenChat();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerInRange = true;

        if (!chatOpen && interactPrompt != null)
        {
            interactPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerInRange = false;

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }

        if (chatOpen)
        {
            CloseChat();
        }
    }

    private void OpenChat()
    {
        chatOpen = true;

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }

        if (chatPanel != null)
        {
            chatPanel.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseChat()
    {
        chatOpen = false;

        if (chatPanel != null)
        {
            chatPanel.SetActive(false);
        }

        if (playerInRange && interactPrompt != null)
        {
            interactPrompt.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
