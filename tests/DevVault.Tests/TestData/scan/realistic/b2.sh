#!/bin/bash
foo() {
  local arr=(1 2 3)
  echo ${#arr[@]}
  echo done
}
baz() {
  echo hi
  echo there
}
