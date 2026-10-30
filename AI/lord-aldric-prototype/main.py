import copy
import json
import re
import urllib.error
import urllib.request


# -----------------------------
# CONFIGURATION
# -----------------------------

MODEL = "lord-aldric-en"
OLLAMA_URL = "http://localhost:11434/api/chat"
REQUEST_TIMEOUT = 120

NUM_PREDICT = 512      # Maximum tokens the model may generate
NUM_CTX = 4096         # Context window
MAX_ATTEMPTS = 4       # Retries for cut-off / narrator-style / invalid output

ALLOWED_INTENTS = {
    "continue_conversation",
    "share_information",
    "agree_to_help",
    "refuse_help",
    "end_conversation",
}

SENTENCE_ENDINGS = (".", "!", "?", "…", '."', '!"', '?"', ".”", "!”", "?”")


def create_initial_state():
    return {
        "trust": 30,
        "anger": 0,
        "fear": 30,
        "history": [],
        "messages": [],
    }


state = create_initial_state()


def clamp(value):
    return max(0, min(100, value))


# -----------------------------
# RELATIONSHIP UPDATES
# -----------------------------

def update_relationship(new_state, player_message):
    """
    Updates relationship values in a temporary state.
    The real game state is only updated after a valid NPC response.
    """

    text = player_message.lower()

    threats = [
        "i will kill you",
        "i'll kill you",
        "kill you",
        "i will hurt you",
        "i'll hurt you",
        "threaten",
    ]

    insults = [
        "idiot",
        "fool",
        "stupid",
        "coward",
        "you are useless",
        "you are pathetic",
    ]

    apologies = [
        "i am sorry",
        "i'm sorry",
        "my apologies",
        "forgive me",
        "i apologize",
    ]

    compliments = [
        "you are wise",
        "you are brave",
        "i respect you",
        "you have my respect",
        "honorable lord",
    ]

    helpful_actions = [
        "i brought you food",
        "i brought food",
        "i helped you",
        "i freed you",
        "i saved you",
        "i brought medicine",
    ]

    # One category per message to avoid stacking every rule.
    if any(phrase in text for phrase in threats):
        new_state["anger"] += 15
        new_state["trust"] -= 10
        new_state["fear"] += 20

    elif any(phrase in text for phrase in insults):
        new_state["anger"] += 18
        new_state["trust"] -= 10
        new_state["fear"] += 12

    elif any(phrase in text for phrase in apologies):
        new_state["anger"] -= 7
        new_state["trust"] += 3

    elif any(phrase in text for phrase in helpful_actions):
        new_state["anger"] -= 5
        new_state["trust"] += 12
        new_state["fear"] -= 5

    elif any(phrase in text for phrase in compliments):
        new_state["trust"] += 1 if new_state["anger"] >= 30 else 3
        new_state["fear"] -= 4

    for key in ("trust", "anger", "fear"):
        new_state[key] = clamp(new_state[key])


# -----------------------------
# DECISION RULES
# -----------------------------

def validate_decision(proposal, current_state):
    """
    The game rules, not the LLM, decide whether cooperation is allowed.
    """

    intent = proposal["intent"]

    if current_state["anger"] >= 85:
        return "end_conversation"

    if intent in {"agree_to_help", "share_information"}:
        if current_state["anger"] >= 60:
            return "refuse_help"

        # High fear can override low trust (reluctant cooperation).
        scared_into_helping = current_state["fear"] >= 70

        if current_state["trust"] < 50 and not scared_into_helping:
            return "refuse_help"

    return intent


def remember_event(new_state, event):
    if not event:
        return

    if event not in new_state["history"]:
        new_state["history"].append(event)

    new_state["history"] = new_state["history"][-12:]


# -----------------------------
# SYSTEM PROMPT + FEW-SHOT EXAMPLES
# -----------------------------

