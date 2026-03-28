# START_HERE

This is the fastest orientation page for contributors working in the DINBoard
repository today.

## Read These First

1. [QUICK_START.md](./QUICK_START.md)
2. [AGENTS.md](./AGENTS.md)
3. [AI_CONTEXT.md](./AI_CONTEXT.md)
4. [ARCHITECTURE_MAP.md](./ARCHITECTURE_MAP.md)
5. [CONTRIBUTING.md](./CONTRIBUTING.md)
6. [CODE_QUALITY.md](./CODE_QUALITY.md)

If you are going to change code in a critical area, do not skip the architecture
documents.

## What Is In This Repo

- desktop app: `DINBoard.csproj`
- tests: `Tests/Avalonia.Tests.csproj`
- benchmarks: `Benchmarks/Benchmarks.csproj`
- current passing automated tests in the suite: `314`

## Fastest Useful Path

1. Restore packages:

```powershell
dotnet restore Avalonia.sln
```

2. Build the app:

```powershell
dotnet build DINBoard.csproj
```

3. Run tests:

```powershell
dotnet test Tests\Avalonia.Tests.csproj --no-restore
```

4. Launch the app:

```powershell
dotnet run --project DINBoard.csproj
```

5. Validate a release candidate:

```powershell
.\scripts\Validate-Release.ps1
```

Then complete [RELEASE_CHECKLIST.md](./RELEASE_CHECKLIST.md).

## If You Only Have 15 Minutes

Do this in order:
- read [QUICK_START.md](./QUICK_START.md)
- skim [AI_CONTEXT.md](./AI_CONTEXT.md)
- skim [ARCHITECTURE_MAP.md](./ARCHITECTURE_MAP.md)
- run the test suite once
- open the app once

That gives you enough context to make a small safe change.

## Current Quality System

The repo already has:
- analyzers enabled in `DINBoard.csproj`
- `.editorconfig`
- `.stylecop.json`
- architecture documents
- broad test coverage

The repo does not currently contain:
- `.github` workflow definitions
- `.githooks` scripts
- extra `docs/` process material referenced by older drafts

So the current workflow is pragmatic:
- use the existing docs
- keep changes small
- run the relevant tests manually
- run the release validation script before shipping
- keep documentation aligned with reality

## Where To Go Next

- development workflow: [QUICK_START.md](./QUICK_START.md)
- architecture: [ARCHITECTURE_MAP.md](./ARCHITECTURE_MAP.md)
- anti-chaos rules: [PREVENTING_CODE_MESS.md](./PREVENTING_CODE_MESS.md)
- coding conventions: [CONTRIBUTING.md](./CONTRIBUTING.md)
- quality overview: [CODE_QUALITY.md](./CODE_QUALITY.md)

## First Safe Contribution

A good first contribution in this repo is usually one of these:
- add or improve a targeted test
- extract a single local responsibility
- update stale documentation
- fix a UI-only issue without touching electrical or persistence logic

Avoid broad cross-cutting refactors until you know which subsystem you are in.
