#!/usr/bin/env bash

set +e
umask 077

PUBLIC_REPO="Vaso73/MultiPingMonitor"
PRIVATE_REPO="Vaso73-Software/Sponsor-Pro-Releases"
EXPECTED_ROOT="/home/vaio/projects/MultiPingMonitor"
MANIFEST_REL="updates/sponsor-pro.json"
ASSEMBLY_REL="MultiPingMonitor/Properties/AssemblyInfo.cs"
ASSET_NAME="MultiPingMonitor.zip"
EXE_NAME="MultiPingMonitor.exe"

MODE=""
VERSION=""
ZIP_INPUT=""
EVIDENCE_INPUT=""
PREFLIGHT_ONLY=0
RESUME_DRAFT_ID=""

usage() {
  cat <<'EOF'
Usage:
  scripts/publish-sponsor-pro-release.sh \
    --mode new|correct \
    --version X.Y.Z \
    --zip /absolute/path/MultiPingMonitor.zip \
    --evidence /absolute/path/release-evidence.json \
    [--resume-draft-id RELEASE_ID] \
    [--preflight-only]
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --mode)
      MODE=${2:-}
      shift 2
      ;;
    --version)
      VERSION=${2:-}
      shift 2
      ;;
    --zip)
      ZIP_INPUT=${2:-}
      shift 2
      ;;
    --evidence)
      EVIDENCE_INPUT=${2:-}
      shift 2
      ;;
    --resume-draft-id)
      RESUME_DRAFT_ID=${2:-}
      shift 2
      ;;
    --preflight-only)
      PREFLIGHT_ONLY=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "ERROR=unknown_argument:$1"
      usage
      exit 2
      ;;
  esac
done

if [ "$MODE" != "new" ] && [ "$MODE" != "correct" ]; then
  echo "ERROR=mode_must_be_new_or_correct"
  usage
  exit 2
fi

if ! printf '%s\n' "$VERSION" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$'; then
  echo "ERROR=invalid_version:$VERSION"
  exit 2
fi

if [ -z "$ZIP_INPUT" ] || [ -z "$EVIDENCE_INPUT" ]; then
  echo "ERROR=zip_and_evidence_are_required"
  usage
  exit 2
fi

TAG="multipingmonitor/v$VERSION"
TITLE="MultiPingMonitor Pro v$VERSION"
STAMP=$(date -u +%Y%m%d-%H%M%S)
BACKUP_ROOT=${MPM_BACKUP_ROOT:-"$HOME/backups/MultiPingMonitor"}
RUN_DIR="$BACKUP_ROOT/publish-v$VERSION-$STAMP"
LOG_FILE="$RUN_DIR/publish.log"
RECOVERY_DIR="$RUN_DIR/recovery"
VERIFY_DIR="$RUN_DIR/remote-verification"
WORK_DIR="$RUN_DIR/work"

mkdir -p "$RECOVERY_DIR" "$VERIFY_DIR" "$WORK_DIR"
if [ "$?" -ne 0 ]; then
  echo "ERROR=cannot_create_run_directory:$RUN_DIR"
  exit 1
fi

exec > >(tee -a "$LOG_FILE") 2>&1

FAIL_REASON=""
REMOTE_MODIFIED=0
RELEASE_ID=""
RECOVERY_ZIP=""
APPROVED_ZIP=""
APPROVED_ZIP_SIZE=""
APPROVED_ZIP_SHA=""
APPROVED_EXE_VERSION=""
CURRENT_HEAD=""
CURRENT_TREE=""
MANIFEST_SIZE=""
MANIFEST_SHA=""

fail() {
  FAIL_REASON=$1
  echo "ERROR=$FAIL_REASON"
  return 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1
  if [ "$?" -ne 0 ]; then
    fail "missing_command:$1"
    return 1
  fi
  return 0
}

read_exe_file_version() {
  python3 - "$1" <<'PY'
import pathlib
import struct
import sys

file_path = pathlib.Path(sys.argv[1])
data = file_path.read_bytes()
marker = "FileVersion".encode("utf-16le") + b"\x00\x00"
positions = []
start = 0
while True:
    idx = data.find(marker, start)
    if idx < 0:
        break
    positions.append(idx)
    start = idx + 2

for idx in positions:
    block_start = idx - 6
    if block_start < 0:
        continue
    try:
        block_length, value_length, value_type = struct.unpack_from("<HHH", data, block_start)
    except struct.error:
        continue
    if block_length < 8 or value_length < 1 or value_length > 128 or value_type != 1:
        continue
    value_offset = (idx + len(marker) + 3) & ~3
    value_end = value_offset + value_length * 2
    if value_end > len(data):
        continue
    try:
        value = data[value_offset:value_end].decode("utf-16le").rstrip("\x00").strip()
    except UnicodeDecodeError:
        continue
    if value and all(ch.isdigit() or ch in ". " for ch in value):
        print(value)
        sys.exit(0)

print("FileVersion resource not found", file=sys.stderr)
sys.exit(1)
PY
}

