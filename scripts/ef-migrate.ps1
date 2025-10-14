param([string]$Name="Init")

# # Ensure local dotnet-ef tool is present
# dotnet new tool-manifest --force | Out-Null
# dotnet tool install dotnet-ef --version 8.* --create-manifest-if-needed | Out-Null

# # Make sure the startup project has the Design package
# dotnet add src/CodeIndex.API package Microsoft.EntityFrameworkCore.Design --version 8.*

# # If you're on Postgres locally, ensure the provider is available in startup as well (harmless if already installed)
# dotnet add src/CodeIndex.API package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.*

dotnet restore
dotnet build

# Create & apply migration
dotnet ef migrations add $Name -p src/CodeIndex.Infrastructure/ -s src/CodeIndex.API/
dotnet ef database update -p src/CodeIndex.Infrastructure/ -s src/CodeIndex.API/
