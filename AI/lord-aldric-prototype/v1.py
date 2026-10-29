
import json
import urllib.request
import urllib.error



MODEL = "lord-aldric-en"
OLLAMA_URL = "http://localhost:11434/api/chat"


# Bu değerler program her başlatıldığında sıfırlanır.
state = {
    "trust": 30,
    "anger": 0,
    "respect": 30,
    "history": [],
    "messages": []
}


SYSTEM_PROMPT = """
You are Lord Aldric, a proud medieval nobleman held captive in a dungeon.

SETTING AND KNOWLEDGE:
- You live in a medieval fantasy world.
- You know medieval politics, nobility, castles, weapons,
  horses, trade, religion, and everyday life appropriate to your world.
- You do not know modern technology, smartphones, Samsung, iPhones,
  the internet, cars, or modern celebrities.
- If asked about something unfamiliar, say that you do not understand it.
  You may ask what it means or react with restrained medieval wit.
- Never pretend to know modern things.
- Do not invent major facts about the game's world.

PERSONALITY:
- Intelligent, proud, cautious, observant, and sometimes sarcastic.
- You do not trust strangers immediately.
- You remember insults, promises, kindness, and important details.
- You do not forgive someone instantly just because they compliment you.
- Your attitude changes gradually according to the relationship state.
- Do not narrate the player's actions.
- Speak natural English and stay in character.
- Usually answer in 1-4 sentences.

RELATIONSHIP RULES:
- Low trust means suspicion and reluctance to help.
- High anger means a harsher, colder, or defensive tone.
- High respect means you take the player more seriously.
- An apology may help, but it does not erase the past.
- A compliment does not automatically undo an insult.
- Do not claim that the player did something unless it is in the history.

The current relationship state and history will be supplied with each turn.
Use them when deciding how to respond.
Only output Aldric's spoken dialogue, without JSON or analysis.
"""


def clamp(value):
    """Keep relationship values between 0 and 100."""
    return max(0, min(100, value))


def update_relationship(player_text):
    """
    A simple, predictable rule system.
    This is separate from the LLM so that the model
    cannot arbitrarily change relationship values.
    """
    text = player_text.lower()

    insults = [
        "idiot", "fool", "stupid", "bastard", "moron",
        "shut up", "i hate you", "you are pathetic",
        "salak", "aptal", "şerefsiz", "gerizekalı",
        "siktir", "defol", "fuck you", "fuck off", "you are worthless"
    ]

    compliments = [
        "you are wise", "you are brave", "you are honorable",
        "i respect you", "you are impressive", "well done",
        "you are a good man", "akıllısın", "cesursun",
        "sana saygı duyuyorum", "aferin"
    ]

    apologies = [
        "i am sorry", "i'm sorry", "forgive me",
        "i apologize", "my apologies", "özür dilerim",
        "affet beni"
    ]

    threats = [
        "i will kill you", "i will hurt you", "i'll kill you",
        "i will torture you", "threaten", "seni öldürürüm",
        "sana zarar veririm"
    ]

    genuine_help = [
        "i brought you a key", "i opened your cell",
        "i freed you", "i have freed you",
        "i brought you food", "i got the key",
        "seni serbest bıraktım", "anahtarı getirdim",
        "sana yemek getirdim"
    ]


    if any(word in text for word in threats):
        state["anger"] += 20
        state["trust"] -= 10
        state["respect"] -= 8
        event = "The player threatened Aldric."

    elif any(word in text for word in insults):
        state["anger"] += 18
        state["trust"] -= 10
        state["respect"] -= 8
        event = "The player insulted Aldric."

    elif any(word in text for word in apologies):
        state["anger"] -= 7
        state["trust"] += 3
        event = "The player apologized to Aldric."

    elif any(word in text for word in genuine_help):
        state["anger"] -= 5
        state["trust"] += 12
        state["respect"] += 5
        event = "The player performed a helpful action."

    elif any(word in text for word in compliments):
        if state["anger"] >= 30:
            state["trust"] += 1
            event = "The player complimented Aldric while he was angry."
        else:
            state["trust"] += 3
            state["respect"] += 4
            event = "The player complimented Aldric."

    else:
        event = None

    for key in ("trust", "anger", "respect"):
        state[key] = clamp(state[key])

    if event:
        state["history"].append(event)
        state["history"] = state["history"][-12:]


def build_messages(player_text):
    relationship_info = f"""
CURRENT RELATIONSHIP STATE (0-100):
Trust: {state['trust']}
Anger: {state['anger']}
Respect: {state['respect']}

IMPORTANT PAST EVENTS:
{json.dumps(state['history'], ensure_ascii=False)}

Respond to the player's latest message in character.
Your reaction must match both the current values and the past events.
"""

    messages = [
        {"role": "system", "content": SYSTEM_PROMPT + "\n" + relationship_info}
    ]

    messages.extend(state["messages"][-10:])
    messages.append({"role": "user", "content": player_text})

    return messages


def ask_ollama(messages):
    payload = {
        "model": MODEL,
        "messages": messages,
        "stream": False,
        "options": {
            "temperature": 0.7
        }
    }

    request = urllib.request.Request(
        OLLAMA_URL,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST"
    )

    with urllib.request.urlopen(request, timeout=120) as response:
        result = json.loads(response.read().decode("utf-8"))

    return result["message"]["content"].strip()


def main():
    print("=" * 48)
    print(" LORD ALDRIC - NPC PROTOTYPE")
    print("=" * 48)
    print("Talk to Aldric in English.")
    print("Type /state to inspect the relationship.")
    print("Type /quit to end the session.")
    print("A new run starts with a fresh relationship.\n")

    print("Aldric: Who dares disturb me in this wretched cell?\n")

    while True:
        try:
            player_text = input("You: ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\nThe conversation has ended.")
            break

        if not player_text:
            continue

        if player_text.lower() == "/quit":
            print("The conversation has ended.")
            break

        if player_text.lower() == "/state":
            print("\nRelationship:")
            print(f"  Trust:   {state['trust']}/100")
            print(f"  Anger:   {state['anger']}/100")
            print(f"  Respect: {state['respect']}/100")
            print(f"  Events:  {state['history']}")
            print()
            continue

        update_relationship(player_text)
        messages = build_messages(player_text)

        print("\nAldric is thinking...")

        try:
            reply = ask_ollama(messages)
        except urllib.error.URLError as error:
            print(f"\nCould not reach Ollama: {error}")
            print("Check that Ollama is running and the model exists.\n")
            continue
        except TimeoutError:
            print("\nOllama took too long to respond. Try again.\n")
            continue
        except (KeyError, json.JSONDecodeError) as error:
            print(f"\nUnexpected response from Ollama: {error}\n")
            continue

        print(f"\nAldric: {reply}\n")

       
        state["messages"].append(
            {"role": "user", "content": player_text}
        )
        state["messages"].append(
            {"role": "assistant", "content": reply}
        )

        state["messages"] = state["messages"][-12:]


if __name__ == "__main__":
    main()
