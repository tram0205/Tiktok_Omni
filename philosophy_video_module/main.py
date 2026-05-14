import argparse

from .config import AppConfig
from .pipeline import PhilosophyVideoPipeline


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate philosophy quote videos.")
    parser.add_argument("--input", required=True, help="Quote text or article URL")
    parser.add_argument("--output-dir", default="outputs", help="Output directory")
    args = parser.parse_args()

    config = AppConfig.from_env()
    pipeline = PhilosophyVideoPipeline(config)
    result = pipeline.run(args.input, output_dir=args.output_dir)

    print("Done.")
    print(f"Output: {result.output_video_path}")
    print(f"Quote: {result.philosophy_quote}")
    print(f"Mood: {result.mood}")
    print(f"Visual prompt: {result.visual_prompt}")
    print(f"Music style: {result.music_style}")


if __name__ == "__main__":
    main()