def build_system_prompt(current_state, required_intent=None):
    history_text = (
        "\n".join(f"- {event}" for event in current_state["history"])
        or "- No important events yet."
    )

    required_rule = ""

    if required_intent:
        required_rule = f"""
REQUIRED DECISION:
Your intent for this reply MUST be "{required_intent}".
Let your spoken words clearly match that decision.
"""

    return f"""
You are Lord Aldric, a proud medieval nobleman chained in a dungeon cell.
The player is standing in front of you right now, and you are talking to them.

You ARE Aldric. Everything in "dialogue" is the exact words you say out loud.
Speak in the first person ("I", "my", "me") and address the player as "you".

PERSONALITY:
- Proud, intelligent, cautious, and dignified.
- You protect your secrets and do not trust strangers easily.
- You know nothing about modern things.
- You answer exactly what the player just said.
- You know a secret passage exists in this fortress. You hide its location
  unless your trust is high.

CURRENT FEELINGS (0-100):
Trust: {current_state["trust"]}
Anger: {current_state["anger"]}
Fear: {current_state["fear"]}

THINGS YOU REMEMBER:
{history_text}

HOW YOUR FEELINGS AFFECT YOU:
- Low trust: you are guarded and refuse to reveal secrets.
- High anger: you are cold, hostile, or refuse to talk.
- Very high fear (70+) with low anger: you may give in reluctantly, with a
  shaky, defeated voice.
- High trust: you are warmer and may share what you know.
- Insults are not forgotten quickly; apologies calm you only a little.

FORMAT:
Reply with a single JSON object with exactly these keys:
dialogue (your spoken words, 1 to 3 complete sentences, first person),
intent (one of the allowed intents),
reason (a short explanation),
memory_candidate (a notable event to remember, or "").

Allowed intents: continue_conversation, share_information, agree_to_help,
refuse_help, end_conversation.
Use continue_conversation for ordinary talk such as greetings and small talk.
{required_rule}
"""


def example(dialogue, intent, reason, memory=""):
    return json.dumps(
        {
            "dialogue": dialogue,
            "intent": intent,
            "reason": reason,
            "memory_candidate": memory,
        },
        ensure_ascii=False,
    )


# Fixed example exchanges. Small models imitate these very strongly.
FEW_SHOT_MESSAGES = [
    {"role": "user", "content": "Who are you?"},
    {
        "role": "assistant",
        "content": example(
            "I am Lord Aldric, master of this fortress, though you find me "
            "in chains. And who might you be, stranger, to stand before me?",
            "continue_conversation",
            "A simple introduction; no cooperation requested.",
        ),
    },
    {"role": "user", "content": "Do you know a way out of here?"},
    {
        "role": "assistant",
        "content": example(
            "Such knowledge is not handed to strangers. Earn my trust first, "
            "and perhaps then we shall speak of it.",
            "refuse_help",
            "Trust is too low to reveal anything.",
        ),
    },
]


# -----------------------------
# NARRATOR / BAD-OUTPUT DETECTION
# -----------------------------

NARRATOR_PHRASES = (
    "speaks in",
    "says in",
    "his voice",
    "in a dignified tone",
    "with a hint of",
    "a hint of pride",
    "lord aldric speaks",
    "lord aldric says",
    "aldric speaks",
    "aldric says",
    "aldric replies",
    "aldric responds",
)

FIRST_OR_SECOND_PERSON = re.compile(
    r"\b(i|i'm|i've|i'll|i'd|my|me|mine|you|your|you're|we|us|our)\b",
    re.IGNORECASE,
)


def looks_like_narration(dialogue):
    """
    True if the text reads like a narrator describing Aldric
    instead of Aldric speaking.
    """
    lowered = dialogue.lower().strip()

    if lowered.startswith(("lord aldric", "aldric", "he ", "his ")):
        return True

    if any(phrase in lowered for phrase in NARRATOR_PHRASES):
        return True

    if not FIRST_OR_SECOND_PERSON.search(dialogue):
        return True

    return False


def trim_to_last_sentence(text):
    """
    If the text ends mid-sentence, cut it back to the last complete sentence.
    Returns None if no complete sentence exists.
    """
    text = text.strip()

    if text.endswith(SENTENCE_ENDINGS):
        return text

    last_end = max(text.rfind("."), text.rfind("!"), text.rfind("?"))

    if last_end == -1:
        return None

    return text[: last_end + 1].strip()


# -----------------------------
# OLLAMA REQUEST
# -----------------------------

