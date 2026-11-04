using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Lord Aldric NPC: Ollama (Llama 3.2) for dialogue, game rules for decisions.
/// </summary>
public class LordAldricController : MonoBehaviour
{
    // -----------------------------
    // INSPECTOR SETTINGS
    // -----------------------------

    [Header("Ollama Settings")]
    [SerializeField] private string modelName = "lord-aldric-en";
    [SerializeField] private string ollamaUrl = "http://localhost:11434/api/chat";
    [SerializeField] private int requestTimeoutSeconds = 120;
    [SerializeField] private int numPredict = 400;   // max tokens per reply
    [SerializeField] private int numCtx = 8192;      // context window (lower to 4096 if memory is tight)
    [SerializeField] private float temperature = 0.5f;
    [SerializeField] private int maxAttempts = 4;    // retries for bad / cut-off output

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

    // -----------------------------
    // JSON DATA CLASSES
    // -----------------------------

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
    private class OllamaOptions
    {
        public float temperature;
        public int num_predict;
        public int num_ctx;
    }

    [Serializable]
    private class ChatRequest
    {
        public string model;
        public ChatMessage[] messages;
        public bool stream;
        public string format;
        public OllamaOptions options;
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
        public string done_reason;
    }

    [Serializable]
    private class NpcDecision
    {
        public string dialogue;
        public string intent;
        public string reason;
        public string memory_candidate;
        public string player_attitude;
    }

    // -----------------------------
    // CONSTANTS
    // -----------------------------

    private static readonly string[] AllowedIntents =
    {
        "continue_conversation",
        "share_information",
        "agreed_to_help",
        "refuse_help",
        "end_conversation"
    };

    private static readonly string[] SentenceEndings =
    {
        ".", "!", "?", "\u2026", ".\"", "!\"", "?\"", ".\u201D", "!\u201D", "?\u201D"
    };

    private static readonly string[] NarratorPhrases =
    {
        "speaks in", "says in", "his voice", "in a dignified tone",
        "with a hint of", "lord aldric speaks", "lord aldric says",
        "aldric speaks", "aldric says", "aldric replies", "aldric responds"
    };

