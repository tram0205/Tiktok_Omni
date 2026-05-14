import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import requests

from .clients import VoiceAPIClient


@dataclass
class VoiceResult:
    audio_path: str
    word_timestamps: list[dict[str, Any]]


class VoiceGenerator:
    """Creates expressive voiceover and returns timing data for captions."""

    def __init__(self, voice_client: VoiceAPIClient) -> None:
        self.voice_client = voice_client

    def generate_voiceover(
        self,
        text: str,
        output_dir: str,
        voice_name: str = "expressive_female_vi",
    ) -> VoiceResult:
        Path(output_dir).mkdir(parents=True, exist_ok=True)
        payload = self.voice_client.generate_voice(text=text, voice_name=voice_name, language="vi")

        audio_url = (
            payload.get("audio_url")
            or payload.get("result", {}).get("audio_url")
            or payload.get("data", {}).get("audio_url")
            or ""
        )
        if not audio_url:
            raise RuntimeError("Voice API returned empty audio URL.")

        audio_path = os.path.join(output_dir, "voice.mp3")
        self._download_file(audio_url, audio_path)

        # Expected shape is provider dependent.
        # Example format: [{"word":"...", "start":0.32, "end":0.56}, ...]
        timestamps = payload.get("word_timestamps") or payload.get("timestamps") or []
        return VoiceResult(audio_path=audio_path, word_timestamps=timestamps)

    @staticmethod
    def _download_file(url: str, output_path: str) -> None:
        with requests.get(url, stream=True, timeout=120) as resp:
            resp.raise_for_status()
            with open(output_path, "wb") as f:
                for chunk in resp.iter_content(chunk_size=1024 * 256):
                    if chunk:
                        f.write(chunk)