version_matches() {
  case "$1" in
    "$VERSION"|"$VERSION.0") return 0 ;;
    *) return 1 ;;
  esac
}

get_release_ids_for_tag() {
  gh api --paginate "repos/$PRIVATE_REPO/releases?per_page=100" \
    --jq ".[] | select(.tag_name == \"$TAG\") | .id"
}

get_private_release_json() {
  gh api "repos/$PRIVATE_REPO/releases/tags/$TAG"
}

get_private_release_json_by_id() {
  gh api "repos/$PRIVATE_REPO/releases/$1"
}

download_private_release_asset_by_id() {
  asset_id=$1
  output_file=$2

  gh api \
    -H "Accept: application/octet-stream" \
    "repos/$PRIVATE_REPO/releases/assets/$asset_id" \
    > "$output_file"
}

verify_zip_semantics() {
  zip_path=$1
  extract_dir=$2

  unzip -t "$zip_path" >/dev/null
  if [ "$?" -ne 0 ]; then
    fail "zip_integrity_failed:$zip_path"
    return 1
  fi

  entry_list=$(unzip -Z1 "$zip_path")
  entry_rc=$?
  entry_count=$(printf '%s\n' "$entry_list" | awk 'NF {n++} END {print n+0}')

  if [ "$entry_rc" -ne 0 ] || [ "$entry_count" -ne 1 ] || [ "$entry_list" != "$EXE_NAME" ]; then
    echo "zip_entries_begin"
    printf '%s\n' "$entry_list"
    echo "zip_entries_end"
    fail "zip_content_contract_failed:$zip_path"
    return 1
  fi

  rm -rf "$extract_dir"
  mkdir -p "$extract_dir"
  unzip -q "$zip_path" -d "$extract_dir"
  if [ "$?" -ne 0 ] || [ ! -f "$extract_dir/$EXE_NAME" ]; then
    fail "zip_extract_failed:$zip_path"
    return 1
  fi

  exe_version=$(read_exe_file_version "$extract_dir/$EXE_NAME")
  if [ "$?" -ne 0 ] || ! version_matches "$exe_version"; then
    echo "actual_exe_file_version=$exe_version"
    fail "exe_file_version_mismatch:$zip_path"
    return 1
  fi

  printf '%s\n' "$exe_version"
  return 0
}

load_manifest() {
  manifest_values=$(python3 - "$MANIFEST_REL" <<'PY'
import json
import pathlib
import sys

p = pathlib.Path(sys.argv[1])
obj = json.loads(p.read_text(encoding="utf-8"))
keys = [
    "schemaVersion",
    "channel",
    "latestVersion",
    "releaseTag",
    "assetName",
    "assetSize",
    "sha256",
]
for key in keys:
    value = obj[key]
    if isinstance(value, (dict, list)):
        raise SystemExit(f"invalid scalar field: {key}")
    print(value)
PY
)
  if [ "$?" -ne 0 ]; then
    fail "manifest_parse_failed"
    return 1
  fi

  mapfile -t manifest_fields <<< "$manifest_values"
  if [ "${#manifest_fields[@]}" -ne 7 ]; then
    fail "manifest_field_count_invalid"
    return 1
  fi

  MANIFEST_SCHEMA=${manifest_fields[0]}
  MANIFEST_CHANNEL=${manifest_fields[1]}
  MANIFEST_VERSION=${manifest_fields[2]}
  MANIFEST_TAG=${manifest_fields[3]}
  MANIFEST_ASSET=${manifest_fields[4]}
  MANIFEST_SIZE=${manifest_fields[5]}
  MANIFEST_SHA=${manifest_fields[6]}

  [ "$MANIFEST_SCHEMA" = "1" ] || { fail "manifest_schema_invalid"; return 1; }
  [ "$MANIFEST_CHANNEL" = "sponsor-pro" ] || { fail "manifest_channel_invalid"; return 1; }
  [ "$MANIFEST_VERSION" = "$VERSION" ] || { fail "manifest_version_mismatch"; return 1; }
  [ "$MANIFEST_TAG" = "$TAG" ] || { fail "manifest_tag_mismatch"; return 1; }
  [ "$MANIFEST_ASSET" = "$ASSET_NAME" ] || { fail "manifest_asset_name_mismatch"; return 1; }
  printf '%s\n' "$MANIFEST_SIZE" | grep -Eq '^[0-9]+$' || { fail "manifest_asset_size_invalid"; return 1; }
  printf '%s\n' "$MANIFEST_SHA" | grep -Eq '^[0-9a-f]{64}$' || { fail "manifest_sha256_invalid"; return 1; }
  return 0
}