def ask_ollama_once(current_state, required_intent=None):
    system_prompt = build_system_prompt(
        current_state,
        required_intent=required_intent,
    )

    messages = [{"role": "system", "content": system_prompt}]
    messages.extend(FEW_SHOT_MESSAGES)
    messages.extend(current_state["messages"][-10:])

    payload = {
        "model": MODEL,
        "messages": messages,
        "stream": False,
        "format": "json",
        "options": {
            "temperature": 0.5,
            "num_predict": NUM_PREDICT,
            "num_ctx": NUM_CTX,
        },
    }

    request = urllib.request.Request(
        OLLAMA_URL,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )

    with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT) as response:
        response_data = json.loads(response.read().decode("utf-8"))

    if response_data.get("done_reason") == "length":
        raise ValueError("Response was cut off by the token limit.")

    raw_content = response_data.get("message", {}).get("content", "")

    if not raw_content.strip():
        raise ValueError("Ollama returned an empty response.")

    result = json.loads(raw_content)

    if not isinstance(result, dict):
        raise ValueError("The NPC response must be a JSON object.")

    if not result.get("dialogue"):
        result["dialogue"] = (
            result.get("response")
            or result.get("text")
            or result.get("answer")
            or ""
        )

    dialogue = result.get("dialogue")

    if not isinstance(dialogue, str) or len(dialogue.strip()) < 10:
        raise ValueError(f"Missing or invalid dialogue. Model output: {raw_content}")

    dialogue = dialogue.strip().strip('"').strip()

    trimmed = trim_to_last_sentence(dialogue)

    if trimmed is None or len(trimmed) < 10:
        raise ValueError(f"Dialogue was cut off mid-sentence: {dialogue}")

    dialogue = trimmed

    if looks_like_narration(dialogue):
        raise ValueError(f"Dialogue sounds like a narrator: {dialogue}")

    reason = result.get("reason", "")
    memory_candidate = result.get("memory_candidate", "")

    if not isinstance(reason, str):
        reason = ""

    if not isinstance(memory_candidate, str):
        memory_candidate = ""

    intent = result.get("intent", "")

    if not isinstance(intent, str):
        intent = ""

    intent = intent.strip()

    # Missing/invalid intent: fall back to a neutral, non-cooperative intent.
    if intent not in ALLOWED_INTENTS:
        if required_intent in ALLOWED_INTENTS:
            intent = required_intent
        else:
            intent = "continue_conversation"

    return {
        "dialogue": dialogue,
        "intent": intent,
        "reason": reason.strip() or "Decision based on the current conversation.",
        "memory_candidate": memory_candidate.strip(),
    }


def ask_ollama(current_state, required_intent=None):
    """
    Retries when the output is cut off, invalid, or written like narration.
    Connection errors (URLError, timeout) are raised immediately.
    """
    last_error = None

    for _ in range(MAX_ATTEMPTS):
        try:
            return ask_ollama_once(current_state, required_intent)
        except (json.JSONDecodeError, ValueError) as error:
            last_error = error

    raise ValueError(
        f"Model failed to give a valid response after {MAX_ATTEMPTS} attempts: "
        f"{last_error}"
    )


# -----------------------------
# RESPONSE VALIDATION
# -----------------------------

def get_valid_response(current_state, max_retries=2):
    """
    If the model's intent breaks the game rules, regenerate with the required
    intent. If the model still disobeys, the game rule wins.
    """

    proposal = ask_ollama(current_state)
    required_intent = validate_decision(proposal, current_state)

    if proposal["intent"] == required_intent:
        return proposal

    corrected = proposal

    for _ in range(max_retries):
        corrected = ask_ollama(current_state, required_intent=required_intent)

        if corrected["intent"] == required_intent:
            return corrected

    corrected["intent"] = required_intent
    return corrected


# -----------------------------
# NPC CONVERSATION
# -----------------------------

def process_player_message(player_message):
    global state

    # Work on a copy. Failed requests must not change the real state.
    candidate_state = copy.deepcopy(state)

    update_relationship(candidate_state, player_message)

    candidate_state["messages"].append({
        "role": "user",
        "content": player_message,
    })

    try:
        result = get_valid_response(candidate_state)

    except (
        urllib.error.URLError,
        TimeoutError,
        json.JSONDecodeError,
        KeyError,
        ValueError,
        OSError,
    ) as error:
        print(f"\nCould not process the NPC response: {error}")
        print("Relationship changes were NOT applied.")
        print("Please check Ollama and try again.\n")
        return False

    candidate_state["messages"].append({
        "role": "assistant",
        "content": result["dialogue"],
    })

    remember_event(candidate_state, result["memory_candidate"])

    state = candidate_state

    print(f"\nAldric: {result['dialogue']}")
    print(f"[Intent: {result['intent']}]")
    print(f"[Reason: {result['reason']}]")
    print(
        f"[Trust: {state['trust']} | "
        f"Anger: {state['anger']} | "
        f"Fear: {state['fear']}]"
    )

    return result["intent"] == "end_conversation"


# -----------------------------
# MAIN LOOP
# -----------------------------

def main():
    global state

    state = create_initial_state()

    print("Lord Aldric - NPC Prototype")
    print("Type 'quit' to exit the conversation.")
    print("Relationship values reset when the program restarts.")

    while True:
        try:
            player_message = input("\nYou: ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\nConversation ended.")
            break

        if not player_message:
            continue

        if player_message.lower() in {"quit", "exit"}:
            print("Conversation ended.")
            break

        should_end = process_player_message(player_message)

        if should_end:
            print("\nAldric refuses to continue the conversation.")
            break


if __name__ == "__main__":
    main()