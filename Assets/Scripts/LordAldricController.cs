
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;

public class LordAldricController : MonoBehaviour
{
    [Header("Ollama Settings")]
    [SerializeField] private string modelName = "lord-aldric-en";
    [SerializeField] private string ollamaUrl =
        "http://localhost:11434/api/chat";

    [Header("Relationship State")]
    [Range(0, 100)] public int trust = 30;
    [Range(0, 100)] public int anger = 0;
    [Range(0, 100)] public int respect = 30;

    [Header("Dialogue UI")]
    public TMP_Text dialogueText;
    public TMP_InputField playerInput;
    public Button sendButton;
    public TMP_Text relationshipText;

    [Header("Game State")]
    [SerializeField] private UnityEvent onDoorUnlocked;
    private bool doorUnlocked;

    [Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;

        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    [Serializable]
    private class ChatRequest
    {
        public string model;
        public ChatMessage[] messages;
        public bool stream;
        public string format;
    }

    [Serializable]
    private class OllamaMessage
    {
        public string content;
    }

    [Serializable]
    private class OllamaResponse
    {
        public OllamaMessage message;
    }

    [Serializable]
    private class NpcDecision
    {
        public string dialogue;
        public string intent;
        public string reason;
        public string memory_candidate;
    }

    private bool isProcessing;

    private string BuildSystemPrompt()
    {
        return $@"
You are Lord Aldric, a proud medieval nobleman held captive by the player.

Speak naturally and directly to the player.
Stay in character and use medieval language.
Do not narrate the scene or describe your tone.
Never output placeholder text such as 'Lord Aldric's response'.
The secret passage exists, but you protect its location when trust is low.
Low trust makes you reluctant to reveal secrets.
High anger makes you hostile and defensive.
Respect and trust are different.

Current relationship:
Trust: {trust}
Anger: {anger}
Respect: {respect}

Return ONLY a valid JSON object with these fields:
{{
  ""dialogue"": ""Your spoken response"",
  ""intent"": ""continue_conversation, share_information, agreed_to_help, refuse_help, or end_conversation"",
  ""reason"": ""Brief explanation"",
  ""memory_candidate"": ""Important event or empty string""
}}

If trust is below 50, do not reveal the secret passage location.
Use complete, natural sentences in the dialogue field.
";
    }

    // Select this component's context menu in the Inspector to test.
    [ContextMenu("Test Ollama Connection")]
    private void TestOllamaConnection()
    {
        SendMessageToAldric("Hello, Lord Aldric.");
    }

    public void SendMessageToAldric(string playerMessage)
    {
        if (isProcessing)
        {
            Debug.LogWarning("Aldric is already processing a message.");
            return;
        }

        if (string.IsNullOrWhiteSpace(playerMessage))
        {
            Debug.LogWarning("Player message cannot be empty.");
            return;
        }

        StartCoroutine(SendMessageCoroutine(playerMessage));
    }

    public void SendPlayerMessage()
    {
        if (playerInput == null)
        {
            Debug.LogError("PlayerInput is not assigned.");
            return;
        }

        string message = playerInput.text.Trim();

        if (string.IsNullOrEmpty(message))
            return;

        SendMessageToAldric(message);
    }

    private IEnumerator SendMessageCoroutine(string playerMessage)
    {
        isProcessing = true;

        ChatRequest payload = new ChatRequest
        {
            model = modelName,
            stream = false,
            format = "json",
            messages = new[]
            {
                new ChatMessage("system", BuildSystemPrompt()),
                new ChatMessage("user", playerMessage)
            }
        };

        string json = JsonUtility.ToJson(payload);
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request =
               new UnityWebRequest(ollamaUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 180;

            Debug.Log("Sending message to Lord Aldric...");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"Ollama request failed: {request.error}\n" +
                    $"Response: {request.downloadHandler.text}"
                );

                isProcessing = false;
                yield break;
            }

            try
            {
                OllamaResponse ollamaResponse =
                    JsonUtility.FromJson<OllamaResponse>(
                        request.downloadHandler.text
                    );

                if (ollamaResponse == null ||
                    ollamaResponse.message == null ||
                    string.IsNullOrWhiteSpace(
                        ollamaResponse.message.content))
                {
                    throw new Exception("Ollama returned an empty response.");
                }

                
                string content = ollamaResponse.message.content;

                NpcDecision decision =
                    JsonUtility.FromJson<NpcDecision>(content);

                if (decision == null ||
                    string.IsNullOrWhiteSpace(decision.dialogue))
                {
                    throw new Exception(
                        "NPC response is missing dialogue. Raw content: " + content
                    );
                }

                // Some model responses omit optional decision fields.
                // Use a safe default instead of crashing.
                if (string.IsNullOrWhiteSpace(decision.intent))
                {
                    decision.intent = "continue_conversation";
                }

                if (string.IsNullOrWhiteSpace(decision.reason))
                {
                    decision.reason = "No reason provided.";
                }

                if (string.IsNullOrWhiteSpace(decision.memory_candidate))
                {
                    decision.memory_candidate = "";
                }


                string[] allowedIntents =
                {
                    "continue_conversation",
                    "share_information",
                    "agreed_to_help",
                    "refuse_help",
                    "end_conversation"
                };

                if (Array.IndexOf(allowedIntents, decision.intent) < 0)
                {
                    throw new Exception(
                        $"Unknown NPC intent: {decision.intent}"
                    );
                }

                // Enforce game rules independently of the LLM.
                if (anger >= 85)
                {
                    decision.intent = "end_conversation";
                }
                else if (
                    (decision.intent == "share_information" ||
                     decision.intent == "agreed_to_help") &&
                    (trust < 50 || anger >= 60))
                {
                    decision.intent = "refuse_help";
                }

                // Display Aldric's response in the game UI.
            if (dialogueText != null)
            {
                dialogueText.text = "Lord Aldric: " + decision.dialogue;
            }
            else
            {
                Debug.LogWarning("DialogueText is not assigned in the Inspector.");
            }

            if (relationshipText != null)
            {
                relationshipText.text = $"Trust: {trust} | Anger: {anger} | Respect: {respect}";
            }

            bool shouldUnlockDoor = decision.intent == "agreed_to_help" || decision.intent == "share_information";

            if (shouldUnlockDoor && !doorUnlocked)
            {
                doorUnlocked = true;
                onDoorUnlocked?.Invoke();
            }

            // Keep the response details in the Console for debugging.
            Debug.Log($"Aldric: {decision.dialogue}");
            Debug.Log($"Intent: {decision.intent}");
            Debug.Log($"Reason: {decision.reason}");
            Debug.Log(
                $"Trust: {trust} | Anger: {anger} | Respect: {respect}"
            );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Could not parse NPC response: {exception.Message}\n" +
                    $"Raw response: {ollamaResponseText(request)}"
                );
            }
        }

        isProcessing = false;
    }

    private string ollamaResponseText(UnityWebRequest request)
    {
        return request.downloadHandler != null
            ? request.downloadHandler.text
            : "(no response body)";
    }
}
