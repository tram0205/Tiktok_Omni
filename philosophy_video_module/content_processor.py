import re
from dataclasses import dataclass
from urllib.parse import urlparse

import requests
from bs4 import BeautifulSoup

from .clients import LLMClient


@dataclass
class ProcessedContent:
    source_text: str
    philosophy_sentence: str
    mood: str
    visual_style_prompt: str
    music_style: str


class ContentProcessor:
    """Extracts and transforms source content into render-ready semantic data."""

    def __init__(self, llm_client: LLMClient) -> None:
        self.llm = llm_client

    def process(self, text_or_url: str) -> ProcessedContent:
        source_text = self._extract_if_url(text_or_url)
        philosophy_sentence = self._summarize_to_quote(source_text)
        mood = self._detect_mood(philosophy_sentence)
        visual_style_prompt = self._build_visual_style_prompt(mood)
        music_style = self._map_music_style(mood)
        return ProcessedContent(
            source_text=source_text,
            philosophy_sentence=philosophy_sentence,
            mood=mood,
            visual_style_prompt=visual_style_prompt,
            music_style=music_style,
        )

    def _extract_if_url(self, value: str) -> str:
        value = (value or "").strip()
        if not value:
            return ""
        if not self._is_url(value):
            return value

        # Basic web extraction: title + paragraph text.
        resp = requests.get(value, timeout=20, headers={"User-Agent": "Mozilla/5.0"})
        resp.raise_for_status()
        soup = BeautifulSoup(resp.text, "lxml")
        title = (soup.title.string.strip() if soup.title and soup.title.string else "")
        paragraphs = [p.get_text(" ", strip=True) for p in soup.find_all("p")]
        body = " ".join(p for p in paragraphs if len(p) > 30)
        merged = f"{title}. {body}".strip()
        return re.sub(r"\s+", " ", merged)

    def _summarize_to_quote(self, source_text: str) -> str:
        prompt = (
            "Summarize the following text into ONE short philosophical quote. "
            "Keep it deep, emotional, and concise (max 25 words). "
            "Return only the final sentence.\n\n"
            f"Text: {source_text}"
        )
        result = self.llm.generate(prompt, temperature=0.6)
        return result or "Silence teaches what noise can never explain."

    def _detect_mood(self, quote: str) -> str:
        prompt = (
            "Classify the mood of this quote into one label among: "
            "calm, hopeful, melancholic, intense, reflective. "
            "Return only one label.\n\n"
            f"Quote: {quote}"
        )
        result = self.llm.generate(prompt, temperature=0.2).lower().strip()
        valid = {"calm", "hopeful", "melancholic", "intense", "reflective"}
        return result if result in valid else "reflective"

    @staticmethod
    def _build_visual_style_prompt(mood: str) -> str:
        style_map = {
            "calm": "cinematic nature, soft sunrise, misty mountain, slow motion, gentle camera drift",
            "hopeful": "golden hour cityscape, cinematic lens flare, uplifting atmosphere, smooth dolly shot",
            "melancholic": "rainy streets at dusk, moody lighting, shallow depth of field, slow cinematic pan",
            "intense": "dramatic clouds, high contrast cinematic look, powerful motion, deep shadows",
            "reflective": "quiet forest path, moody lighting, cinematic composition, slow motion",
        }
        return style_map.get(mood, style_map["reflective"])

    @staticmethod
    def _map_music_style(mood: str) -> str:
        music_map = {
            "calm": "ambient piano",
            "hopeful": "uplifting lo-fi",
            "melancholic": "soft cinematic ambient",
            "intense": "dark ambient pulse",
            "reflective": "minimal lo-fi ambient",
        }
        return music_map.get(mood, "minimal lo-fi ambient")

    @staticmethod
    def _is_url(value: str) -> bool:
        try:
            parsed = urlparse(value)
            return parsed.scheme in {"http", "https"} and bool(parsed.netloc)
        except Exception:
            return False