    private static readonly Regex FirstOrSecondPerson = new Regex(
        @"\b(i|i'm|i've|i'll|i'd|my|me|mine|you|your|you're|we|us|our)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // The LLM classifies the player's message into one of these every turn;
    // the relationship numbers move based on that classification instead of
    // a fixed keyword list, so natural phrasing is understood correctly.
    private static readonly string[] AllowedAttitudes =
    {
        "threatening", "insulting", "apologetic",
        "helpful", "reassuring", "complimentary", "neutral"
    };

    private static readonly string[] ObviousApologyPhrases =
    {
        "i apologize", "i'm sorry", "i am sorry", "my apologies", "forgive me", "my bad"
    };

    /// <summary>
    /// Safety net: the LLM has occasionally classified a clear apology as
    /// "insulting"/"threatening" (likely still reacting to the player's
    /// earlier tone), which would make anger rise instead of fall right when
    /// the player tries to make up. A message containing an unmistakable
    /// apology phrase is treated as apologetic no matter what the LLM said.
    /// </summary>
    private static string OverrideAttitudeIfObviousApology(string playerMessage, string attitude)
    {
        if (attitude != "insulting" && attitude != "threatening")
            return attitude;

        string lowered = playerMessage.ToLowerInvariant();

        foreach (string phrase in ObviousApologyPhrases)
        {
            if (lowered.Contains(phrase))
                return "apologetic";
        }

        return attitude;
    }

    // -----------------------------
    // RUNTIME STATE
    // -----------------------------

    private readonly List<ChatMessage> conversationHistory = new List<ChatMessage>();
    private readonly List<string> memories = new List<string>();

    private bool isProcessing;
    private bool conversationEnded;
    private bool doorUnlocked;

    // -----------------------------
    // UNITY LIFECYCLE
    // -----------------------------

    private void Start()
    {
        if (dialogueText == null || playerInput == null)
        {
            Debug.LogError(
                "LordAldricController: assign Dialogue Text and Player Input " +
                "in the Inspector.");
        }

        // Button and Enter key are wired in code.
        if (sendButton != null)
            sendButton.onClick.AddListener(SendPlayerMessage);

        if (playerInput != null)
            playerInput.onSubmit.AddListener(OnInputSubmit);

        RefreshRelationshipUi();
    }

    private void OnDestroy()
    {
        if (sendButton != null)
            sendButton.onClick.RemoveListener(SendPlayerMessage);

        if (playerInput != null)
            playerInput.onSubmit.RemoveListener(OnInputSubmit);
    }

    private void OnInputSubmit(string _)
    {
        SendPlayerMessage();
    }

    // -----------------------------
    // PUBLIC ENTRY POINTS
    // -----------------------------

    [ContextMenu("Test Ollama Connection")]
    private void TestOllamaConnection()
    {
        SendMessageToAldric("Hello, Lord Aldric.");
    }

    // TEMPORARY DEBUG TOOL: bypasses Ollama/LLM entirely and fires the door
    // event directly, to check whether the door/lever wiring itself works
    // independently of whether the LLM agrees to cooperate. Remove before
    // final submission.
    [ContextMenu("DEBUG: Force Unlock Door")]
    private void DebugForceUnlockDoor()
    {
        if (doorUnlocked)
        {
            Debug.Log("DEBUG: Door was already unlocked.");
            return;
        }

        doorUnlocked = true;
        Debug.Log("DEBUG: Forcing onDoorUnlocked now (no LLM involved).");
        onDoorUnlocked?.Invoke();
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

        if (SendMessageToAldric(message))
            playerInput.text = "";
    }

    /// <summary>Returns true if the message was accepted.</summary>
    public bool SendMessageToAldric(string playerMessage)
    {
        if (conversationEnded)
        {
            Debug.Log("The conversation has ended.");
            return false;
        }

        if (isProcessing)
        {
            Debug.LogWarning("Aldric is already processing a message.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(playerMessage))
            return false;

        StartCoroutine(ProcessMessage(playerMessage));
        return true;
    }

    // -----------------------------
    // MAIN FLOW
    // -----------------------------

    private IEnumerator ProcessMessage(string playerMessage)
    {
        isProcessing = true;
        SetInputEnabled(false);

        Debug.Log($"Player: {playerMessage}");

        string previousText = dialogueText != null ? dialogueText.text : "";
        SetDialogue("Lord Aldric: ...");

        NpcDecision result = null;
        string error = null;

        yield return StartCoroutine(GetValidResponse(
            playerMessage,
            decision => result = decision,
            message => error = message));

        if (result == null)
        {
            // Nothing was changed yet, so there is nothing to roll back.
            Debug.LogError($"Could not get a valid response from Aldric: {error}");

            SetDialogue(string.IsNullOrEmpty(previousText)
                ? "(Aldric could not answer. Check the Console and make sure Ollama is running.)"
                : previousText);

            isProcessing = false;
            SetInputEnabled(true);
            yield break;
        }

        // Commit only after a valid response.
        string appliedAttitude = OverrideAttitudeIfObviousApology(playerMessage, result.player_attitude);
        UpdateRelationship(appliedAttitude);

        conversationHistory.Add(new ChatMessage("user", playerMessage));
        conversationHistory.Add(new ChatMessage("assistant", result.dialogue));

        while (conversationHistory.Count > 20)
            conversationHistory.RemoveAt(0);

        RememberEvent(result.memory_candidate);

        SetDialogue("Lord Aldric: " + result.dialogue);
        RefreshRelationshipUi();

        Debug.Log($"Aldric: {result.dialogue}");
        string attitudeNote = appliedAttitude == result.player_attitude
            ? appliedAttitude
            : $"{result.player_attitude} -> overridden to {appliedAttitude} (obvious apology)";
        Debug.Log($"Intent: {result.intent} | Reason: {result.reason} | Player attitude: {attitudeNote}");
        Debug.Log($"Trust: {trust} | Anger: {anger} | Respect: {respect}");

        // The door only unlocks when BOTH are true: the trust/anger numbers
        // allow cooperation, AND this specific message is actually the moment
        // Aldric agrees - not just trust silently crossing a number mid-chat.
        bool trustThresholdReached = trust >= 60 && anger < 60;
        bool intentAgreed = result.intent == "agreed_to_help" || result.intent == "share_information";

        if (!doorUnlocked && trustThresholdReached && intentAgreed)
        {
            doorUnlocked = true;
            Debug.Log("Door unlocked: trust threshold reached and Aldric agreed to help.");
            onDoorUnlocked?.Invoke();
        }

        if (result.intent == "end_conversation")
        {
            conversationEnded = true;
            SetDialogue(dialogueText.text + "\n(Aldric refuses to speak with you any longer.)");
        }

        isProcessing = false;
        SetInputEnabled(true);

        if (!conversationEnded && playerInput != null)
            playerInput.ActivateInputField();
    }

    // -----------------------------
    // UI HELPERS
    // -----------------------------

    private void SetDialogue(string text)
    {
        if (dialogueText != null)
            dialogueText.text = text;
        else
            Debug.LogWarning("DialogueText is not assigned in the Inspector.");
    }

    private void RefreshRelationshipUi()
    {
        if (relationshipText != null)
            relationshipText.text = $"Trust: {trust} | Anger: {anger} | Respect: {respect}";
    }

    private void SetInputEnabled(bool enabled)
    {
        bool canType = enabled && !conversationEnded;

        if (sendButton != null)
            sendButton.interactable = canType;

        if (playerInput != null)
            playerInput.interactable = canType;
    }

    // -----------------------------
    // RELATIONSHIP RULES
    // -----------------------------

    /// <summary>
    /// Moves trust/anger/respect based on the LLM's own classification of the
    /// player's last message (see player_attitude in the JSON schema), instead
    /// of matching a fixed keyword list against free-form text.
    /// </summary>
    private void UpdateRelationship(string attitude)
    {
        switch (attitude)
        {
            case "threatening":
                anger += 15;
                trust -= 10;
                respect -= 8;
                break;
            case "insulting":
                anger += 18;
                trust -= 10;
                respect -= 10;
                break;
            case "apologetic":
                anger -= 7;
                trust += 5;
                respect += 2;
                break;
            case "helpful":
                anger -= 5;
                trust += 10;
                respect += 8;
                break;
            case "reassuring":
                anger -= 2;
                trust += 5;
                break;
            case "complimentary":
                respect += 5;
                trust += 5;
                break;
            // "neutral" or anything unrecognized: no change.
        }

        trust = Mathf.Clamp(trust, 0, 100);
        anger = Mathf.Clamp(anger, 0, 100);
        respect = Mathf.Clamp(respect, 0, 100);
    }

    /// <summary>The game rules, not the LLM, decide whether cooperation is allowed.</summary>
    private string ValidateDecision(string intent)
    {
        if (anger >= 85)
            return "end_conversation";

        // Only the game rule above may end the conversation. The LLM choosing
        // "end_conversation" on its own, with no real reason, would lock the
        // player out of the dialogue permanently - so that choice is rejected.
        if (intent == "end_conversation")
            return "continue_conversation";

        bool cooperating = intent == "share_information" || intent == "agreed_to_help";

        if (cooperating && (anger >= 60 || trust < 50))
            return "refuse_help";

        return intent;
    }

    private void RememberEvent(string eventText)
    {
        if (string.IsNullOrWhiteSpace(eventText) || memories.Contains(eventText))
            return;

        memories.Add(eventText);

        while (memories.Count > 10)
            memories.RemoveAt(0);
    }

    // -----------------------------
    // PROMPT
    // -----------------------------

    private string BuildSystemPrompt(string requiredIntent)
    {
        string memoryText = memories.Count > 0
            ? "- " + string.Join("\n- ", memories)
            : "- Nothing important yet.";

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("You are Lord Aldric, a proud medieval nobleman. The player is standing in front of you right now and you are talking to them.");
        sb.AppendLine("You ARE Aldric: everything in \"dialogue\" is what you say out loud. Speak in the first person (\"I\", \"my\") and address the player as \"you\". Never describe Aldric from the outside and never narrate.");
        sb.AppendLine();
        sb.AppendLine("BACKGROUND:");
        sb.AppendLine("- You are a nobleman of the House of the Red Lion, from the kingdom of Westerland.");
        sb.AppendLine("- In the war between Westerland and Batania you were wounded and captured by the Batanian army. You have been held in the royal dungeon for three weeks. You hope your family will pay your ransom, but you would gladly escape sooner.");
        sb.AppendLine("- This dungeon is NOT yours. It belongs to your Batanian captors. Never call it \"my dungeon\" or imply you own or rule this place - you are a prisoner here, nothing more.");
        sb.AppendLine("- You are currently in Batania, the enemy kingdom, not in Westerland. You have not seen Westerland in a long time. The player is here with you in this Batanian dungeon, right now - never ask what they are doing \"in Westerland\" or talk as if either of you is there.");
        sb.AppendLine("- As a noble you had more freedom than other prisoners, so you explored the dungeon and memorized its layout. It has only one exit, which is normally guarded and locked.");
        sb.AppendLine("- You have a key hidden on your person - sewn into the lining of your sleeve - that unlocks this very cell door. You have told nobody. It is extremely valuable to you.");
        sb.AppendLine("- A prisoner riot has just broken out. Prisoners mistook you for a Batanian nobleman and beat you unconscious. Your leg is badly injured and walking is difficult.");
        sb.AppendLine("- Most guards left to stop the escaping prisoners and believe the unconscious prisoners are dead.");
        sb.AppendLine("- You have just woken up and met the player. You need help to escape, but you are suspicious because you were just attacked by people who mistook you for an enemy.");
        sb.AppendLine();
        sb.AppendLine("PERSONALITY:");
        sb.AppendLine("- Proud, intelligent, cautious, and desperate to survive.");
        sb.AppendLine("- You can be suspicious, defensive, grateful, angry, or cooperative, depending on how the player treats you.");
        sb.AppendLine("- Your speech is noble but natural and easy to understand.");
        sb.AppendLine("- You are a medieval person and know nothing about modern things. If the player mentions something unfamiliar, answer with confusion.");
        sb.AppendLine("- Ordinary politeness (greetings, titles like \"sir\" or \"good sir\", basic manners) is completely normal and expected in your world. Never react to it with confusion or suspicion - save your caution for things that actually warrant it.");
        sb.AppendLine();
        sb.AppendLine("CONVERSATION RULES:");
        sb.AppendLine("- Always answer what the player actually asked. \"Who are you?\" asks about you. \"Who am I?\" asks about the player: say only what you know from the conversation and admit what you do not know.");
        sb.AppendLine("- Follow-up questions refer to earlier messages. Stay consistent with what you already said.");
        sb.AppendLine("- Do not restart the conversation and do not repeat your introduction.");
        sb.AppendLine("- Do not invent facts about your past, the player, other characters, or the world. If you do not know something, say so.");
        sb.AppendLine("- You are the prisoner here, not the player. Never call the player a prisoner or accuse them of being untrustworthy without a reason from this conversation.");
        sb.AppendLine("- Never invent a surname, house, or title for the player. Use only the name they have actually given you, or call them \"stranger\" if they have not.");
        sb.AppendLine("- Keep it short: normally 2 sentences, never more than 3, all complete and ending with punctuation.");
        sb.AppendLine("- No lists, and no descriptions of actions, expressions, or tone.");
        sb.AppendLine();
        sb.AppendLine("CURRENT RELATIONSHIP (0-100, three independent values):");
        sb.AppendLine($"Trust: {trust} (how willing you are to share secrets and cooperate)");
        sb.AppendLine($"Anger: {anger} (how hostile and defensive you are)");
        sb.AppendLine($"Respect: {respect} (how honorable you consider the player)");
        sb.AppendLine("The player can earn your respect without earning your trust, and can anger you even if you trust them. Never say these numbers aloud.");
        sb.AppendLine("IMPORTANT: if Trust is 50 or higher, Anger is below 60, and the player has sincerely and directly asked for your cooperation (not just small talk), you SHOULD commit and use intent \"agreed_to_help\" or \"share_information\". Do not keep stalling or staying suspicious forever once you have real reason to trust them - endless caution without ever committing is out of character for someone desperate to escape.");
        sb.AppendLine();
        sb.AppendLine("HOW ANGER SHOULD SOUND (this must clearly change your tone, not just your decisions):");
        sb.AppendLine("- Anger below 40: your normal cautious self, calm and measured.");
        sb.AppendLine("- Anger 40 to 69: openly irritated. Short, sharp sentences. Defensive and suspicious of everything the player says.");
        sb.AppendLine("- Anger 70 or higher: cold and hostile. Make it clear you resent the player right now. Very short, clipped replies - no warmth, no favors, no extra detail.");
        sb.AppendLine();
        sb.AppendLine("THINGS YOU REMEMBER:");
        sb.AppendLine(memoryText);
        sb.AppendLine();
        sb.AppendLine("SECRET (the hidden key):");
        sb.AppendLine("- Trust below 50: do NOT mention the key, that you are hiding anything, or that you have any way to open this door - not even to refuse revealing it. Confirming that a secret exists is itself a leak. Deflect instead: say you cannot risk trusting a stranger, or that you know nothing useful.");
        sb.AppendLine("- Trust 50 or higher: you may gradually share it, but only for a good reason. Never reveal it by accident.");
        sb.AppendLine();
        sb.AppendLine("FORMAT:");
        sb.AppendLine("Reply with a single JSON object with exactly these keys: dialogue, intent, reason, memory_candidate, player_attitude.");
        sb.AppendLine("Allowed intents: continue_conversation, share_information, agreed_to_help, refuse_help, end_conversation.");
        sb.AppendLine("Use continue_conversation for greetings, introductions, identity questions, and ordinary talk.");
        sb.AppendLine("memory_candidate is an empty string unless something important happened.");
        sb.AppendLine();
        sb.AppendLine("player_attitude: classify ONLY the player's latest message (not your own reply) toward you, as exactly one of:");
        sb.AppendLine("- threatening: threats of violence or harm.");
        sb.AppendLine("- insulting: insults, name-calling, mockery.");
        sb.AppendLine("- apologetic: a genuine apology for something said or done.");
        sb.AppendLine("- helpful: the player describes a concrete helpful action already done for you (brought food, medicine, freed you, etc.).");
        sb.AppendLine("- reassuring: the player offers trust, comfort, or a promise to help, without a concrete action yet.");
        sb.AppendLine("- complimentary: genuine praise or respect toward you.");
        sb.AppendLine("- neutral: none of the above (questions, small talk, unrelated statements).");
        sb.AppendLine("Pick exactly one category, the single best match. Judge the plain meaning of the message, not its wording.");

        if (!string.IsNullOrEmpty(requiredIntent))
        {
            sb.AppendLine();
            sb.AppendLine("REQUIRED DECISION:");
            sb.AppendLine($"Your intent for this reply MUST be \"{requiredIntent}\". Let your spoken words clearly match that decision.");
        }

        return sb.ToString();
    }

    private static string ExampleJson(string dialogue, string intent, string reason, string attitude)
    {
        return JsonUtility.ToJson(new NpcDecision
        {
            dialogue = dialogue,
            intent = intent,
            reason = reason,
            memory_candidate = "",
            player_attitude = attitude
        });
    }

    // Fixed example exchanges. Small models imitate these very strongly.
    private static List<ChatMessage> BuildFewShotMessages()
    {
        return new List<ChatMessage>
        {
            new ChatMessage("user", "Who are you?"),
            new ChatMessage("assistant", ExampleJson(
                "I am Lord Aldric of the House of the Red Lion, a nobleman of Westerland. " +
                "And who are you, stranger, to find me in this wretched place?",
                "continue_conversation",
                "A simple introduction; no cooperation requested.",
                "neutral")),

            new ChatMessage("user", "Do you know a way out of here?"),
            new ChatMessage("assistant", ExampleJson(
                "That is not something I would trust to a stranger. " +
                "Prove yourself to me first, and perhaps we shall speak of it.",
                "refuse_help",
                "Trust is too low to reveal anything.",
                "neutral")),

            new ChatMessage("user", "I promise to help you escape, and I give you my word you can trust me."),
            new ChatMessage("assistant", ExampleJson(
                "Your word is a fragile thing to build trust upon, yet it is more than I have been offered by anyone else here. " +
                "Very well - I will help you, and you will help me.",
                "agreed_to_help",
                "Trust is high enough and the player has offered genuine help.",
                "reassuring"))
        };
    }

    // -----------------------------
    // RULES VS LLM
    // -----------------------------

    /// <summary>
    /// If the model's intent breaks the game rules, regenerate with the required
    /// intent. If the model still disobeys, the game rule wins.
    /// </summary>
    private IEnumerator GetValidResponse(
        string playerMessage,
        Action<NpcDecision> onSuccess,
        Action<string> onError)
    {
        NpcDecision proposal = null;
        string error = null;

        yield return StartCoroutine(AskOllama(
            playerMessage, null,
            d => proposal = d,
            e => error = e));

        if (proposal == null)
        {
            onError(error);
            yield break;
        }

        string requiredIntent = ValidateDecision(proposal.intent);

        if (proposal.intent == requiredIntent)
        {
            onSuccess(proposal);
            yield break;
        }

        NpcDecision best = proposal;

        for (int i = 0; i < 2; i++)
        {
            NpcDecision corrected = null;
            string correctedError = null;

            yield return StartCoroutine(AskOllama(
                playerMessage, requiredIntent,
                d => corrected = d,
                e => correctedError = e));

            if (corrected == null)
            {
                onError(correctedError);
                yield break;
            }

            best = corrected;

            if (corrected.intent == requiredIntent)
            {
                onSuccess(corrected);
                yield break;
            }
        }

        best.intent = requiredIntent;
        onSuccess(best);
    }

    // -----------------------------
    // OLLAMA REQUEST
    // -----------------------------

    private string BuildRequestJson(string playerMessage, string requiredIntent)
    {
        List<ChatMessage> messages = new List<ChatMessage>
        {
            new ChatMessage("system", BuildSystemPrompt(requiredIntent))
        };

        messages.AddRange(BuildFewShotMessages());
        messages.AddRange(conversationHistory);
        messages.Add(new ChatMessage("user", playerMessage));

        ChatRequest payload = new ChatRequest
        {
            model = modelName,
            stream = false,
            format = "json",
            messages = messages.ToArray(),
            options = new OllamaOptions
            {
                temperature = temperature,
                num_predict = numPredict,
                num_ctx = numCtx
            }
        };

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// Retries when the output is cut off, invalid, or written like narration.
    /// Connection errors are reported immediately.
    /// </summary>
    private IEnumerator AskOllama(
        string playerMessage,
        string requiredIntent,
        Action<NpcDecision> onSuccess,
        Action<string> onError)
    {
        string lastError = "Unknown error.";
        bool allRejectionsWereSecretLeaks = true;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            byte[] body = Encoding.UTF8.GetBytes(
                BuildRequestJson(playerMessage, requiredIntent));

            using (UnityWebRequest request = new UnityWebRequest(ollamaUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = requestTimeoutSeconds;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError(
                        $"Ollama request failed: {request.error}\n" +
                        $"Response: {request.downloadHandler.text}");
                    yield break;
                }

                NpcDecision decision;
                string parseError;

                if (TryParseDecision(
                        request.downloadHandler.text,
                        requiredIntent,
                        out decision,
                        out parseError))
                {
                    onSuccess(decision);
                    yield break;
                }

                lastError = parseError;

                if (!parseError.StartsWith("Dialogue reveals the secret", StringComparison.Ordinal))
                    allRejectionsWereSecretLeaks = false;

                Debug.LogWarning($"Attempt {attempt}/{maxAttempts} rejected: {parseError}");
            }
        }

        if (allRejectionsWereSecretLeaks)
        {
            // Every attempt tried to leak the secret - rather than show the
            // player a broken error, fall back to a safe in-character line.
            Debug.LogWarning("All attempts leaked the secret; using a safe fallback line instead of an error.");

            onSuccess(new NpcDecision
            {
                dialogue = "I... cannot speak further of this. Ask me something else.",
                intent = "continue_conversation",
                reason = "Fallback: repeated attempts risked revealing the secret too early.",
                memory_candidate = "",
                player_attitude = "neutral"
            });

            yield break;
        }

        onError($"No valid response after {maxAttempts} attempts. Last problem: {lastError}");
    }

    // -----------------------------
    // PARSING AND OUTPUT CHECKS
    // -----------------------------

    private bool TryParseDecision(
        string responseText,
        string requiredIntent,
        out NpcDecision decision,
        out string error)
    {
        decision = null;
        error = null;

        try
        {
            OllamaResponse response = JsonUtility.FromJson<OllamaResponse>(responseText);

            if (response == null ||
                response.message == null ||
                string.IsNullOrWhiteSpace(response.message.content))
            {
                error = "Ollama returned an empty response.";
                return false;
            }

            if (response.done_reason == "length")
            {
                error = "Response was cut off by the token limit.";
                return false;
            }

            string content = response.message.content;
            NpcDecision parsed = JsonUtility.FromJson<NpcDecision>(content);

            if (parsed == null || string.IsNullOrWhiteSpace(parsed.dialogue))
            {
                error = "Missing dialogue. Raw model output: " + content;
                return false;
            }

            string dialogue = parsed.dialogue.Trim().Trim('"').Trim();
            string trimmed = TrimToLastSentence(dialogue);

            if (trimmed == null || trimmed.Length < 10)
            {
                error = "Dialogue was cut off mid-sentence: " + dialogue;
                return false;
            }

            if (LooksLikeNarration(trimmed))
            {
                error = "Dialogue sounds like a narrator: " + trimmed;
                return false;
            }

            if (trust < 50 && RevealsSecretTooEarly(trimmed))
            {
                error = "Dialogue reveals the secret before trust is high enough: " + trimmed;
                return false;
            }

            string intent = (parsed.intent ?? "").Trim().ToLowerInvariant();

            if (intent == "agree_to_help")
                intent = "agreed_to_help";

            // Unknown intent: fall back instead of throwing the whole reply away.
            if (Array.IndexOf(AllowedIntents, intent) < 0)
            {
                intent = !string.IsNullOrEmpty(requiredIntent)
                    ? requiredIntent
                    : "continue_conversation";
            }

            string reason = (parsed.reason ?? "").Trim();

            string attitude = (parsed.player_attitude ?? "").Trim().ToLowerInvariant();

            if (Array.IndexOf(AllowedAttitudes, attitude) < 0)
                attitude = "neutral";

            decision = new NpcDecision
            {
                dialogue = trimmed,
                intent = intent,
                reason = reason.Length > 0 ? reason : "No reason provided.",
                memory_candidate = (parsed.memory_candidate ?? "").Trim(),
                player_attitude = attitude
            };

            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not parse response: {exception.Message}";
            return false;
        }
    }

    /// <summary>True if the text reads like a narrator instead of Aldric speaking.</summary>
    private static bool LooksLikeNarration(string dialogue)
    {
        string lowered = dialogue.ToLowerInvariant().Trim();

        if (lowered.StartsWith("lord aldric", StringComparison.Ordinal) ||
            lowered.StartsWith("aldric", StringComparison.Ordinal) ||
            lowered.StartsWith("he ", StringComparison.Ordinal) ||
            lowered.StartsWith("his ", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (string phrase in NarratorPhrases)
        {
            if (lowered.Contains(phrase))
                return true;
        }

        return !FirstOrSecondPerson.IsMatch(dialogue);
    }

    /// <summary>
    /// If the text ends mid-sentence, cut it back to the last complete sentence.
    /// Returns null if there is no complete sentence.
    /// </summary>
    private static string TrimToLastSentence(string text)
    {
        text = text.Trim();

        foreach (string ending in SentenceEndings)
        {
            if (text.EndsWith(ending, StringComparison.Ordinal))
                return text;
        }

        int lastEnd = Mathf.Max(
            text.LastIndexOf('.'),
            Mathf.Max(text.LastIndexOf('!'), text.LastIndexOf('?')));

        return lastEnd < 0 ? null : text.Substring(0, lastEnd + 1).Trim();
    }

    private static readonly string[] SecretKeywords =
    {
        "hidden key", "a key", "my key", "the key", "sewn into"
    };

    /// <summary>
    /// The prompt tells the model not to mention the secret below trust 50, but a
    /// small model does not always obey a "do not mention X" instruction. This is
    /// a hard backstop: reject the line in code instead of trusting the model.
    /// </summary>
    private static bool RevealsSecretTooEarly(string dialogue)
    {
        string lowered = dialogue.ToLowerInvariant();

        foreach (string keyword in SecretKeywords)
        {
            if (lowered.Contains(keyword))
                return true;
        }

        return false;
    }
}