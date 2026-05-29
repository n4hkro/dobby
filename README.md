# Dobby

Dobby is a Windows desktop host platform for separately installed business plugins.

Canonical product, architecture, stack, acceptance, and release guidance lives in
`docs/`. This repository root contains only the buildable skeleton, local developer
commands, and implementation-owned source/test paths.

## Local Commands

```powershell
dotnet restore .\Dobby.sln
dotnet build .\Dobby.sln -c Debug --no-restore
dotnet test .\Dobby.sln -c Debug --no-build
dotnet format .\Dobby.sln --verify-no-changes
dotnet run --project .\src\Dobby.Host\Dobby.Host.csproj -c Debug
```

The workspace is intentionally not initialized as a git repository by the skeleton.
That remains a user/workspace bootstrap action.
