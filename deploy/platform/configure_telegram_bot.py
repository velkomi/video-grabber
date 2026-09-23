#!/usr/bin/env python3
"""Configure the VideoGrabber Telegram bot after the HTTPS API is deployed.

Secrets are read from files/environment and are never printed.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys
import urllib.error
import urllib.request


def secret(name: str, file_name: str) -> str:
    direct = os.environ.get(name, "").strip()
    if direct:
        return direct
    path = os.environ.get(file_name, "").strip()
    if not path:
        raise SystemExit(f"{name} or {file_name} is required")
    value = Path(path).read_text(encoding="utf-8").strip()
    if not value:
        raise SystemExit(f"{file_name} points to an empty file")
    return value


def api(token: str, method: str, payload: dict | None = None) -> dict:
    data = None
    headers = {"User-Agent": "VideoGrabber-Telegram-Setup/1.0"}
    if payload is not None:
        data = json.dumps(payload, separators=(",", ":")).encode("utf-8")
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(
        f"https://api.telegram.org/bot{token}/{method}",
        data=data,
        headers=headers,
        method="POST" if payload is not None else "GET",
    )
    with urllib.request.urlopen(request, timeout=20) as response:
        body = json.loads(response.read().decode("utf-8") or "{}")
    if body.get("ok") is not True:
        raise RuntimeError(f"Telegram rejected {method}")
    return body


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    token = secret("VG_TELEGRAM_BOT_TOKEN", "VG_TELEGRAM_BOT_TOKEN_FILE")
    webhook_secret = secret(
        "VG_TELEGRAM_WEBHOOK_SECRET", "VG_TELEGRAM_WEBHOOK_SECRET_FILE"
    )
    username = os.environ.get("VG_TELEGRAM_BOT_USERNAME", "@VideoGra_bot").strip()
    public_url = os.environ.get(
        "VG_PLATFORM_PUBLIC_URL",
        "https://videograbber.srv1902378.hstgr.cloud/",
    ).rstrip("/")
    miniapp_url = os.environ.get(
        "VG_TELEGRAM_MINIAPP_URL",
        public_url + "/miniapp/",
    )

    if not public_url.startswith("https://") or not miniapp_url.startswith("https://"):
        raise SystemExit("Telegram webhook and Mini App URLs must use HTTPS")

    me = api(token, "getMe")["result"]
    actual_username = "@" + str(me.get("username") or "")
    if actual_username.lower() != username.lower():
        raise SystemExit(
            f"Bot token belongs to {actual_username or '<no username>'}, expected {username}"
        )

    plan = {
        "bot": actual_username,
        "webhook": public_url + "/v1/telegram/webhook",
        "miniapp": miniapp_url,
        "commands": [
            "start", "download", "course", "mp3", "media", "jobs",
            "account", "subscription", "devices", "settings", "link",
            "buy", "payments", "help",
        ],
    }
    print(json.dumps(plan, ensure_ascii=False, indent=2))
    if not args.apply:
        print("DRY_RUN_OK")
        return 0

    commands = [
        {"command": "start", "description": "Открыть VideoGrabber"},
        {"command": "download", "description": "Скачать видео по ссылке"},
        {"command": "course", "description": "Скачать полный курс на Windows"},
        {"command": "mp3", "description": "Скачать аудио MP3"},
        {"command": "media", "description": "Редактор и обработка медиа"},
        {"command": "jobs", "description": "Мои задания и очередь"},
        {"command": "account", "description": "Аккаунт и привязки"},
        {"command": "subscription", "description": "Тариф и лимиты"},
        {"command": "devices", "description": "Мои Windows-компьютеры"},
        {"command": "settings", "description": "Открыть Mini App"},
        {"command": "link", "description": "Привязать способ входа"},
        {"command": "buy", "description": "Оплата в Telegram Stars"},
        {"command": "payments", "description": "Платежи и поддержка"},
        {"command": "help", "description": "Справка"},
    ]
    api(token, "setMyCommands", {"commands": commands})
    api(
        token,
        "setChatMenuButton",
        {
            "menu_button": {
                "type": "web_app",
                "text": "VideoGrabber",
                "web_app": {"url": miniapp_url},
            }
        },
    )
    api(
        token,
        "setWebhook",
        {
            "url": public_url + "/v1/telegram/webhook",
            "secret_token": webhook_secret,
            "drop_pending_updates": False,
            "allowed_updates": [
                "message",
                "callback_query",
                "pre_checkout_query",
                "my_chat_member",
            ],
        },
    )

    webhook = api(token, "getWebhookInfo")["result"]
    if webhook.get("url") != public_url + "/v1/telegram/webhook":
        raise RuntimeError("Telegram webhook verification failed")

    print(
        json.dumps(
            {
                "configured": True,
                "bot": actual_username,
                "webhook_set": True,
                "pending_update_count": webhook.get("pending_update_count", 0),
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except urllib.error.HTTPError as exc:
        print(f"TELEGRAM_HTTP_{exc.code}", file=sys.stderr)
        raise SystemExit(2)
    except urllib.error.URLError:
        print("TELEGRAM_NETWORK_ERROR", file=sys.stderr)
        raise SystemExit(2)
