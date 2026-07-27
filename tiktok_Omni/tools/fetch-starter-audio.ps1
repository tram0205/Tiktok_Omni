# Tải starter pack Mixkit (license Mixkit Free — kiểm tra mixkit.co/license trước khi phát hành thương mại).
# Chạy: pwsh -File Tools\fetch-starter-audio.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$musicDir = Join-Path $root "Assets\Audio\Music"
$sfxDir = Join-Path $root "Assets\Audio\Sfx"
New-Item -ItemType Directory -Force -Path $musicDir, $sfxDir | Out-Null

$downloads = @(
    @{ Url = "https://assets.mixkit.co/music/preview/mixkit-serene-view-443.mp3"; Out = "music_serene_view_calm.mp3"; Dir = $musicDir },
    @{ Url = "https://assets.mixkit.co/music/preview/mixkit-tech-house-vibes-130.mp3"; Out = "music_tech_house_upbeat.mp3"; Dir = $musicDir },
    @{ Url = "https://assets.mixkit.co/music/preview/mixkit-a-very-happy-christmas-897.mp3"; Out = "music_happy_light_retail.mp3"; Dir = $musicDir },
    @{ Url = "https://assets.mixkit.co/sfx/preview/mixkit-fast-whoosh-1493.mp3"; Out = "sfx_whoosh_fast.mp3"; Dir = $sfxDir },
    @{ Url = "https://assets.mixkit.co/sfx/preview/mixkit-cinematic-transition-swoosh-1492.mp3"; Out = "sfx_whoosh_cinematic.mp3"; Dir = $sfxDir },
    @{ Url = "https://assets.mixkit.co/sfx/preview/mixkit-achievement-bell-600.mp3"; Out = "sfx_ding_achievement.mp3"; Dir = $sfxDir },
    @{ Url = "https://assets.mixkit.co/sfx/preview/mixkit-select-click-1109.mp3"; Out = "sfx_pop_ui_click.mp3"; Dir = $sfxDir },
    @{ Url = "https://assets.mixkit.co/sfx/preview/mixkit-camera-shutter-click-1133.mp3"; Out = "sfx_camera_shutter.mp3"; Dir = $sfxDir }
)

foreach ($item in $downloads) {
    $dest = Join-Path $item.Dir $item.Out
    if (Test-Path $dest) {
        Write-Host "Skip (exists): $($item.Out)"
        continue
    }
    Write-Host "Downloading $($item.Out)..."
    Invoke-WebRequest -Uri $item.Url -OutFile $dest -UseBasicParsing
}

Write-Host "Done. Music: $musicDir | Sfx: $sfxDir"
