# Set for current PowerShell session only
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=CodeIndexDb;Username=admin;Password=YOUR_PASS;SSL Mode=Disable;"
$env:OpenAI__ApiKey = "sk-REPLACE"
$env:OpenAI__BaseUrl = "https://api.openai.com/v1"
$env:OpenAI__AssistantId = ""      # optional; empty = auto-create in code
$env:OpenAI__Model = "gpt-4.1"
$env:FeatureFlags__UsePostgres = "true"
$env:FeatureFlags__HangfirePostgres = "false"

Write-Host "Environment variables set for this session."
