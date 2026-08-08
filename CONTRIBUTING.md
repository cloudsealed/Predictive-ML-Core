# Contributing to Predictive-ML-Core

Thank you for your interest in contributing! This project is maintained as a community resource for learning and using predictive analytics in cloud infrastructure.

## Before You Contribute

Please read [COMMUNITY.md](COMMUNITY.md) to understand the project's purpose and boundaries.

## Contribution Guidelines

### Types of Contributions We Accept ✅

- **Bug fixes** in existing code
- **Performance improvements** with synthetic data benchmarks
- **Documentation updates** (clarifications, examples, typos)
- **Example scenarios** using synthetic/public data
- **Tests** with synthetic telemetry data
- **Algorithm improvements** (if generalizable to any infrastructure)

### Types We Don't Accept ❌

- Real production telemetry or logs
- Customer data or anonymized customer data without explicit consent
- CloudSealed-specific implementations
- Credentials, API keys, or tokens in any form
- Changes to remove/modify MIT license

## Process

1. **Check existing issues/PRs** to avoid duplicates
2. **Open an issue first** for significant changes (discuss before implementing)
3. **Fork and branch**: `git checkout -b feature/your-feature-name`
4. **Keep data synthetic**: All examples use generated data
5. **Update docs**: Explain what your change does
6. **Add tests**: Use synthetic data; all tests must be reproducible
7. **Run locally**: Ensure `dotnet build` and tests pass
8. **Create PR**: Link the issue, describe changes clearly

## Data Policy

**You MUST NOT commit**:
- Real telemetry or logs
- Customer/company-specific data (even anonymized)
- Cloud credentials (keys, tokens, connection strings)
- Production configuration

**You CAN use**:
- Synthetic data generated with `Random` seeded values
- Public datasets (cite source, respect license)
- Realistic but fake scenarios (e.g., "Company A" with made-up metrics)

### Quick Check Before Committing

```bash
# Ensure no secrets are committed
git-secrets --scan

# Verify no real company/customer names in data
grep -r "YOUR_COMPANY\|customer\|client" src/
```

## Code Style

- Follow standard C# conventions (PascalCase for public members)
- XML comments for public APIs
- Meaningful variable names
- Keep functions focused and testable

## Documentation

When adding a feature:
1. Update README.md if it's user-facing
2. Add inline comments explaining the "why"
3. Include example usage in docstrings
4. Document inputs/outputs clearly

## Testing

- Write tests for any new algorithms
- Use synthetic data only
- Tests must be reproducible (fixed seeds)
- Verify tests pass: `dotnet test`

## Licensing

By contributing, you agree that your work will be distributed under the MIT license. You confirm you have the right to contribute and that you're not violating any third-party licenses.

## Questions?

- Check [COMMUNITY.md](COMMUNITY.md) FAQ
- Open an issue with `[question]` tag
- Review existing PRs for examples

Thank you for contributing to the community! 🙏
