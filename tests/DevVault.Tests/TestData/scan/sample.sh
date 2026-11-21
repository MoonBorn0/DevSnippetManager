#!/bin/bash

# Yedek arşivi oluşturur
backup_dir() {
    local src="$1"
    tar -czf backup.tar.gz "$src"
}

function deploy_app {
    echo "deploying"
    return 0
}
