import os
from pathlib import Path
from typing import Any

from moviepy.editor import (
    AudioFileClip,
    CompositeAudioClip,
    CompositeVideoClip,
    TextClip,
    VideoFileClip,
)


class VideoRenderer:
    """Renders final MP4 with caption overlays and ducked music."""

    def render(
        self,
        background_video_path: str,
        voice_audio_path: str,
        output_path: str,
        quote_text: str,
        word_timestamps: list[dict[str, Any]] | None = None,
        music_path: str | None = None,
        caption_font: str = "Arial",
        caption_font_size: int = 64,
        voice_volume: float = 1.0,
        music_volume: float = 0.22,
        fps: int = 30,
    ) -> str:
        Path(os.path.dirname(output_path) or ".").mkdir(parents=True, exist_ok=True)

        bg = VideoFileClip(background_video_path)
        voice = AudioFileClip(voice_audio_path).volumex(voice_volume)

        target_duration = min(bg.duration, voice.duration) if voice.duration > 0 else bg.duration
        video = bg.subclip(0, target_duration)

        audio_layers = [voice]
        if music_path and os.path.exists(music_path):
            music = AudioFileClip(music_path)
            music = music.subclip(0, target_duration).audio_fadein(1.2).audio_fadeout(1.2).volumex(music_volume)
            audio_layers.append(music)

        mixed_audio = CompositeAudioClip(audio_layers)

        # Build timed captions from timestamps if available; fallback to one centered quote.
        caption_clips = self._build_caption_clips(
            quote_text=quote_text,
            duration=target_duration,
            timestamps=word_timestamps or [],
            font=caption_font,
            font_size=caption_font_size,
        )

        final = CompositeVideoClip([video, *caption_clips]).set_audio(mixed_audio)
        final.write_videofile(
            output_path,
            codec="libx264",
            audio_codec="aac",
            fps=fps,
            bitrate="12M",
            threads=4,
            preset="medium",
        )

        final.close()
        video.close()
        bg.close()
        voice.close()
        return output_path

    def _build_caption_clips(
        self,
        quote_text: str,
        duration: float,
        timestamps: list[dict[str, Any]],
        font: str,
        font_size: int,
    ) -> list[TextClip]:
        clips: list[TextClip] = []
        if timestamps:
            # Group words into short chunks for better readability.
            chunk: list[dict[str, Any]] = []
            for token in timestamps:
                chunk.append(token)
                if len(chunk) >= 4:
                    clips.append(self._chunk_to_textclip(chunk, font, font_size))
                    chunk = []
            if chunk:
                clips.append(self._chunk_to_textclip(chunk, font, font_size))
            return clips

        # Fallback: one quote in center with a fade in.
        clip = (
            TextClip(
                txt=quote_text,
                fontsize=font_size,
                font=font,
                color="white",
                stroke_color="black",
                stroke_width=2,
                method="caption",
                size=(960, None),
                align="center",
            )
            .set_position(("center", "center"))
            .set_start(0.3)
            .set_duration(max(2, duration - 0.3))
            .crossfadein(0.5)
        )
        return [clip]

    @staticmethod
    def _chunk_to_textclip(chunk: list[dict[str, Any]], font: str, font_size: int) -> TextClip:
        words = [str(x.get("word", "")).strip() for x in chunk if str(x.get("word", "")).strip()]
        text = " ".join(words) if words else "..."
        start = float(chunk[0].get("start", 0.0))
        end = float(chunk[-1].get("end", start + 1.2))
        duration = max(0.3, end - start)
        return (
            TextClip(
                txt=text,
                fontsize=font_size,
                font=font,
                color="white",
                stroke_color="black",
                stroke_width=2,
                method="caption",
                size=(960, None),
                align="center",
            )
            .set_position(("center", "center"))
            .set_start(start)
            .set_duration(duration)
            .crossfadein(0.2)
        )
