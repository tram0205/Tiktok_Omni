import os
from pathlib import Path

import requests

from .clients import VideoAPIClient


class VideoBackgroundGenerator:
    """Generates and downloads background video assets."""

    def __init__(self, video_client: VideoAPIClient) -> None:
        self.video_client = video_client

    def generate_background(
        self,
        visual_prompt: str,
        output_dir: str,
        duration_seconds: int = 20,
    ) -> str:
        Path(output_dir).mkdir(parents=True, exist_ok=True)
        video_url = self.video_client.generate_video_url(
            prompt=visual_prompt,
            duration_seconds=duration_seconds,
        )
        if not video_url:
            raise RuntimeError("Video API returned empty URL.")

        output_path = os.path.join(output_dir, "background.mp4")
        self._download_file(video_url, output_path)
        return output_path

    @staticmethod
    def _download_file(url: str, output_path: str) -> None:
        with requests.get(url, stream=True, timeout=120) as resp:
            resp.raise_for_status()
            with open(output_path, "wb") as f:
                for chunk in resp.iter_content(chunk_size=1024 * 256):
                    if chunk:
                        f.write(chunk)
