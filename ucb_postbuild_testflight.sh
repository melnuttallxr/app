#!/usr/bin/env bash
# robust + readable
set -eo pipefail

echo "==> Post-build: upload to TestFlight"

# --- helpers ---
fail() { echo "ERROR: $*" >&2; exit 1; }

require_env() {
  local var="$1"
  if [[ -z "${!var:-}" ]]; then
    fail "Required env var '$var' is not set. Add it in UCB → Build Target → Environment Variables."
  fi
}

decode_base64() {
  # macOS base64 uses -D, GNU uses -d
  if base64 -D >/dev/null 2>&1 <<<"TWFj"; then
    base64 -D
  else
    base64 -d
  fi
}

# --- locate IPA ---
IPA_PATH="$(find "$PWD" -type f -name '*.ipa' -print -quit)"
[[ -n "$IPA_PATH" ]] || fail ".ipa not found. Ensure the iOS build exports an IPA."
echo "Found IPA: $IPA_PATH"

# --- check env ---
require_env "ASC_KEY_ID"
require_env "ASC_ISSUER_ID"
require_env "ASC_KEY_P8_BASE64"

# --- fastlane ---
if ! command -v fastlane >/dev/null 2>&1; then
  echo "Installing fastlane..."
  sudo gem install fastlane --no-document
fi

# --- recreate .p8 and api_key.json ---
ASC_KEY_DIR="$(mktemp -d)"
ASC_KEY_PATH="$ASC_KEY_DIR/AuthKey_${ASC_KEY_ID}.p8"
printf "%s" "$ASC_KEY_P8_BASE64" | decode_base64 > "$ASC_KEY_PATH" || fail "Failed to decode ASC_KEY_P8_BASE64"
chmod 600 "$ASC_KEY_PATH"

API_JSON="$ASC_KEY_DIR/api_key.json"
# escape key lines for JSON safely
KEY_ESCAPED=$(awk '{gsub("\\\\","\\\\\\\\" ); gsub("\"","\\\""); printf "%s\\n",$0}' "$ASC_KEY_PATH")

cat > "$API_JSON" <<JSON
{
  "key_id": "$ASC_KEY_ID",
  "issuer_id": "$ASC_ISSUER_ID",
  "key": "$KEY_ESCAPED",
  "in_house": false
}
JSON

# --- build args for pilot ---
ARGS=( "pilot" "upload"
  "--api_key_path" "$API_JSON"
  "--ipa" "$IPA_PATH"
  "--skip_waiting_for_build_processing"
)

# optional groups
if [[ -n "${TF_GROUPS:-}" ]]; then
  IFS=',' read -ra GROUPS <<< "$TF_GROUPS"
  for g in "${GROUPS[@]}"; do
    # trim spaces
    g="${g#"${g%%[![:space:]]*}"}"; g="${g%"${g##*[![:space:]]}"}"
    [[ -n "$g" ]] && ARGS+=( "--groups" "$g" )
  done
fi

echo "Running: fastlane ${ARGS[*]}"
fastlane "${ARGS[@]}"

echo "==> Upload to TestFlight: DONE"
