#!/usr/bin/env bash
set -euo pipefail

echo "==> Post-build: upload to TestFlight"

# 1) Найдём .ipa, собранный UCB (берём первый найденный)
IPA_PATH="$(find "$PWD" -type f -name '*.ipa' -print -quit)"
if [[ -z "${IPA_PATH:-}" ]]; then
  echo "ERROR: .ipa не найден. Проверьте, что iOS билд формирует .ipa"
  exit 1
fi
echo "Found IPA: $IPA_PATH"

# 2) Установим fastlane (на окружениях macOS UCB обычно есть ruby)
if ! command -v fastlane >/dev/null 2>&1; then
  echo "Installing fastlane..."
  sudo gem install fastlane --no-document
fi

# 3) Восстановим .p8 из base64 во временный файл
ASC_KEY_DIR="$(mktemp -d)"
ASC_KEY_PATH="$ASC_KEY_DIR/AuthKey_$ASC_KEY_ID.p8"
echo "$ASC_KEY_P8_BASE64" | base64 -d > "$ASC_KEY_PATH"
chmod 600 "$ASC_KEY_PATH"

# 4) Создадим JSON-файл для fastlane api_key (без сохранения в репо)
API_JSON="$ASC_KEY_DIR/api_key.json"
cat > "$API_JSON" <<JSON
{
  "key_id": "$ASC_KEY_ID",
  "issuer_id": "$ASC_ISSUER_ID",
  "key": "$(cat "$ASC_KEY_PATH" | awk '{printf "%s\\n", $0}')",
  "in_house": false
}
JSON

# 5) Зальём в TestFlight
# Варианты:
#  - просто загрузить билд (после "Processing" он появится в TestFlight)
#  - сразу разослать внутр. тестерам (--groups)
ARGS=( "pilot" "upload"
  "--api_key_path" "$API_JSON"
  "--ipa" "$IPA_PATH"
  "--skip_waiting_for_build_processing"  # не ждём обработки на стороне Apple
)

# Если заданы группы — добавим автодистрибуцию
if [[ -n "${TF_GROUPS:-}" ]]; then
  IFS=',' read -ra GROUPS <<< "$TF_GROUPS"
  for g in "${GROUPS[@]}"; do
    ARGS+=( "--groups" "$g" )
  done
fi

echo "Running: fastlane ${ARGS[*]}"
fastlane "${ARGS[@]}"

echo "==> Upload to TestFlight: DONE"
