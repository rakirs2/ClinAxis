Never merge or deploy any change that has not been fully verified locally.

Before opening or updating a pull request:
1. Build every affected project locally (e.g., `dotnet build`).
2. Run the relevant automated tests locally (e.g., `dotnet test`).
3. Confirm any additional verification steps described in AGENTS.md are complete.

If local verification cannot be performed, the change must not proceed until the gap is resolved.
