cd src/CodeIndex.API
dotnet user-secrets init
dotnet user-secrets set "OpenAI:ApiKey" "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"
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=CodeIndexDb;Username=admin;Password=Adminp@ss2025!;SSL Mode=Disable;"
dotnet user-secrets set "OpenAI:Project" "proj_qd3qMud0Db4twUZqGtNiBNRE"