load_evidence() {
  evidence_values=$(python3 - "$EVIDENCE_INPUT" <<'PY'
import json
import pathlib
import sys

p = pathlib.Path(sys.argv[1])
obj = json.loads(p.read_text(encoding="utf-8"))
for key in ["sourceTree", "version", "tests", "publish", "zipSize", "zipSha256"]:
    print(obj[key])
PY
)
  if [ "$?" -ne 0 ]; then
    fail "evidence_parse_failed"
    return 1
  fi

  mapfile -t evidence_fields <<< "$evidence_values"
  if [ "${#evidence_fields[@]}" -ne 6 ]; then
    fail "evidence_field_count_invalid"
    return 1
  fi

  EVIDENCE_TREE=${evidence_fields[0]}
  EVIDENCE_VERSION=${evidence_fields[1]}
  EVIDENCE_TESTS=${evidence_fields[2]}
  EVIDENCE_PUBLISH=${evidence_fields[3]}
  EVIDENCE_ZIP_SIZE=${evidence_fields[4]}
  EVIDENCE_ZIP_SHA=${evidence_fields[5]}

  [ "$EVIDENCE_TREE" = "$CURRENT_TREE" ] || { fail "evidence_source_tree_mismatch"; return 1; }
  [ "$EVIDENCE_VERSION" = "$VERSION" ] || { fail "evidence_version_mismatch"; return 1; }
  [ "$EVIDENCE_TESTS" = "PASS" ] || { fail "evidence_tests_not_pass"; return 1; }
  [ "$EVIDENCE_PUBLISH" = "PASS" ] || { fail "evidence_publish_not_pass"; return 1; }
  [ "$EVIDENCE_ZIP_SIZE" = "$APPROVED_ZIP_SIZE" ] || { fail "evidence_zip_size_mismatch"; return 1; }
  [ "$EVIDENCE_ZIP_SHA" = "$APPROVED_ZIP_SHA" ] || { fail "evidence_zip_sha_mismatch"; return 1; }
  return 0
}

