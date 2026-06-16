# AGENTS.md

## Cursor Cloud specific instructions

This repo contains two independent products:

- `tiktok_Omni/` — a **.NET Framework 4.7.2 Windows Forms** desktop app (`WindowsDesktop` SDK, `OutputType=WinExe`). It is **Windows-only and cannot be built or run on the Linux cloud VM** (no `dotnet` SDK present, and WinForms/`net472`/DPAPI/`System.Management` are unavailable on Linux). End-to-end work on it requires a Windows machine — see `MAINTENANCE.md`. Note the `tiktok_Omni.Tests` project referenced in `tiktok_Omni.sln` is **not present** in the repo, so `dotnet test` will not work as-is.
- `philosophy_video_module/` — a cross-platform **Python 3.10+** video-generation pipeline. This is the only component runnable on the Linux cloud VM.

### philosophy_video_module (Python)

- Dependencies (`philosophy_video_module/requirements.txt`) are installed into a virtualenv at `.venv/` (gitignored). Use `.venv/bin/python`.
- Always run from the repo root (`/workspace`) so the package resolves, e.g. `.venv/bin/python -m philosophy_video_module.main --input "..."`.
- Runtime system tools: **FFmpeg** (preinstalled) and **ImageMagick** (`convert`). MoviePy's `TextClip` (caption rendering) shells out to ImageMagick.
  - GOTCHA: Ubuntu's default ImageMagick policy blocks MoviePy captions. The line `<policy domain="path" rights="none" pattern="@*"/>` in `/etc/ImageMagick-6/policy.xml` must be removed/commented out, otherwise `TextClip` fails with `attempt to perform an operation not allowed by the security policy '@/tmp/...'`. This is a one-time system change captured in the VM snapshot (not in the update script).
  - Use a font that exists on Linux (e.g. `DejaVu-Sans`); the repo default `Arial` is not installed here.
- The full CLI pipeline (`python -m philosophy_video_module.main`) calls three **external APIs** configured via env vars: `LLM_API_KEY`/`LLM_ENDPOINT`, `VIDEO_API_KEY`/`VIDEO_API_ENDPOINT`, `VOICE_API_KEY`/`VOICE_API_ENDPOINT` (see `philosophy_video_module/config.py`). Without these, only the local rendering core (`philosophy_video_module/video_renderer.py`, MoviePy + FFmpeg + ImageMagick) can be exercised end-to-end.
- There is no lint config and no automated test suite for the Python module; "verification" is `python -m py_compile` + actually running a render.
