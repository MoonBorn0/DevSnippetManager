if [ $# -lt 1 ]; then echo x; fi
foo() {
  echo "$#"
  echo 1
}
bar() {
  if [[ $# -eq 0 ]]; then echo y; fi
  echo 2
}
