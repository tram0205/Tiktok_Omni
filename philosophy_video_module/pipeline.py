import os
from dataclasses import dataclass
from pathlib import Path

from .background_generator import VideoBackgroundGenerator
from .clients import LLMClient, VideoAPIClient, VoiceAPIClient
from .config import AppConfig
from .content_processor import ContentProcessor
from .video_renderer import VideoRenderer
from .voice_generator import VoiceGenerator


@dataclass
class PipelineResult:
    output_video_path: str
    philosophy_quote: str
    mood: str
    visual_prompt: str
    music_style: str


class PhilosophyVideoPipeline:
    """Orchestrates the complete quote-video generation flow."""

    def __init__(self, config: AppConfig) -> None:
        self.config = config
        self.content_processor = ContentProcessor(LLMClient(config.llm_api_key, config.llm_endpoint))
        self.bg_generator = VideoBackgroundGenerator(VideoAPIClient(config.video_api_key, config.video_api_endpoint))
        self.voice_generator = VoiceGenerator(VoiceAPIClient(config.voice_api_key, config.voice_api_endpoint))
        self.renderer = VideoRenderer()

    def run(self, text_or_url: str, output_dir: str = "outputs") -> PipelineResult:
        Path(output_dir).mkdir(parents=True, exist_ok=True)
        assets_dir = os.path.join(output_dir, "assets")
        Path(assets_dir).mkdir(parents=True, exist_ok=True)

        processed = self.content_processor.process(text_or_url)
        visual_prompt = processed.visual_style_prompt

        bg_path = self.bg_generator.generate_background(
            visual_prompt=visual_prompt,
            output_dir=assets_dir,
            duration_seconds=20,
        )

        voice = self.voice_generator.generate_voiceover(
            text=processed.philosophy_sentence,
            output_dir=assets_dir,
            voice_name="expressive_female_vi",
        )

        # Optional: if you have a local mood-based music library, map file path here.
        music_path = self._resolve_music_by_mood(processed.mood)

        output_video_path = os.path.join(output_dir, "philosophy_video.mp4")
        self.renderer.render(
            background_video_path=bg_path,
            voice_audio_path=voice.audio_path,
            output_path=output_video_path,
            quote_text=processed.philosophy_sentence,
            word_timestamps=voice.word_timestamps,
            music_path=music_path,
            caption_font=self.config.caption_font,
            caption_font_size=self.config.caption_font_size,
            voice_volume=self.config.voice_volume,
            music_volume=self.config.music_volume,
            fps=self.config.fps,
        )

        return PipelineResult(
            output_video_path=output_video_path,
            philosophy_quote=processed.philosophy_sentence,
            mood=processed.mood,
            visual_prompt=processed.visual_style_prompt,
            music_style=processed.music_style,
        )

    @staticmethod
    def _resolve_music_by_mood(mood: str) -> str | None:
        # Plug your local music mapping logic here.
        # Example:
        # library = {
        #   "calm": "assets/music/ambient_piano.mp3",
        #   "hopeful": "assets/music/uplifting_lofi.mp3",
        # }
        # return library.get(mood)
        return None
