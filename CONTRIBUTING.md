# Contributing

Contributions are welcome through GitHub issues and pull requests.

By intentionally submitting a contribution, you agree that it is provided
under Apache License 2.0 unless the file is explicitly governed by another
license. Do not submit code, art, audio, fonts, data, screenshots, or other
material that you do not have the right to redistribute.

Before opening a pull request:

1. Read `AGENTS.md` and the applicable rules under `.agents/`.
2. Run `Tools/godot/Verify-GodotProject.ps1`.
3. Add new media or binary files to
   `Tools/public-release/asset-provenance.json` with verifiable provenance.
4. Update `THIRD_PARTY_NOTICES.md` and the dependency manifest for new
   third-party dependencies.
5. Keep gameplay, visual, and Editor manual QA status explicit; automated
   tests do not convert a human acceptance item to passed.

The agent-workflow map at `.agents/docs/agent-workflow-infrastructure.md`
summarizes the repository's agent tooling (eval kit, dispatch recipes,
environment guards) and where each piece applies.

Security reports must follow `SECURITY.md` rather than a public issue.
