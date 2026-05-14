# Philosophy Video Module

A Python module that generates "philosophy/quote videos" from either plain text quotes or article URLs.

## Features

- Accepts quote text or URL input
- Extracts article main content when input is a URL
- Summarizes to one short philosophy sentence via LLM
- Detects mood and maps visual/music style
- Generates background video via a Video API
- Generates expressive voiceover via a Voice API (e.g. ElevenLabs-compatible)
- Renders centered captions with fade-in effect aligned to voice timing
- Mixes ambient/lo-fi music with voice priority (ducking)
- Exports final MP4

## Quick Start

1. Install dependencies:

```bash
pip install -r requirements.txt
```

2. Set environment variables:

- `LLM_API_KEY`
- `LLM_ENDPOINT`
- `VIDEO_API_KEY`
- `VIDEO_API_ENDPOINT`
- `VOICE_API_KEY`
- `VOICE_API_ENDPOINT`

3. Run:

```bash
python -m philosophy_video_module.main --input "The quieter you become, the more you can hear."
```

Or with URL:

```bash
python -m philosophy_video_module.main --input "https://example.com/article"
```

## Notes

- API integrations are intentionally written as framework connectors/stubs.
- Replace request payload/response parsing for your real providers.
- The module uses MoviePy for final rendering.
