import json
from typing import Any

import requests


class LLMClient:
    """Minimal LLM client wrapper."""

    def __init__(self, api_key: str, endpoint: str) -> None:
        self.api_key = api_key
        self.endpoint = endpoint

    def generate(self, prompt: str, temperature: float = 0.5) -> str:
        # Replace this payload format with your LLM provider schema.
        payload = {"prompt": prompt, "temperature": temperature}
        headers = {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json",
        }
        resp = requests.post(self.endpoint, headers=headers, data=json.dumps(payload), timeout=60)
        resp.raise_for_status()
        data: dict[str, Any] = resp.json()
        return (
            data.get("text")
            or data.get("output", {}).get("text")
            or data.get("choices", [{}])[0].get("text")
            or ""
        ).strip()


class VideoAPIClient:
    """Minimal background video generation client wrapper."""

    def __init__(self, api_key: str, endpoint: str) -> None:
        self.api_key = api_key
        self.endpoint = endpoint

    def generate_video_url(self, prompt: str, duration_seconds: int = 20) -> str:
        # Replace this payload format with your Video API schema.
        payload = {"prompt": prompt, "duration_seconds": duration_seconds}
        headers = {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json",
        }
        resp = requests.post(self.endpoint, headers=headers, json=payload, timeout=120)
        resp.raise_for_status()
        data = resp.json()
        return (
            data.get("video_url")
            or data.get("result", {}).get("video_url")
            or data.get("data", {}).get("video_url")
            or ""
        ).strip()


class VoiceAPIClient:
    """Minimal expressive voice generation client wrapper."""

    def __init__(self, api_key: str, endpoint: str) -> None:
        self.api_key = api_key
        self.endpoint = endpoint

    def generate_voice(
        self,
        text: str,
        voice_name: str = "expressive_female_vi",
        language: str = "vi",
    ) -> dict[str, Any]:
        # Replace this payload format with your Voice API schema.
        payload = {
            "text": text,
            "voice": voice_name,
            "language": language,
            "style": "expressive",
            "return_timestamps": True,
        }
        headers = {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json",
        }
        resp = requests.post(self.endpoint, headers=headers, json=payload, timeout=120)
        resp.raise_for_status()
        return resp.json()
