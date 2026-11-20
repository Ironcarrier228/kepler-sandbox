#!/usr/bin/env bash
# Build the desktop (not dedicated server) player from any working directory.
set -euo pipefail
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
version=$(awk '/^m_EditorVersion:/ { print $2; exit }' "$root/ProjectSettings/ProjectVersion.txt")
editor=${UNITY_EDITOR:-"$HOME/Unity/Hub/Editor/$version/Editor/Unity"}
if [[ ! -x "$editor" || ! -f "$editor" ]]; then
    printf 'Unity %s не найдена: %s\nУстановите и активируйте Unity в Hub или укажите UNITY_EDITOR=/полный/путь/Editor/Unity.\n' "$version" "$editor" >&2
    exit 2
fi
builds="$root/Builds"
mkdir -p "$builds"
staging=$(mktemp -d "$builds/.linux-build.XXXXXX")
trap 'rm -rf -- "$staging"' EXIT
name=Kepler-Linux-x86_64
export KEPLER_PLAYER_DIR="$staging/$name"
log="$builds/linux-build.log"
printf 'Сборка Linux x86_64. Лог: %s\n' "$log"
if ! "$editor" -batchmode -nographics -quit -projectPath "$root" -buildTarget Linux64 \
        -executeMethod Kepler.Editor.LinuxPlayerBuild.Run -logFile "$log"; then
    if [[ -f "$log" ]]; then tail -n 40 "$log" >&2; fi
    exit 1
fi
if [[ ! -s "$KEPLER_PLAYER_DIR/Kepler.x86_64" || ! -d "$KEPLER_PLAYER_DIR/Kepler_Data" || ! -s "$KEPLER_PLAYER_DIR/UnityPlayer.so" ]]; then
    printf 'Неполная сборка Unity. См. %s\n' "$log" >&2
    exit 1
fi
chmod +x "$KEPLER_PLAYER_DIR/Kepler.x86_64"
tar -czf "$staging/$name.tar.gz" -C "$staging" "$name"
mv -f -- "$staging/$name.tar.gz" "$builds/$name.tar.gz"
printf 'Готово: %s/%s.tar.gz\n' "$builds" "$name"
