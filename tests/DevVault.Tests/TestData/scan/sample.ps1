# Günlük dosyalarını temizler
function Clear-OldLogs {
    param([string]$Path)
    Get-ChildItem -Path $Path -Filter *.log
}