preflight() {
  echo "===== SPONSOR PRO PUBLISH PREFLIGHT ====="
  echo "mode=$MODE"
  echo "version=$VERSION"
  echo "tag=$TAG"
  echo "run_dir=$RUN_DIR"

  if [ -n "$RESUME_DRAFT_ID" ]; then
    [ "$MODE" = "new" ] || { fail "resume_draft_requires_new_mode"; return 1; }
    printf '%s\n' "$RESUME_DRAFT_ID" | grep -Eq '^[0-9]+$' || { fail "resume_draft_id_invalid"; return 1; }
  fi

  for cmd in git gh python3 sha256sum unzip cmp base64 realpath awk grep sed tee; do
    require_command "$cmd" || return 1
  done

  repo_root=$(git rev-parse --show-toplevel 2>/dev/null)
  [ "$repo_root" = "$EXPECTED_ROOT" ] || { fail "repository_root_mismatch:$repo_root"; return 1; }
  cd "$repo_root" || { fail "cannot_enter_repository_root"; return 1; }

  gh api user --jq '.login' > "$WORK_DIR/github-user.txt"
  if [ "$?" -ne 0 ] || [ ! -s "$WORK_DIR/github-user.txt" ]; then
    fail "github_authentication_failed"
    return 1
  fi
  echo "github_user=$(cat "$WORK_DIR/github-user.txt")"

  git fetch origin --prune
  if [ "$?" -ne 0 ]; then
    fail "git_fetch_failed"
    return 1
  fi

  current_branch=$(git branch --show-current)
  CURRENT_HEAD=$(git rev-parse HEAD)
  origin_main=$(git rev-parse origin/main)
  CURRENT_TREE=$(git rev-parse HEAD^{tree})
  git_state=$(git status --porcelain=v1 --untracked-files=all)

  echo "branch=$current_branch"
  echo "head=$CURRENT_HEAD"
  echo "origin_main=$origin_main"
  echo "source_tree=$CURRENT_TREE"

  [ "$current_branch" = "main" ] || { fail "branch_must_be_main"; return 1; }
  [ "$CURRENT_HEAD" = "$origin_main" ] || { fail "local_main_not_equal_origin_main"; return 1; }
  [ -z "$git_state" ] || { printf '%s\n' "$git_state"; fail "working_tree_not_clean"; return 1; }

  grep -Fqx "[assembly: AssemblyVersion(\"$VERSION\")]" "$ASSEMBLY_REL" || { fail "assembly_version_mismatch"; return 1; }
  grep -Fqx "[assembly: AssemblyFileVersion(\"$VERSION\")]" "$ASSEMBLY_REL" || { fail "assembly_file_version_mismatch"; return 1; }

  load_manifest || return 1

  gh api "repos/$PUBLIC_REPO/contents/$MANIFEST_REL?ref=main" --jq '.content' \
    | tr -d '\n' | base64 -d > "$WORK_DIR/remote-manifest.json"
  if [ "$?" -ne 0 ] || ! cmp -s "$MANIFEST_REL" "$WORK_DIR/remote-manifest.json"; then
    fail "remote_main_manifest_mismatch"
    return 1
  fi

  APPROVED_ZIP=$(realpath "$ZIP_INPUT" 2>/dev/null)
  if [ "$?" -ne 0 ] || [ ! -f "$APPROVED_ZIP" ]; then
    fail "approved_zip_missing:$ZIP_INPUT"
    return 1
  fi
  [ "$(basename "$APPROVED_ZIP")" = "$ASSET_NAME" ] || { fail "approved_zip_name_invalid"; return 1; }

  APPROVED_ZIP_SIZE=$(wc -c < "$APPROVED_ZIP" | tr -d '[:space:]')
  APPROVED_ZIP_SHA=$(sha256sum "$APPROVED_ZIP" | awk '{print $1}')
  APPROVED_EXE_VERSION=$(verify_zip_semantics "$APPROVED_ZIP" "$WORK_DIR/local-extract")
  if [ "$?" -ne 0 ]; then
    return 1
  fi

  echo "approved_zip=$APPROVED_ZIP"
  echo "approved_zip_size=$APPROVED_ZIP_SIZE"
  echo "approved_zip_sha256=$APPROVED_ZIP_SHA"
  echo "approved_exe_file_version=$APPROVED_EXE_VERSION"

  [ "$APPROVED_ZIP_SIZE" = "$MANIFEST_SIZE" ] || { fail "zip_size_does_not_match_manifest"; return 1; }
  [ "$APPROVED_ZIP_SHA" = "$MANIFEST_SHA" ] || { fail "zip_sha_does_not_match_manifest"; return 1; }

  EVIDENCE_INPUT=$(realpath "$EVIDENCE_INPUT" 2>/dev/null)
  if [ "$?" -ne 0 ] || [ ! -f "$EVIDENCE_INPUT" ]; then
    fail "release_evidence_missing"
    return 1
  fi
  load_evidence || return 1

  public_latest=$(gh api "repos/$PUBLIC_REPO/releases/latest" --jq '.tag_name')
  if [ "$?" -ne 0 ] || [ "$public_latest" != "v0.4.6" ]; then
    echo "public_latest=$public_latest"
    fail "public_latest_invariant_failed"
    return 1
  fi
  echo "public_latest=$public_latest"

  public_tag_matches=$(git ls-remote --tags origin \
    "refs/tags/v$VERSION" \
    "refs/tags/$TAG")
  if [ -n "$public_tag_matches" ]; then
    printf '%s\n' "$public_tag_matches"
    fail "public_sponsor_pro_tag_exists"
    return 1
  fi

  gh api "repos/$PRIVATE_REPO" --jq '.full_name' > "$WORK_DIR/private-repo.txt"
  if [ "$?" -ne 0 ] || [ "$(cat "$WORK_DIR/private-repo.txt")" != "$PRIVATE_REPO" ]; then
    fail "private_repository_access_failed"
    return 1
  fi

  release_ids=$(get_release_ids_for_tag)
  if [ "$?" -ne 0 ]; then
    fail "private_release_lookup_failed"
    return 1
  fi
  release_count=$(printf '%s\n' "$release_ids" | awk 'NF {n++} END {print n+0}')

  private_tag_matches=$(git ls-remote --tags "git@github.com:$PRIVATE_REPO.git" "refs/tags/$TAG")
  if [ $? -ne 0 ]; then
    fail "private_tag_lookup_failed"
    return 1
  fi

  if [ "$MODE" = "new" ]; then
    if [ -z "$RESUME_DRAFT_ID" ]; then
      [ "$release_count" -eq 0 ] || { fail "new_mode_release_already_exists"; return 1; }
      [ -z "$private_tag_matches" ] || { fail "new_mode_private_tag_already_exists"; return 1; }
    else
      [ "$release_count" -eq 1 ] || { fail "resume_mode_requires_exactly_one_release"; return 1; }
      RELEASE_ID=$(printf '%s\n' "$release_ids" | awk 'NF {print; exit}')
      [ "$RELEASE_ID" = "$RESUME_DRAFT_ID" ] || { fail "resume_draft_id_mismatch"; return 1; }

      get_private_release_json_by_id "$RELEASE_ID" > "$WORK_DIR/resume-draft.json"
      if [ $? -ne 0 ]; then
        fail "resume_draft_read_failed"
        return 1
      fi

      python3 - "$WORK_DIR/resume-draft.json" "$RELEASE_ID" "$TAG" "$TITLE" "$ASSET_NAME" "$MANIFEST_SIZE" <<'PY'
import json
import sys

path, release_id, tag, title, asset_name, expected_size = sys.argv[1:]
obj = json.load(open(path, encoding="utf-8"))
assets = obj.get("assets", [])

ok = (
    str(obj.get("id")) == release_id
    and obj.get("tag_name") == tag
    and obj.get("name") == title
    and obj.get("draft") is True
    and obj.get("prerelease") is False
    and len(assets) == 1
    and assets[0].get("name") == asset_name
    and assets[0].get("state") == "uploaded"
    and int(assets[0].get("size", -1)) == int(expected_size)
)

raise SystemExit(0 if ok else 1)
PY

      if [ $? -ne 0 ]; then
        fail "resume_draft_state_invalid"
        return 1
      fi

      resume_asset_id=$(python3 - "$WORK_DIR/resume-draft.json" <<'PY_RESUME_ASSET_ID_20260707'
import json
import sys

obj = json.load(open(sys.argv[1], encoding="utf-8"))
assets = obj.get("assets", [])
print(assets[0].get("id", "") if len(assets) == 1 else "")
PY_RESUME_ASSET_ID_20260707
)

      printf '%s\n' "$resume_asset_id" | grep -Eq '^[0-9]+$'
      if [ $? -ne 0 ]; then
        fail "resume_draft_asset_id_invalid"
        return 1
      fi

      resume_asset_zip="$WORK_DIR/resume-draft-$RELEASE_ID-$ASSET_NAME"

      download_private_release_asset_by_id "$resume_asset_id" "$resume_asset_zip"
      if [ $? -ne 0 ]; then
        fail "resume_draft_asset_download_failed"
        return 1
      fi

      resume_asset_size=$(wc -c < "$resume_asset_zip" | tr -d '[:space:]')
      resume_asset_sha=$(sha256sum "$resume_asset_zip" | awk '{print $1}')

      resume_asset_exe_version=$(verify_zip_semantics         "$resume_asset_zip"         "$WORK_DIR/resume-draft-extracted")

      if [ $? -ne 0 ]; then
        return 1
      fi

      [ "$resume_asset_size" = "$APPROVED_ZIP_SIZE" ] || {
        fail "resume_draft_asset_size_mismatch"
        return 1
      }

      [ "$resume_asset_sha" = "$APPROVED_ZIP_SHA" ] || {
        fail "resume_draft_asset_sha_mismatch"
        return 1
      }

      cmp -s "$resume_asset_zip" "$APPROVED_ZIP"

      [ $? -eq 0 ] || {
        fail "resume_draft_asset_not_byte_identical"
        return 1
      }

      echo "resume_draft_id=$RELEASE_ID"
      echo "resume_draft_asset_id=$resume_asset_id"
      echo "resume_draft_asset_size=$resume_asset_size"
      echo "resume_draft_asset_sha256=$resume_asset_sha"
      echo "resume_draft_asset_exe_file_version=$resume_asset_exe_version"
    fi
  else
    [ "$release_count" -eq 1 ] || { fail "correction_mode_requires_exactly_one_release"; return 1; }
    RELEASE_ID=$(printf '%s\n' "$release_ids" | awk 'NF {print; exit}')
    get_private_release_json > "$WORK_DIR/existing-release.json"
    if [ $? -ne 0 ]; then
      fail "existing_release_read_failed"
      return 1
    fi
    existing_asset_count=$(python3 - "$WORK_DIR/existing-release.json" "$ASSET_NAME" <<'PY'
import json
import sys
obj = json.load(open(sys.argv[1], encoding="utf-8"))
name = sys.argv[2]
print(sum(1 for asset in obj.get("assets", []) if asset.get("name") == name))
PY
)
    total_asset_count=$(python3 - "$WORK_DIR/existing-release.json" <<'PY'
import json
import sys
obj = json.load(open(sys.argv[1], encoding="utf-8"))
print(len(obj.get("assets", [])))
PY
)
    [ "$existing_asset_count" -eq 1 ] || { fail "correction_mode_expected_asset_not_unique"; return 1; }
    [ "$total_asset_count" -eq 1 ] || { fail "correction_mode_unrelated_assets_present"; return 1; }
  fi

  echo "preflight_release_count=$release_count"
  echo "RESULT=PASS_SPONSOR_PRO_RELEASE_PREFLIGHT"
  return 0
}

