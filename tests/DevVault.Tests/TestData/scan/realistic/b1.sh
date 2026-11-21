#!/bin/bash
foo() {
  local arr=(1 2 3)
  echo ${#arr[@]}
  echo done
}
bar() {
  cat <<EOF
don't panic { here
EOF
  echo a
  echo b
}
baz() {
  echo hi
  echo there
}
