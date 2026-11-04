using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Companion speech lines, written by hand - no LLM involved here, by design.
public class CompanionDialogueController : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;

    [SerializeField] private float interactRange = 4f;
    [SerializeField] private float viewAngleThreshold = 35f;
    [SerializeField] private float lineDisplaySeconds = 4f;

    [TextArea]
    [SerializeField]
    private string[] dialogueLines =
    {
        "Stay close, I've got your back.",
        "This place gives me the creeps... but I've got you.",
        "If that lever needs pulling again, just say the word.",
        "We've come a long way together, haven't we?",
        "Careful where you step - this castle doesn't forgive mistakes.",
        "I won't let anything happen to you.",
        "Let's find a way out of here, together."
    };

    private int nextLineIndex;
    private bool dialogueOpen;

    private void Start()
    {
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerTransform == null || Keyboard.current == null)
        {
            return;
        }

        bool canInteract = !dialogueOpen && IsPlayerLookingAtMe();

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(canInteract);
        }

        if (canInteract && Keyboard.current.fKey.wasPressedThisFrame)
        {
            StartCoroutine(ShowNextLine());
        }
    }

    private bool IsPlayerLookingAtMe()
    {
        Vector3 toMe = transform.position - playerTransform.position;
        float distance = toMe.magnitude;

        if (distance > interactRange)
        {
            return false;
        }

        float angle = Vector3.Angle(playerTransform.forward, toMe);
        return angle <= viewAngleThreshold;
    }

    private IEnumerator ShowNextLine()
    {
        dialogueOpen = true;

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }

        if (dialogueLines.Length > 0 && dialogueText != null)
        {
            dialogueText.text = dialogueLines[nextLineIndex];
            nextLineIndex = (nextLineIndex + 1) % dialogueLines.Length;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        yield return new WaitForSeconds(lineDisplaySeconds);

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        dialogueOpen = false;
    }
}
