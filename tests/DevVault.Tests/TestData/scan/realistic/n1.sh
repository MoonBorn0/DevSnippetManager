#!/bin/bash
set -euo pipefail
foo() {
  echo 1
  echo 2
}
source ./lib.sh
bar() {
  echo 3
  echo 4
}
