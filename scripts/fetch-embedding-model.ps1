$ErrorActionPreference = "Stop"

$dest = Join-Path $env:LOCALAPPDATA "DataMan\models"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$model = Join-Path $dest "all-MiniLM-L6-v2.onnx"
$vocab = Join-Path $dest "vocab.txt"
$modelUrl = "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx"
$vocabUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt"
$headers = @{ "User-Agent" = "DataMan-fetch-embedding-model" }

if (-not (Test-Path -LiteralPath $model)) {
    Write-Host "Downloading MiniLM ONNX to $model"
    Invoke-WebRequest -Uri $modelUrl -OutFile $model -Headers $headers -UseBasicParsing
}

if (-not (Test-Path -LiteralPath $vocab)) {
    Write-Host "Downloading MiniLM vocab to $vocab"
    Invoke-WebRequest -Uri $vocabUrl -OutFile $vocab -Headers $headers -UseBasicParsing
}

Write-Host "Embedding model ready in $dest"