create_release_notes() {
  cat > "$WORK_DIR/release-notes.md" <<EOF
## MultiPingMonitor Sponsor Pro v$VERSION

Sponsor Pro release for MultiPingMonitor v$VERSION.

Artifact contract:

- asset: \`MultiPingMonitor.zip\`
- ZIP entries: exactly one
- executable: \`MultiPingMonitor.exe\`
- packaging source: \`SingleFile.pubxml\`
EOF
}

verify_remote_release_metadata() {
  get_private_release_json > "$WORK_DIR/final-release.json"
  if [ $? -ne 0 ]; then
    fail "final_release_read_failed"
    return 1
  fi

  python3 - "$WORK_DIR/final-release.json" "$TAG" "$TITLE" "$ASSET_NAME" "$MANIFEST_SIZE" <<'PY'
import json
import sys

path, tag, title, asset_name, expected_size = sys.argv[1:]
obj = json.load(open(path, encoding="utf-8"))
errors = []
if obj.get("tag_name") != tag:
    errors.append("tag")
if obj.get("name") != title:
    errors.append("title")
if obj.get("draft") is not False:
    errors.append("draft")
if obj.get("prerelease") is not False:
    errors.append("prerelease")
if not obj.get("published_at"):
    errors.append("published_at")
assets = obj.get("assets", [])
if len(assets) != 1:
    errors.append("asset_count")
else:
    asset = assets[0]
    if asset.get("name") != asset_name:
        errors.append("asset_name")
    if asset.get("state") != "uploaded":
        errors.append("asset_state")
    if int(asset.get("size", -1)) != int(expected_size):
        errors.append("asset_size")
if errors:
    print("remote release metadata mismatch: " + ",".join(errors), file=sys.stderr)
    raise SystemExit(1)
print(obj["id"])
PY
  if [ $? -ne 0 ]; then
    fail "remote_release_metadata_invalid"
    return 1
  fi

  latest_private=$(gh api "repos/$PRIVATE_REPO/releases/latest" --jq '.tag_name')
  if [ $? -ne 0 ] || [ "$latest_private" != "$TAG" ]; then
    echo "private_latest=$latest_private"
    fail "private_latest_invariant_failed"
    return 1
  fi

  latest_public=$(gh api "repos/$PUBLIC_REPO/releases/latest" --jq '.tag_name')
  if [ $? -ne 0 ] || [ "$latest_public" != "v0.4.6" ]; then
    echo "public_latest=$latest_public"
    fail "public_latest_changed"
    return 1
  fi

  public_tag_matches=$(git ls-remote --tags origin \
    "refs/tags/v$VERSION" \
    "refs/tags/$TAG")
  if [ -n "$public_tag_matches" ]; then
    printf '%s\n' "$public_tag_matches"
    fail "public_sponsor_pro_tag_created"
    return 1
  fi

  echo "private_latest=$latest_private"
  echo "public_latest=$latest_public"
  return 0
}

remote_download_verification() {
  rm -rf "$VERIFY_DIR"
  mkdir -p "$VERIFY_DIR"

  gh release download "$TAG" \
    --repo "$PRIVATE_REPO" \
    --pattern "$ASSET_NAME" \
    --dir "$VERIFY_DIR"
  if [ $? -ne 0 ]; then
    fail "remote_asset_download_failed"
    return 1
  fi

  downloaded_zip="$VERIFY_DIR/$ASSET_NAME"
  [ -f "$downloaded_zip" ] || { fail "downloaded_asset_missing"; return 1; }

  downloaded_size=$(wc -c < "$downloaded_zip" | tr -d '[:space:]')
  downloaded_sha=$(sha256sum "$downloaded_zip" | awk '{print $1}')
  downloaded_exe_version=$(verify_zip_semantics "$downloaded_zip" "$VERIFY_DIR/extracted")
  if [ $? -ne 0 ]; then
    return 1
  fi

  [ "$downloaded_size" = "$APPROVED_ZIP_SIZE" ] || { fail "downloaded_size_mismatch"; return 1; }
  [ "$downloaded_sha" = "$APPROVED_ZIP_SHA" ] || { fail "downloaded_sha_mismatch"; return 1; }
  cmp -s "$downloaded_zip" "$APPROVED_ZIP"
  [ $? -eq 0 ] || { fail "downloaded_zip_not_byte_identical"; return 1; }

  echo "downloaded_zip_size=$downloaded_size"
  echo "downloaded_zip_sha256=$downloaded_sha"
  echo "downloaded_exe_file_version=$downloaded_exe_version"
  return 0
}

verify_backend_latest_metadata() {
  backend_latest_url=${MPM_BACKEND_LATEST_URL:-"https://updates.watel.cloud/v1/update/latest"}
  backend_latest_json="$WORK_DIR/backend-latest.json"

  curl -fsS --max-time 30 "$backend_latest_url" > "$backend_latest_json"
  if [ $? -ne 0 ]; then
    fail "backend_latest_request_failed"
    return 1
  fi

  python3 - "$backend_latest_json" "$VERSION" "$TAG" "$ASSET_NAME" "$APPROVED_ZIP_SIZE" "$APPROVED_ZIP_SHA" <<'PY_BACKEND_LATEST'
import json
import sys

path, version, tag, asset_name, expected_size, expected_sha = sys.argv[1:]
obj = json.load(open(path, encoding="utf-8"))
asset = obj.get("asset") or {}

errors = []
if obj.get("status") != "ok":
    errors.append("status")
if obj.get("latestVersion") != version:
    errors.append("latestVersion")
if obj.get("tagName") != tag:
    errors.append("tagName")
if asset.get("name") != asset_name:
    errors.append("asset.name")
try:
    asset_size = int(asset.get("size") or 0)
except (TypeError, ValueError):
    asset_size = 0
if asset_size != int(expected_size):
    errors.append("asset.size")
if str(asset.get("sha256") or "").lower() != expected_sha:
    errors.append("asset.sha256")

if errors:
    print("backend_latest_errors=" + ",".join(errors))
    print("backend_latest_version=" + str(obj.get("latestVersion")))
    print("backend_latest_tag=" + str(obj.get("tagName")))
    print("backend_latest_asset_name=" + str(asset.get("name")))
    print("backend_latest_asset_size=" + str(asset.get("size")))
    print("backend_latest_asset_sha256=" + str(asset.get("sha256")))
    raise SystemExit(1)
PY_BACKEND_LATEST

  if [ $? -ne 0 ]; then
    fail "backend_latest_mismatch"
    return 1
  fi

  echo "backend_latest_url=$backend_latest_url"
  echo "backend_latest_version=$VERSION"
  echo "backend_latest_tag=$TAG"
  echo "backend_latest_asset_name=$ASSET_NAME"
  echo "backend_latest_asset_size=$APPROVED_ZIP_SIZE"
  echo "backend_latest_asset_sha256=$APPROVED_ZIP_SHA"
  return 0
}

prepare_correction_recovery() {
  get_private_release_json > "$RECOVERY_DIR/release-before.json"
  if [ $? -ne 0 ]; then
    fail "cannot_save_existing_release_metadata"
    return 1
  fi

  rm -f "$RECOVERY_DIR/$ASSET_NAME"
  gh release download "$TAG" \
    --repo "$PRIVATE_REPO" \
    --pattern "$ASSET_NAME" \
    --dir "$RECOVERY_DIR"
  if [ $? -ne 0 ]; then
    fail "cannot_download_recovery_asset"
    return 1
  fi

  RECOVERY_ZIP="$RECOVERY_DIR/$ASSET_NAME"
  [ -f "$RECOVERY_ZIP" ] || { fail "recovery_asset_missing"; return 1; }

  recovery_size=$(wc -c < "$RECOVERY_ZIP" | tr -d '[:space:]')
  recovery_sha=$(sha256sum "$RECOVERY_ZIP" | awk '{print $1}')
  echo "recovery_zip_size=$recovery_size"
  echo "recovery_zip_sha256=$recovery_sha"
  return 0
}

rollback_correction() {
  echo "===== CORRECTION ROLLBACK ATTEMPT ====="
  [ -f "$RECOVERY_ZIP" ] || { echo "ROLLBACK=FAILED_RECOVERY_ASSET_MISSING"; return 1; }

  current_json="$WORK_DIR/rollback-current-release.json"
  get_private_release_json > "$current_json"
  if [ $? -eq 0 ]; then
    current_asset_id=$(python3 - "$current_json" "$ASSET_NAME" <<'PY'
import json
import sys
obj = json.load(open(sys.argv[1], encoding="utf-8"))
ids = [str(a["id"]) for a in obj.get("assets", []) if a.get("name") == sys.argv[2]]
print(ids[0] if len(ids) == 1 else "")
PY
)
    if [ -n "$current_asset_id" ]; then
      gh api --method DELETE "repos/$PRIVATE_REPO/releases/assets/$current_asset_id" >/dev/null
    fi
  fi

  gh release upload "$TAG" "$RECOVERY_ZIP" --repo "$PRIVATE_REPO"
  if [ $? -ne 0 ]; then
    echo "ROLLBACK=FAILED_UPLOAD"
    return 1
  fi

  get_private_release_json > "$WORK_DIR/rollback-final-release.json"
  if [ $? -ne 0 ]; then
    echo "ROLLBACK=FAILED_FINAL_READ"
    return 1
  fi

  expected_recovery_size=$(wc -c < "$RECOVERY_ZIP" | tr -d '[:space:]')
  restored_size=$(python3 - "$WORK_DIR/rollback-final-release.json" "$ASSET_NAME" <<'PY'
import json
import sys
obj = json.load(open(sys.argv[1], encoding="utf-8"))
assets = [a for a in obj.get("assets", []) if a.get("name") == sys.argv[2]]
print(assets[0].get("size", "") if len(assets) == 1 and len(obj.get("assets", [])) == 1 else "")
PY
)

  if [ "$restored_size" = "$expected_recovery_size" ]; then
    echo "ROLLBACK=PASS_RECOVERY_ASSET_RESTORED"
    return 0
  fi

  echo "ROLLBACK=FAILED_FINAL_STATE_MISMATCH"
  return 1
}

publish_new_release() {
  create_release_notes

  if [ -n "$RESUME_DRAFT_ID" ]; then
    RELEASE_ID="$RESUME_DRAFT_ID"
    echo "resuming_draft_release_id=$RELEASE_ID"
  else
    create_json="$WORK_DIR/create-release.json"

    gh api --method POST "repos/$PRIVATE_REPO/releases" \
      -f tag_name="$TAG" \
      -f name="$TITLE" \
      -f body="$(cat "$WORK_DIR/release-notes.md")" \
      -F draft=true \
      -F prerelease=false > "$create_json"

    if [ $? -ne 0 ]; then
      fail "draft_release_creation_failed"
      return 1
    fi

    RELEASE_ID=$(python3 - "$create_json" <<'PY'
import json
import sys

print(json.load(open(sys.argv[1], encoding="utf-8"))["id"])
PY
)

    REMOTE_MODIFIED=1
    echo "created_draft_release_id=$RELEASE_ID"

    gh release upload "$TAG" "$APPROVED_ZIP"       --repo "$PRIVATE_REPO"

    if [ $? -ne 0 ]; then
      fail "draft_asset_upload_failed"
      return 1
    fi
  fi

  draft_json="$WORK_DIR/draft-after-upload.json"

  get_private_release_json_by_id "$RELEASE_ID" > "$draft_json"

  if [ $? -ne 0 ]; then
    fail "draft_release_verification_read_failed"
    return 1
  fi

  python3 - "$draft_json" "$RELEASE_ID" "$TAG" "$TITLE" "$ASSET_NAME" "$APPROVED_ZIP_SIZE" <<'PY'
import json
import sys

path, release_id, tag, title, asset_name, expected_size = sys.argv[1:]
obj = json.load(open(path, encoding="utf-8"))
assets = obj.get("assets", [])

ok = (
    str(obj.get("id")) == release_id
    and obj.get("tag_name") == tag
    and obj.get("name") == title
    and obj.get("draft") is True
    and obj.get("prerelease") is False
    and len(assets) == 1
    and assets[0].get("name") == asset_name
    and assets[0].get("state") == "uploaded"
    and int(assets[0].get("size", -1)) == int(expected_size)
)

raise SystemExit(0 if ok else 1)
PY

  if [ $? -ne 0 ]; then
    fail "draft_release_asset_verification_failed"
    return 1
  fi

  gh api --method PATCH     "repos/$PRIVATE_REPO/releases/$RELEASE_ID"     -F draft=false     -F prerelease=false     -f make_latest=true     > "$WORK_DIR/published-release.json"

  if [ $? -ne 0 ]; then
    fail "release_publication_failed"
    return 1
  fi

  REMOTE_MODIFIED=1
  echo "release_published=true"
  return 0
}

publish_correction() {
  prepare_correction_recovery || return 1

  existing_json="$WORK_DIR/existing-release-for-correction.json"
  get_private_release_json > "$existing_json"
  if [ $? -ne 0 ]; then
    fail "correction_release_read_failed"
    return 1
  fi

  RELEASE_ID=$(python3 - "$existing_json" <<'PY'
import json
import sys
print(json.load(open(sys.argv[1], encoding="utf-8"))["id"])
PY
)
  existing_asset_id=$(python3 - "$existing_json" "$ASSET_NAME" <<'PY'
import json
import sys
obj = json.load(open(sys.argv[1], encoding="utf-8"))
assets = [a for a in obj.get("assets", []) if a.get("name") == sys.argv[2]]
print(assets[0]["id"] if len(assets) == 1 and len(obj.get("assets", [])) == 1 else "")
PY
)
  [ -n "$existing_asset_id" ] || { fail "correction_asset_id_unavailable"; return 1; }

  gh api --method DELETE "repos/$PRIVATE_REPO/releases/assets/$existing_asset_id" >/dev/null
  if [ $? -ne 0 ]; then
    fail "correction_old_asset_delete_failed"
    return 1
  fi
  REMOTE_MODIFIED=1

  gh release upload "$TAG" "$APPROVED_ZIP" --repo "$PRIVATE_REPO"
  if [ $? -ne 0 ]; then
    fail "correction_replacement_upload_failed"
    rollback_correction
    return 1
  fi

  echo "correction_asset_replaced=true"
  return 0
}

main() {
  preflight || return 1

  if [ "$PREFLIGHT_ONLY" -eq 1 ]; then
    return 0
  fi

  echo "===== SPONSOR PRO REMOTE TRANSACTION ====="

  if [ "$MODE" = "new" ]; then
    publish_new_release || return 1
  else
    publish_correction || return 1
  fi

  verify_remote_release_metadata
  if [ $? -ne 0 ]; then
    if [ "$MODE" = "correct" ] && [ "$REMOTE_MODIFIED" -eq 1 ]; then
      rollback_correction
    fi
    return 1
  fi

  remote_download_verification
  if [ $? -ne 0 ]; then
    if [ "$MODE" = "correct" ] && [ "$REMOTE_MODIFIED" -eq 1 ]; then
      rollback_correction
    fi
    return 1
  fi

  verify_backend_latest_metadata
  if [ $? -ne 0 ]; then
    if [ "$MODE" = "correct" ] && [ "$REMOTE_MODIFIED" -eq 1 ]; then
      rollback_correction
    fi
    return 1
  fi

  verify_remote_release_metadata
  if [ $? -ne 0 ]; then
    if [ "$MODE" = "correct" ] && [ "$REMOTE_MODIFIED" -eq 1 ]; then
      rollback_correction
    fi
    return 1
  fi

  echo "RESULT=PASS_SPONSOR_PRO_RELEASE_PUBLISHED_AND_VERIFIED"
  return 0
}

main
main_rc=$?

if [ "$main_rc" -ne 0 ]; then
  echo "RESULT=FAIL_SPONSOR_PRO_RELEASE_TRANSACTION"
  echo "failure_reason=$FAIL_REASON"
  echo "run_dir=$RUN_DIR"
fi

exit "$main_rc"
