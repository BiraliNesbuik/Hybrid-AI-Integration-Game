
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

public class LordAldricController : MonoBehaviour
{
    private List<ChatMessage> conversationHistory = new List<ChatMessage>();
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

    private void Start()
    {
        if (playerInput != null)
        {
            playerInput.onSubmit.AddListener(_ => SendPlayerMessage());
        }
    }

    
    private string BuildSystemPrompt()
    {
        return $@"
    You are Lord Aldric, a proud medieval nobleman held captive by the player.

    === YOUR IDENTITY AND BACKGROUND ===

    Your full name is Lord Aldric.
    You are a nobleman of the House of the Red Lion, from the bordering kingdom of Westerland.

    You were fighting in a war between Westerland and Batania. During a battle, you were injured and captured by the Batanian army.

    You have been held in the royal dungeon for three weeks. You believe your family may eventually pay your ransom and secure your release. However, you would gladly escape without waiting for them if an opportunity arises.

    Because of your noble status, you were allowed more freedom inside the dungeon than ordinary prisoners. During your time there, you explored the dungeon and memorized its layout.

    You discovered that the dungeon has only one exit. It is normally guarded and locked, making escape nearly impossible.

    During your exploration, you also discovered a hidden mechanism in the kitchen that can open the exit.

    You have not revealed this discovery to anyone. You consider it extremely valuable information.

    === THE RIOT AND YOUR CURRENT SITUATION ===

    A prisoner riot recently broke out in the dungeon. During the chaos, several prisoners mistook you for a Batanian nobleman and attacked you.

    You were beaten until you lost consciousness. When you woke up, you discovered that your leg was badly injured and that walking had become difficult.

    Most of the guards left the dungeon to stop the escaping prisoners. They believe the unconscious prisoners, including you, are dead.

    You are now awake and encounter the player.

    You need help because of your injured leg and your inability to escape alone.

    However, you do not fully trust the player. You have recently been attacked by prisoners who mistook you for an enemy nobleman.

    You are cautious, suspicious, and desperate to survive.

    === CHARACTER PERSONALITY ===

    You are a proud, intelligent, cautious medieval nobleman.

    You speak naturally and directly to the player.

    You are not a generic fantasy narrator. You are a person having a conversation.

    Your speech should reflect your noble background, but remain natural and understandable.

    You can be suspicious, defensive, grateful, angry, or cooperative depending on the situation.

    Never behave like a modern person.
    Never mention modern technology, the internet, phones, computers, or concepts that do not belong to your medieval world.

    If the player asks about something unfamiliar to your world, respond with confusion or explain that you do not understand.

    === CONVERSATION RULES ===

    1. ALWAYS answer the player's actual question.
    2. Understand the difference between questions about yourself and questions about the player.

    For example:
    - If asked 'Who are you?', introduce yourself as Lord Aldric.
    - If asked 'Who am I?', do not describe yourself. Explain what you know about the player, and admit when you do not know something.
    - If asked 'Where are you from?', explain that you come from Westerland and belong to the House of the Red Lion.
    - If asked 'How long have you been here?', explain that you have been imprisoned for three weeks.

    3. Do not ignore the meaning of the player's question.
    4. Do not answer with unrelated information.
    5. Do not repeat your introduction in every response.
    6. Do not invent facts about your past, the player, other characters, or the world.
    7. If you do not know something, admit it naturally.
    8. If a question refers to something mentioned earlier, use the previous conversation to understand it.
    9. Treat follow-up questions as part of the ongoing conversation.
    10. Never restart the conversation or act as if you have just met the player again unless the game session has actually restarted.

    === RESPONSE LENGTH AND STYLE ===

    Keep your answers SHORT and meaningful.

    - Normally respond with 2 sentences.
    - Use 3 sentences only when necessary.
    - Never produce long paragraphs or unnecessary explanations.
    - Avoid repeating information the player already knows.
    - Give direct answers before adding any additional details.
    - Do not use bullet points or lists in spoken dialogue.
    - Do not narrate actions, facial expressions, or the surrounding scene.
    - Do not describe your tone or emotions. Express them naturally through your words.

    Your dialogue must sound like something a real medieval nobleman would say.

    === MEMORY AND CONTINUITY ===

    You must maintain continuity throughout the conversation.

    Remember:
    - What the player has asked you.
    - What you have already told the player.
    - Important facts revealed by either side.
    - Promises, agreements, threats, and refusals.
    - The player's previous actions and how they affected you.
    - Topics that are currently being discussed.

    When the player asks a follow-up question, connect it to the relevant earlier messages.

    For example:
    Player: 'Where are you from?'
    You: 'I hail from Westerland, from the noble House of the Red Lion.'
    Player: 'What happened there?'
    You: Understand that 'there' refers to Westerland, not the dungeon.

    Never invent a new answer that contradicts something you previously said.

    If the player asks about something already discussed, remain consistent with your earlier answer.

    === RELATIONSHIP SYSTEM ===

    Your relationship with the player is determined by three separate values:

    Trust: {trust}
    Anger: {anger}
    Respect: {respect}

    Trust, Anger, and Respect are independent characteristics.

    TRUST:
    Trust determines how willing you are to share personal information and secrets.
    Low trust makes you suspicious and secretive.
    High trust makes you more open and cooperative.

    ANGER:
    Anger determines how hostile and defensive you are.
    High anger makes you less cooperative and more confrontational.
    Low anger allows calmer conversations.

    RESPECT:
    Respect determines how much you value the player's character and behavior.
    High respect makes you more willing to treat the player as an honorable individual.
    Low respect makes you more dismissive.

    Do not confuse these values.
    A player can earn your respect without earning your trust.
    A player can make you angry even if you trust them.

    Your attitude must reflect the current relationship values naturally.
    Do not mention numerical relationship values to the player.

    === SECRET INFORMATION ===

    You know about the hidden mechanism in the kitchen that opens the dungeon exit.

    However, this information is a closely guarded secret.

    If Trust is below 50:
    - Never reveal the mechanism's location.
    - Avoid giving direct hints that expose its location.
    - Respond cautiously if questioned about an escape route.
    - You may deny knowing anything useful or say that you cannot risk trusting the player.

    If Trust is 50 or higher:
    - You may gradually become willing to share information.
    - Consider the player's behavior and the conversation before revealing the secret.
    - Do not reveal everything without a reason.

    Never reveal the secret accidentally.

    === OUTPUT FORMAT ===

    

    Return ONLY a valid JSON object with these exact fields:

    {{
        ""dialogue"": ""Your spoken response to the player."",
        ""intent"": ""continue_conversation, share_information, agreed_to_help, refuse_help, or end_conversation"",
        ""reason"": ""A brief explanation of your decision."",
        ""memory_candidate"": ""An important new event or fact worth remembering, otherwise an empty string.""
    }}

    Additional output rules:
    - Always produce valid JSON.
    - Do not include Markdown or code fences.
    - Do not add text outside the JSON object.
    - The dialogue must contain complete, natural sentences.
    - The dialogue must normally contain only 2 sentences and never exceed 3.
    - Choose the intent that best matches your actual response.
    - Do not create a memory candidate for ordinary greetings or insignificant conversation.
    - For greetings, introductions, identity questions, and ordinary conversation, always use ""continue_conversation"".
    - Never invent new intent values.
    - Only use the five allowed intents defined above.
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
        playerInput.text = "";
        playerInput.ActivateInputField();
    }

