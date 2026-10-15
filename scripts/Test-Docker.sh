#!/usr/bin/env bash
set -euo pipefail
container_name="studio-smoke-${RANDOM}"
docker build -t campaign-studio:ci .
trap 'docker logs "$container_name" || true; docker rm -f "$container_name" >/dev/null || true' EXIT
docker run -d --name "$container_name" -p 127.0.0.1::8080 \
  -e Studio__ApiKey=0123456789abcdef0123456789abcdef \
  -e Studio__TokenSecret=abcdef0123456789abcdef0123456789 \
  -e Studio__EnableLiveSending=false campaign-studio:ci
port="$(docker port "$container_name" 8080/tcp | cut -d: -f2)"
ready=0
for attempt in $(seq 1 60); do
  if curl --fail --silent "http://127.0.0.1:$port/health" > /tmp/studio-health.json; then ready=1; break; fi
  sleep 1
done
test "$ready" = 1
status="$(curl --silent -o /dev/null -w '%{http_code}' "http://127.0.0.1:$port/api/settings")"
test "$status" = 401
curl --fail --silent -H 'Authorization: Bearer 0123456789abcdef0123456789abcdef' "http://127.0.0.1:$port/api/settings" > /tmp/studio-settings.json
python3 - <<'PY'
import json
with open('/tmp/studio-settings.json') as f:
    settings = json.load(f)
assert settings['liveSendingEnabled'] is False, settings
print('Container startup, authentication and capture-only default passed.')
PY
docker exec "$container_name" sh -c 'test "$(id -u)" != 0 && test -w /app/data'
