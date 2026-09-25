import argparse
import base64
import json
import mimetypes
import os
import sys
import urllib.error
import urllib.request

API_URL = "https://openrouter.ai/api/v1/chat/completions"
DEFAULT_MODEL = "xiaomi/mimo-v2.6-flash"


def main():
    parser = argparse.ArgumentParser(description="Ask an OpenRouter vision model about a screenshot.")
    parser.add_argument("image", help="Path to the image file")
    parser.add_argument("prompt", help="Question to ask about the image")
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--max-tokens", type=int, default=2000)
    parser.add_argument("--temperature", type=float, default=0.0)
    args = parser.parse_args()

    key = os.environ.get("OPENROUTER_API_KEY")
    if not key:
        print("OPENROUTER_API_KEY is not set", file=sys.stderr)
        sys.exit(2)

    mime = mimetypes.guess_type(args.image)[0] or "image/png"
    with open(args.image, "rb") as handle:
        encoded = base64.b64encode(handle.read()).decode("ascii")

    payload = {
        "model": args.model,
        "max_tokens": args.max_tokens,
        "temperature": args.temperature,
        "messages": [
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": args.prompt},
                    {"type": "image_url", "image_url": {"url": f"data:{mime};base64,{encoded}"}},
                ],
            },
        ],
    }

    request = urllib.request.Request(
        API_URL,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {key}",
            "Content-Type": "application/json",
            "HTTP-Referer": "http://localhost/",
            "X-Title": "SimpleLauncher Linux VM GUI test",
        },
        method="POST",
    )

    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            data = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", "replace")
        print(f"HTTP {exc.code}: {body}", file=sys.stderr)
        sys.exit(1)
    except Exception as exc:  # noqa: BLE001
        print(f"request failed: {exc}", file=sys.stderr)
        sys.exit(1)

    choices = data.get("choices") or []
    if not choices:
        print(json.dumps(data, indent=2)[:4000])
        sys.exit(1)

    content = choices[0].get("message", {}).get("content")
    if isinstance(content, list):
        text = "\n".join(part.get("text", "") for part in content if isinstance(part, dict))
    else:
        text = content or ""
    print(text)

    usage = data.get("usage")
    if usage:
        print("\n--- usage ---")
        print(json.dumps(usage))


if __name__ == "__main__":
    main()
