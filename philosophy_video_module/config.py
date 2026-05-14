import os
from dataclasses import dataclass


@dataclass
class AppConfig:
    # API credentials and endpoints
    llm_api_key: str
    llm_endpoint: str
    video_api_key: str
    video_api_endpoint: str
    voice_api_key: str
    voice_api_endpoint: str

    # Rendering defaults
    output_resolution: tuple[int, int] = (1080, 1920)
    fps: int = 30
    voice_volume: float = 1.0
    music_volume: float = 0.22
    caption_font: str = "Arial"
    caption_font_size: int = 64

    @staticmethod
    def from_env() -> "AppConfig":
        return AppConfig(
            llm_api_key=os.getenv("LLM_API_KEY", "").strip(),
            llm_endpoint=os.getenv("LLM_ENDPOINT", "").strip(),
            video_api_key=os.getenv("VIDEO_API_KEY", "").strip(),
            video_api_endpoint=os.getenv("VIDEO_API_ENDPOINT", "").strip(),
            voice_api_key=os.getenv("VOICE_API_KEY", "").strip(),
            voice_api_endpoint=os.getenv("VOICE_API_ENDPOINT", "").strip(),
        )
