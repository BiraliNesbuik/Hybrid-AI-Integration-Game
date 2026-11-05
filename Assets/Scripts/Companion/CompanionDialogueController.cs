using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CompanionDialogueController : MonoBehaviour
{
    [System.Serializable]
    public class DialogueQA
    {
        public string question;
        [TextArea] public string answer;
    }

    [SerializeField] private Transform playerTransform;
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Questions (one button per entry, in order)")]
    [SerializeField] private Button[] optionButtons;
    [SerializeField] private TMP_Text[] optionButtonLabels;
    [SerializeField] private Button closeButton;

    [SerializeField] private float interactRange = 4f;
    [SerializeField] private float viewAngleThreshold = 35f;

    [TextArea]
    [SerializeField]
    private string greetingLine = "Hey there, my name is Bindik. You must be a prisoner.";

    [SerializeField]
    private DialogueQA[] questions =
    {
        new DialogueQA { question = "Where are we?", answer = "" },
        new DialogueQA { question = "What are you?", answer = "" },
        new DialogueQA { question = "What are we doing here?", answer = "" }
    };

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

        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i; // capture for the closure below
            if (optionButtons[i] != null)
            {
                optionButtons[i].onClick.AddListener(() => ShowAnswer(index));
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseDialogue);
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
            OpenDialogue();
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

    private void OpenDialogue()
    {
        dialogueOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        if (dialogueText != null)
        {
            dialogueText.text = greetingLine;
        }

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null)
            {
                continue;
            }

            bool hasQuestion = i < questions.Length;
            optionButtons[i].gameObject.SetActive(hasQuestion);

            if (hasQuestion && i < optionButtonLabels.Length && optionButtonLabels[i] != null)
            {
                optionButtonLabels[i].text = questions[i].question;
            }
        }
    }

    private void ShowAnswer(int index)
    {
        if (index < 0 || index >= questions.Length || dialogueText == null)
        {
            return;
        }

        dialogueText.text = questions[index].answer;
    }

    private void CloseDialogue()
    {
        dialogueOpen = false;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
