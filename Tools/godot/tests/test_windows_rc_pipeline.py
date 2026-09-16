import json
import pathlib
import shutil
import struct
import subprocess
import tempfile
import unittest


REPO = pathlib.Path(__file__).resolve().parents[3]
TOOLS = REPO / "Tools" / "godot"
POWERSHELL = shutil.which("pwsh") or shutil.which("powershell")


def run_pwsh(script: pathlib.Path, *args: str, cwd: pathlib.Path | None = None):
    if POWERSHELL is None:
        raise RuntimeError("PowerShell executable was not found.")
    return subprocess.run(
        [POWERSHELL, "-NoProfile", "-File", str(script), *args],
        cwd=cwd or REPO,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    )


class WindowsRcPipelineTests(unittest.TestCase):
    def test_powershell_scripts_parse(self):
        scripts = [
            TOOLS / "New-GodotOwnedRcSource.ps1",
            TOOLS / "Test-GodotWindowsPackage.ps1",
            TOOLS / "Test-GodotWindowsLaunch.ps1",
            TOOLS / "Build-GodotWindows.ps1",
            REPO / "Tools" / "public-release" / "New-PublicRootCandidate.ps1",
        ]
        for script in scripts:
            command = (
                "$errors=$null; [void][System.Management.Automation.Language.Parser]::ParseFile("
                f"'{script}', [ref]$null, [ref]$errors); "
                "if($errors.Count){$errors|%{$_.Message};exit 1}"
            )
            result = subprocess.run(
                [POWERSHELL, "-NoProfile", "-Command", command],
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
            )
            self.assertEqual(0, result.returncode, f"{script}: {result.stdout}")

    def test_public_source_staging_records_byte_identical_hashes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            source = root / "source"
            destination = root / "stage"
            (source / "godot").mkdir(parents=True)
            (source / "Tools" / "public-release").mkdir(parents=True)
            (source / "godot" / "project.godot").write_text(
                'enabled=PackedStringArray("res://addons/tactics_tooling/plugin.cfg")\n',
                encoding="utf-8",
            )
            (source / "Tactics.Godot.slnx").write_text(
                '<Solution><Project Path="godot/Tactics.Godot.Adapter.csproj" /></Solution>\n',
                encoding="utf-8",
            )
            (source / "Tools" / "public-release" / "validate_public_candidate.py").write_text(
                "raise SystemExit(0)\n", encoding="utf-8"
            )
            subprocess.run(["git", "init", "-q"], cwd=source, check=True)
            subprocess.run(["git", "config", "user.name", "test"], cwd=source, check=True)
            subprocess.run(["git", "config", "user.email", "test@invalid"], cwd=source, check=True)
            subprocess.run(["git", "add", "."], cwd=source, check=True)
            subprocess.run(["git", "commit", "-qm", "fixture"], cwd=source, check=True)

            result = run_pwsh(
                TOOLS / "New-GodotOwnedRcSource.ps1",
                "-SourceRoot", str(source),
                "-DestinationRoot", str(destination),
                "-InitializeGit",
            )
            self.assertEqual(0, result.returncode, result.stdout)
            project = (destination / "godot" / "project.godot").read_text(encoding="utf-8")
            self.assertNotIn("godot_ai", project)
            manifest = json.loads((destination / "rc-source-manifest.json").read_text(encoding="utf-8-sig"))
            self.assertEqual("public-source-byte-identical-v1", manifest["boundary"])
            self.assertEqual(3, manifest["fileCount"])
            self.assertEqual(
                sorted([
                    "Tactics.Godot.slnx",
                    "godot/project.godot",
                    "Tools/public-release/validate_public_candidate.py",
                ], key=str.casefold),
                sorted([entry["path"] for entry in manifest["files"]], key=str.casefold),
            )
            self.assertTrue(all(entry["sourceSha256"] == entry["stagedSha256"] for entry in manifest["files"]))
            status = subprocess.run(
                ["git", "status", "--porcelain"], cwd=destination, check=True,
                text=True, stdout=subprocess.PIPE
            ).stdout
            self.assertEqual("", status)

    def test_rc_source_rejects_assume_unchanged_worktree_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            source = root / "source"
            destination = root / "stage"
            (source / "godot").mkdir(parents=True)
            (source / "Tools" / "public-release").mkdir(parents=True)
            project = source / "godot" / "project.godot"
            project.write_text("[application]\n", encoding="utf-8")
            (source / "Tactics.Godot.slnx").write_text("<Solution />\n", encoding="utf-8")
            (source / "Tools" / "public-release" / "validate_public_candidate.py").write_text(
                "raise SystemExit(0)\n", encoding="utf-8"
            )
            subprocess.run(["git", "init", "-q"], cwd=source, check=True)
            subprocess.run(["git", "config", "user.name", "test"], cwd=source, check=True)
            subprocess.run(["git", "config", "user.email", "test@invalid"], cwd=source, check=True)
            subprocess.run(["git", "add", "."], cwd=source, check=True)
            subprocess.run(["git", "commit", "-qm", "fixture"], cwd=source, check=True)
            subprocess.run(["git", "update-index", "--assume-unchanged", "godot/project.godot"],
                           cwd=source, check=True)
            project.write_text("[application]\nconfig/name=\"tampered\"\n", encoding="utf-8")
            status = subprocess.run(
                ["git", "status", "--porcelain=v1", "--untracked-files=no"], cwd=source,
                check=True, text=True, stdout=subprocess.PIPE,
            ).stdout
            self.assertEqual("", status)

            result = run_pwsh(
                TOOLS / "New-GodotOwnedRcSource.ps1", "-SourceRoot", str(source),
                "-DestinationRoot", str(destination),
            )

            self.assertNotEqual(0, result.returncode)
            self.assertIn("differs from its pinned Git object", result.stdout)

    def test_rc_source_materializes_only_the_pinned_clean_submodule(self):
        workflow = (REPO / ".github" / "workflows" / "godot-windows-build.yml").read_text(encoding="utf-8")
        self.assertIn("submodules: recursive", workflow)
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            submodule = root / "maliang"
            source = root / "source"
            destination = root / "stage"
            bad_destination = root / "bad-stage"
            for repository in (submodule, source):
                repository.mkdir()
                subprocess.run(["git", "init", "-q"], cwd=repository, check=True)
                subprocess.run(["git", "config", "user.name", "test"], cwd=repository, check=True)
                subprocess.run(["git", "config", "user.email", "test@invalid"], cwd=repository, check=True)
            (submodule / "payload.txt").write_text("pinned payload", encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=submodule, check=True)
            subprocess.run(["git", "commit", "-qm", "pinned"], cwd=submodule, check=True)
            (source / "godot").mkdir()
            (source / "Tools" / "public-release").mkdir(parents=True)
            (source / "godot" / "project.godot").write_text("[application]\n", encoding="utf-8")
            (source / "Tactics.Godot.slnx").write_text("<Solution />\n", encoding="utf-8")
            (source / "Tools" / "public-release" / "validate_public_candidate.py").write_text(
                "raise SystemExit(0)\n", encoding="utf-8"
            )
            subprocess.run(
                ["git", "-c", "protocol.file.allow=always", "submodule", "add", "-q", str(submodule),
                 "Tools/vendor/maliang"], cwd=source, check=True
            )
            subprocess.run(["git", "add", "."], cwd=source, check=True)
            subprocess.run(["git", "commit", "-qm", "fixture"], cwd=source, check=True)
            result = run_pwsh(
                TOOLS / "New-GodotOwnedRcSource.ps1", "-SourceRoot", str(source),
                "-DestinationRoot", str(destination),
            )
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertEqual("pinned payload", (destination / "Tools/vendor/maliang/payload.txt").read_text(encoding="utf-8"))

            (submodule / "payload.txt").write_text("unpinned payload", encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=submodule, check=True)
            subprocess.run(["git", "commit", "-qm", "unpinned"], cwd=submodule, check=True)
            subprocess.run(["git", "fetch", "-q", str(submodule), "HEAD"], cwd=source / "Tools/vendor/maliang", check=True)
            subprocess.run(["git", "checkout", "-q", "FETCH_HEAD"], cwd=source / "Tools/vendor/maliang", check=True)
            subprocess.run(["git", "config", "submodule.Tools/vendor/maliang.ignore", "all"], cwd=source, check=True)
            result = run_pwsh(
                TOOLS / "New-GodotOwnedRcSource.ps1", "-SourceRoot", str(source),
                "-DestinationRoot", str(bad_destination),
            )
            self.assertNotEqual(0, result.returncode)
            self.assertIn("not at its pinned commit", result.stdout)

    def test_public_root_reconstruction_preserves_ignored_tracked_files(self):
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            source = root / "source"
            destination = root / "public-root"
            validator = source / "Tools" / "public-release" / "validate_public_candidate.py"
            validator.parent.mkdir(parents=True)
            (source / ".gitignore").write_text("*.slnx\n", encoding="utf-8")
            (source / "Tactics.Godot.slnx").write_text("<Solution />\n", encoding="utf-8")
            validator.write_text("raise SystemExit(0)\n", encoding="utf-8")
            subprocess.run(["git", "init", "-q"], cwd=source, check=True)
            subprocess.run(["git", "config", "user.name", "test"], cwd=source, check=True)
            subprocess.run(["git", "config", "user.email", "test@invalid"], cwd=source, check=True)
            subprocess.run(["git", "add", ".gitignore", str(validator.relative_to(source))], cwd=source, check=True)
            subprocess.run(["git", "add", "--force", "Tactics.Godot.slnx"], cwd=source, check=True)
            subprocess.run(["git", "commit", "-qm", "fixture"], cwd=source, check=True)

            result = run_pwsh(
                REPO / "Tools" / "public-release" / "New-PublicRootCandidate.ps1",
                "-SourceRoot", str(source),
                "-DestinationRoot", str(destination),
            )
            self.assertEqual(0, result.returncode, result.stdout)
            tracked = subprocess.run(
                ["git", "ls-files"], cwd=destination, check=True,
                text=True, stdout=subprocess.PIPE,
            ).stdout.splitlines()
            self.assertIn("Tactics.Godot.slnx", tracked)
            self.assertEqual(3, len(tracked))
            self.assertEqual("1", subprocess.run(
                ["git", "rev-list", "--count", "HEAD"], cwd=destination, check=True,
                text=True, stdout=subprocess.PIPE,
            ).stdout.strip())

    def test_public_root_reconstruction_expands_pinned_submodule(self):
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            submodule = root / "vendor"
            source = root / "source"
            destination = root / "public-root"
            for repository in (submodule, source):
                repository.mkdir()
                subprocess.run(["git", "init", "-q"], cwd=repository, check=True)
                subprocess.run(["git", "config", "user.name", "test"], cwd=repository, check=True)
                subprocess.run(["git", "config", "user.email", "test@invalid"], cwd=repository, check=True)
            (submodule / "sample.png").write_bytes(b"sample")
            subprocess.run(["git", "add", "."], cwd=submodule, check=True)
            subprocess.run(["git", "commit", "-qm", "vendor fixture"], cwd=submodule, check=True)
            validator = source / "Tools/public-release/validate_public_candidate.py"
            validator.parent.mkdir(parents=True)
            validator.write_text("raise SystemExit(0)\n", encoding="utf-8")
            subprocess.run(
                ["git", "-c", "protocol.file.allow=always", "submodule", "add", "-q",
                 str(submodule), "Tools/vendor/example"], cwd=source, check=True,
            )
            subprocess.run(["git", "add", "."], cwd=source, check=True)
            subprocess.run(["git", "commit", "-qm", "source fixture"], cwd=source, check=True)

            result = run_pwsh(
                REPO / "Tools/public-release/New-PublicRootCandidate.ps1",
                "-SourceRoot", str(source), "-DestinationRoot", str(destination),
            )

            self.assertEqual(0, result.returncode, result.stdout)
            self.assertEqual(b"sample", (destination / "Tools/vendor/example/sample.png").read_bytes())
            tracked = subprocess.run(
                ["git", "ls-files"], cwd=destination, check=True,
                text=True, stdout=subprocess.PIPE,
            ).stdout.splitlines()
            self.assertIn("Tools/vendor/example/sample.png", tracked)
            self.assertNotIn("Tools/vendor/example", tracked)

    def test_package_audit_writes_manifests_and_rejects_unity_payload(self):
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            package = root / "package"
            package.mkdir()
            pe = bytearray(256)
            struct.pack_into("<H", pe, 0, 0x5A4D)
            struct.pack_into("<I", pe, 0x3C, 0x80)
            struct.pack_into("<I", pe, 0x80, 0x00004550)
            struct.pack_into("<H", pe, 0x84, 0x8664)
            (package / "Tactics.exe").write_bytes(pe)
            (package / "Tactics.pck").write_bytes(b"PCK")
            (package / "Tactics.dll").write_bytes(b"managed")
            source_manifest = root / "source.json"
            source_manifest.write_text('{"schemaVersion":1}', encoding="utf-8")

            result = run_pwsh(
                TOOLS / "Test-GodotWindowsPackage.ps1",
                "-PackageRoot", str(package),
                "-SourceManifestPath", str(source_manifest),
                "-SourceCommit", "a" * 40,
                "-GodotVersion", "4.7.1.stable.mono",
                "-DotnetSdk", "9.0.312",
            )
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertTrue((package / "rc-semantic-manifest.json").is_file())
            self.assertTrue((package / "rc-manifest.json").is_file())
            self.assertTrue((package / "SHA256SUMS.txt").is_file())
            release_semantic = json.loads(
                (package / "rc-semantic-manifest.json").read_text(encoding="utf-8-sig")
            )
            self.assertEqual("release", release_semantic["exportMode"])
            self.assertEqual("ExportRelease", release_semantic["configuration"])

            (package / "Tactics.dll").unlink()
            embedded = run_pwsh(
                TOOLS / "Test-GodotWindowsPackage.ps1",
                "-PackageRoot", str(package),
                "-SourceManifestPath", str(source_manifest),
                "-ManagedPayloadMode", "PckEmbedded",
            )
            self.assertEqual(0, embedded.returncode, embedded.stdout)
            semantic = json.loads((package / "rc-semantic-manifest.json").read_text(encoding="utf-8-sig"))
            self.assertEqual("PckEmbedded", semantic["managedPayloadMode"])

            (package / "UnityEngine.CoreModule.dll").write_bytes(b"forbidden")
            rejected = run_pwsh(
                TOOLS / "Test-GodotWindowsPackage.ps1",
                "-PackageRoot", str(package),
                "-SourceManifestPath", str(source_manifest),
            )
            self.assertNotEqual(0, rejected.returncode)
            self.assertIn("Forbidden RC payload", rejected.stdout)

    def test_debug_package_records_mode_and_allows_symbols(self):
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            package = root / "package"
            package.mkdir()
            pe = bytearray(256)
            struct.pack_into("<H", pe, 0, 0x5A4D)
            struct.pack_into("<I", pe, 0x3C, 0x80)
            struct.pack_into("<I", pe, 0x80, 0x00004550)
            struct.pack_into("<H", pe, 0x84, 0x8664)
            (package / "Tactics.exe").write_bytes(pe)
            (package / "Tactics.pck").write_bytes(b"PCK")
            (package / "Tactics.dll").write_bytes(b"managed")
            (package / "Tactics.pdb").write_bytes(b"symbols")
            source_manifest = root / "source.json"
            source_manifest.write_text('{"schemaVersion":1}', encoding="utf-8")

            result = run_pwsh(
                TOOLS / "Test-GodotWindowsPackage.ps1",
                "-PackageRoot", str(package),
                "-SourceManifestPath", str(source_manifest),
                "-ExportMode", "Debug",
                "-Configuration", "ExportDebug",
            )
            self.assertEqual(0, result.returncode, result.stdout)
            semantic = json.loads(
                (package / "rc-semantic-manifest.json").read_text(encoding="utf-8-sig")
            )
            self.assertEqual("debug", semantic["exportMode"])
            self.assertEqual("ExportDebug", semantic["configuration"])

            release = run_pwsh(
                TOOLS / "Test-GodotWindowsPackage.ps1",
                "-PackageRoot", str(package),
                "-SourceManifestPath", str(source_manifest),
                "-ExportMode", "Release",
                "-Configuration", "ExportRelease",
            )
            self.assertNotEqual(0, release.returncode)
            self.assertIn("Tactics.pdb", release.stdout)

    def test_workflow_is_internal_read_only_and_uploads_bounded_artifacts(self):
        workflow = (REPO / ".github" / "workflows" / "godot-windows-build.yml").read_text(
            encoding="utf-8"
        )
        self.assertIn("workflow_dispatch:", workflow)
        self.assertNotIn("  push:", workflow)
        self.assertIn("build_flavor:", workflow)
        self.assertIn("default: both", workflow)
        self.assertIn("-BuildFlavor '${{ inputs.build_flavor }}'", workflow)
        self.assertIn("contents: read", workflow)
        self.assertNotIn("contents: write", workflow)
        self.assertNotIn("releases: write", workflow)
        self.assertIn("New-GodotOwnedRcSource.ps1", workflow)
        self.assertIn("Pull all approved public LFS objects", workflow)
        self.assertIn("git lfs pull", workflow)
        self.assertNotIn('git lfs pull --include="godot/**"', workflow)
        self.assertIn("Test-GodotWindowsLaunch.ps1", workflow)
        self.assertIn("Tools/okf/requirements.txt", workflow)
        self.assertIn("git config --global core.autocrlf false", workflow)
        self.assertNotIn("-GodotOwned `", workflow)
        self.assertIn("if: always()", workflow)
        self.assertIn("retention-days: 7", workflow)
        self.assertIn("retention-days: 14", workflow)
        self.assertIn("tactics-godot-windows-$key-", workflow)
        self.assertNotIn("create-release", workflow.lower())
        self.assertNotIn("${{ runner.temp }}", workflow)
        self.assertIn("steps.paths.outputs.diagnostics != ''", workflow)

    def test_export_requires_the_tracked_solution_and_rejects_logged_errors(self):
        build_script = (TOOLS / "Build-GodotWindows.ps1").read_text(encoding="utf-8-sig")
        staging_script = (TOOLS / "New-GodotOwnedRcSource.ps1").read_text(encoding="utf-8-sig")

        self.assertIn("Tactics.Godot.slnx", build_script)
        self.assertIn("Tactics.Godot.slnx", staging_script)
        self.assertNotIn("Tactics.Godot.Adapter.sln", build_script)
        self.assertNotIn("Tactics.Godot.Adapter.sln", staging_script)
        self.assertIn("-match '^ERROR:'", build_script)
        self.assertIn("export errors despite exit code 0", build_script)
        self.assertIn("RID allocations of type", build_script)

        for project in (
            REPO / "godot" / "Tactics.Godot.Adapter.csproj",
            REPO / "src" / "Tactics.Core" / "Tactics.Core.csproj",
            REPO / "src" / "Tactics.Application" / "Tactics.Application.csproj",
        ):
            project_text = project.read_text(encoding="utf-8-sig")
            self.assertIn("<RestoreLockedMode>true</RestoreLockedMode>", project_text)
            self.assertIn("<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>", project_text)
            self.assertIn("<PropertyGroup Condition=\"'$(Configuration)' == 'ExportRelease'\">", project_text)
            self.assertIn("<DebugType>None</DebugType>", project_text)
            self.assertIn("<DebugSymbols>false</DebugSymbols>", project_text)

        adapter_project = (REPO / "godot" / "Tactics.Godot.Adapter.csproj").read_text(
            encoding="utf-8-sig"
        )
        self.assertIn("dotnet restore $adapterProject --locked-mode -r win-x64", build_script)
        self.assertIn("dotnet publish $adapterProject", build_script)
        self.assertIn("dotnet build $adapterProject -c ExportDebug", build_script)
        self.assertNotIn("Remove-Item -LiteralPath $outputRoot -Recurse -Force", build_script)
        self.assertIn("Remove-Item -LiteralPath $selectedOutput -Recurse -Force", build_script)
        self.assertIn("-c ExportRelease -r win-x64 --self-contained true -p:GodotTargetPlatform=windows", build_script)
        self.assertIn("--artifacts-path $exportArtifactsDirectory", build_script)
        self.assertIn("-p:GodotProjectDir=$exportGodotProjectDirectory", build_script)
        self.assertIn("editorAssetsHashBeforeExport", build_script)
        self.assertIn("editorAssetsHashAfterExport", build_script)
        self.assertIn("ExportRelease publish changed the Godot Editor dependency graph", build_script)
        self.assertIn("$GodotExecutable --verbose --headless", build_script)
        self.assertIn("'--export-debug'", build_script)
        self.assertIn("'--export-release'", build_script)
        self.assertIn("packages.windows.lock.json", adapter_project)
        self.assertIn("'$(Configuration)' == 'ExportDebug' Or '$(Configuration)' == 'ExportRelease'", adapter_project)
        debug_lock = json.loads((REPO / "godot" / "packages.lock.json").read_text(encoding="utf-8-sig"))
        export_lock = json.loads((REPO / "godot" / "packages.windows.lock.json").read_text(encoding="utf-8-sig"))
        self.assertIn("GodotSharpEditor", debug_lock["dependencies"]["net9.0"])
        self.assertNotIn("GodotSharpEditor", export_lock["dependencies"]["net9.0"])

        verifier = (TOOLS / "Verify-GodotProject.ps1").read_text(encoding="utf-8-sig")
        runsettings = (REPO / "Tactics.Godot.runsettings").read_text(encoding="utf-8-sig")
        self.assertIn("Invoke-IsolatedGdUnitSuite", verifier)
        self.assertIn("Assert-GodotEditorDependencyGraph", verifier)
        self.assertIn("GodotSharpEditor/4\\.7\\.1", verifier)
        self.assertIn("GodotRuntimeTestRunner ends with exit code", verifier)
        self.assertIn("$reportedAssertionFailure", verifier)
        self.assertIn("<TestSessionTimeout>600000</TestSessionTimeout>", runsettings)


if __name__ == "__main__":
    unittest.main()
