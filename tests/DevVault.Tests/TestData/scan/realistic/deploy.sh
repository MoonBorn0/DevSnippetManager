#!/usr/bin/env bash
set -euo pipefail

# Prints usage
usage() {
  echo "usage: $0 <env>"
  echo "  env: dev|prod"
  exit 1
}

function log {
  local level="$1"; shift
  printf '[%s] %s\n' "$level" "$*"
  echo "it's logged"
}

count_args() {
  local arr=("$@")
  echo "${#arr[@]} args"
  echo "last: ${arr[-1]}"
}

write_config() {
  cat > /tmp/app.conf <<EOF
server {
  listen 80;
  name "it's a conf" }
EOF
  echo "written"
  echo "done"
}

strip_prefix() {
  local v="$1"
  echo "${v#prefix-}"
  echo "${v%%.*}"
}

check() {
  if [ $# -lt 1 ]; then
    usage
  fi
  case "$1" in
    dev) echo dev ;;
    *) echo other ;;
  esac
}

main() {
  check "$@"
  log INFO "start"
  count_args "$@"
}

main "$@"
