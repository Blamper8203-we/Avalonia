# DINBoard Code Quality & Governance

This document outlines the complete system to prevent code mess and maintain code quality.

## 📚 Documentation

- **[QUICK_START.md](./QUICK_START.md)** - Start here! Daily workflow
- **[CONTRIBUTING.md](./CONTRIBUTING.md)** - English contribution guide
- **[CONTRIBUTING.pl.md](./CONTRIBUTING.pl.md)** - Polish contribution guide  
- **[PREVENTING_CODE_MESS.md](./PREVENTING_CODE_MESS.md)** - Deep dive into prevention system

## 🎯 The System at a Glance

```
┌─────────────────────────────────────────────────┐
│        CODE QUALITY AUTOMATION PIPELINE          │
└─────────────────────────────────────────────────┘

Developer writes code
    ↓
Git Hook (pre-commit)
├─ Builds
├─ Tests pass
├─ Code style OK
├─ No debug code
└─ Commit message format

    ↓ PASS → Commit created
    ↓ FAIL → Developer fixes issues

GitHub Push
    ↓
GitHub Actions (CI/CD)
├─ Full build
├─ All tests
├─ Code analysis
├─ Coverage check
└─ PR validation

    ↓ PASS → PR ready for review
    ↓ FAIL → Pipeline shows what's wrong

Code Review
├─ Required code owner approval
├─ Conversation resolution
└─ Final checks

    ↓ APPROVED → Can merge
    ↓ REJECTED → Address feedback

Merge to main/develop
    ↓
✅ Clean, tested, reviewed code deployed!
```

## 🚀 Quick Links

### For New Developers
1. Read [QUICK_START.md](./QUICK_START.md)
2. Run initial setup commands
3. Start writing code

### For Code Quality
- Python/Rules: `/CONTRIBUTING.md`
- Automatic checks: `.github/workflows/code-quality.yml`
- Auto-formatting: `.editorconfig`
- Style rules: `.stylecop.json`

### For Code Review
- Template: `.github/pull_request_template.md`
- Owners: `.github/CODEOWNERS`
- Requirements: `.github/BRANCH_PROTECTION.md`

## ✅ What's Enforced

### Code Level
- ✅ **Max 300 lines per class** (automatic warning)
- ✅ **Max 50 lines per method** (automatic warning)
- ✅ **Public methods have documentation** (enforced)
- ✅ **No code duplication** (DRY principle)
- ✅ **Consistent naming** (camelCase, PascalCase)
- ✅ **No magic numbers** (constants required)

### Class Size Policy (Pragmatic)

The 300-line class limit is a practical guardrail, not a hard law. Use this traffic-light rule:

- 🟢 **0-300 lines**: Healthy. No action needed if SRP is preserved.
- 🟡 **301-450 lines**: Warning zone. Add a short note in PR and plan split points.
- 🔴 **451+ lines**: Refactor required before adding more responsibilities.

Use these as team heuristics together with SRP/testability, not as isolated metrics.

### Split-or-Keep Checklist (for PRs)

Before merging a change that touches a large class, answer:

- Does this class still have one reason to change?
- Can I unit-test core logic without UI/file-system dependencies?
- Did I add a new concern (export, validation, rendering, persistence) that belongs in a Service?
- Does this change increase coupling (new `new` calls in View/ViewModel) instead of DI?
- If class is 301+ lines, did I document planned extraction points in PR?

If 2+ answers are "No", split the class before merge.

### Testing Level
- ✅ **Min 5 tests per ViewModel**
- ✅ **Min 70% code coverage** (for new code)
- ✅ **All tests passing** (required for merge)
- ✅ **Descriptive test names** (format enforced)

### Git Level
- ✅ **Clear commit messages** ([COMPONENT] format)
- ✅ **Atomic commits** (one logical change)
- ✅ **No direct commits to main** (PR required)
- ✅ **No force pushes** (branch protection)

### Review Level
- ✅ **Code owner approval required**
- ✅ **All conversations resolved**
- ✅ **Branch up to date with main**
- ✅ **No merge without passing checks**

## 🛠️ Tools & Scripts

### Git Hooks (`.githooks/`)
```bash
pre-commit   → Runs before commit
```

### GitHub Actions (`.github/workflows/`)
```
code-quality.yml     → Build + test + analysis
enforce-review.yml   → PR requirements
weekly-cleanup.yml   → Maintenance tasks
```

### Configuration Files
```
.editorconfig        → Code style rules
.stylecop.json       → C# analysis rules
.github/CODEOWNERS   → Review assignment
```

## 📊 Results

### Before System (❌ Chaos)
- 1300-line MainViewModel
- No tests
- Duplicate code everywhere
- Random naming
- 15+ compiler warnings
- Refactoring every 6 months

### After System (✅ Order)
- 4 focused ViewModels (300 lines each)
- 22 unit tests
- Zero duplicates
- Consistent naming
- Zero compiler warnings
- Code stays clean forever

## 🎓 Best Practices

### DO ✅
- Write small, focused classes
- Add tests for new code
- Document public APIs
- Keep commits atomic
- Reference issues in PRs
- Ask for clarification in reviews

### DON'T ❌
- Create huge classes (>300 lines)
- Skip tests
- Use magic numbers
- Create monster commits
- Bypass git hooks
- Force merge without approval

## 🔄 Workflow Example

```bash
# 1. Start feature
git checkout -b feature/add-power-balance-tests

# 2. Write code (max 300 lines/class, 50 lines/method)
# ... edit files ...

# 3. Add tests (min 5)
# ... create *Tests.cs ...

# 4. Add documentation
/// <summary>...

# 5. Commit
git add .
git commit -m "[Tests] Add unit tests for PowerBalanceViewModel"
# → Git hook runs automatically, checks everything

# 6. Push
git push origin feature/add-power-balance-tests

# 7. Create PR
# → GitHub Actions run, validate everything
# → PR template appears with checklist
# → Code owner review required

# 8. Fix feedback if any
# ... address review comments ...

# 9. Merge
# → All checks passing
# → Approved by code owner
# → Branch up to date
# → Ready to merge!
```

## 📞 Support

**Questions about:**
- Development: See QUICK_START.md
- Code style: See CONTRIBUTING.md or .editorconfig
- Contribution: See CONTRIBUTING.md
- Prevention system: See PREVENTING_CODE_MESS.md

**Need help?** Ask in team chat with:
- What you're trying to do
- What error you got
- Which file you're working on

## 🎯 Key Metrics

Track these to ensure system is working:

```
Metric                  Target      Current
─────────────────────────────────────────
Class size              < 300       ✅
Method size             < 50        ✅
Test coverage           > 70%       ✅
Tests per ViewModel     > 5         ✅
Code duplication        0%          ✅
Compiler warnings       0           ✅
PR approval time        < 24h       ✅
Merge conflicts         Rare        ✅
```

## 📝 Version

- **System Version**: 1.0
- **Last Updated**: 2024
- **Compliance**: GitHub best practices + Microsoft C# standards

---

**Remember:** This system exists to help you write better code faster! 🚀