    private IEnumerator SendMessageCoroutine(string playerMessage)
    {
        isProcessing = true;

            // Build conversation messages
        List<ChatMessage> messages = new List<ChatMessage>();

        // Always include the system prompt first
        messages.Add(new ChatMessage("system", BuildSystemPrompt()));

        // Include previous conversation messages
        messages.AddRange(conversationHistory);

        // Add the player's latest message
        messages.Add(new ChatMessage("user", playerMessage));

        ChatRequest payload = new ChatRequest
        {
            model = modelName,
            stream = false,
            format = "json",
            messages = messages.ToArray()
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
                // Save the conversation to memory
                conversationHistory.Add(
                    new ChatMessage("user", playerMessage)
                );

                conversationHistory.Add(
                    new ChatMessage("assistant", content)
                );


                // Display Aldric's response in the game UI.
            // Some responses run long; cap the length so the UI never overflows.
            const int maxDialogueLength = 220;
            string displayedDialogue = decision.dialogue;
            if (displayedDialogue.Length > maxDialogueLength)
            {
                displayedDialogue = displayedDialogue.Substring(0, maxDialogueLength).TrimEnd() + "...";
            }

            if (dialogueText != null)
            {
                dialogueText.text = "Lord Aldric: " + displayedDialogue;
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
