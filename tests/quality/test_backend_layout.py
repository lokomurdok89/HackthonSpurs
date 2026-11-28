"""Verify backend project ownership without restoring packages or running a service."""
import unittest
from pathlib import Path
from xml.etree import ElementTree

BACKEND = Path(__file__).resolve().parents[2] / "backend"
PREFIX = "HackthonSpurs."
# These are allowed reference edges, not a requirement to reference an unused project.
ALLOWED = {
    "Api": {"Application", "Domain", "Persistence", "Resources"},
    "Worker": {"Application", "Persistence", "Resources"},
    "Persistence": {"Application", "Domain", "Resources"},
    "Application": {"Domain", "Resources"},
    "Domain": set(),
    "Resources": set(),
    "Reader.Spur": {"Resources"},
    "Reader.Fixture": {"Resources"},
    "Ocr.Bilevel": {"Resources"},
}


class BackendBoundaries(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        """Discover production projects in the backend and resolve their real paths."""
        cls.projects = {p.resolve() for p in BACKEND.rglob("*.csproj")
                        if not {"obj", "bin"}.intersection(p.relative_to(BACKEND).parts)}

    def test_solution_naming_and_ownership(self):
        """Require a consistent project/folder name and actual membership in the backend solution."""
        sln = ElementTree.parse(BACKEND / "HackthonSpurs.slnx")
        listed = {(BACKEND / node.attrib["Path"]).resolve() for node in sln.iter("Project")}
        self.assertTrue(all(p.is_file() for p in listed), "Solution contains a missing project")
        self.assertEqual({p for p in listed if p.is_relative_to(BACKEND)}, self.projects)
        self.assertEqual({p.stem for p in self.projects}, {PREFIX + name for name in ALLOWED})
        for proj in self.projects:
            self.assertEqual(proj.parent.name, proj.stem)

    def test_persistence_project_preserves_ef_identity(self):
        """Keep migration discovery compatible while the project and its database folders use the new name."""
        project = BACKEND / (PREFIX + "Persistence")
        xml = ElementTree.parse(project / (project.name + ".csproj"))
        self.assertEqual(xml.findtext(".//AssemblyName"), PREFIX + "Documents")
        self.assertEqual(xml.findtext(".//RootNamespace"), PREFIX + "Documents.Persistence")
        self.assertTrue((project / "Migrations" / "DocumentsDbModelSnapshot.cs").is_file())
        self.assertTrue((project / "Database" / "DocumentsDb.cs").is_file())

    def test_host_and_persistence_roots_are_not_feature_folders(self):
        """Keep endpoint, queue and entity implementations out of the multi-feature project roots."""
        for suffix in ("Api", "Persistence", "Worker", "Reader.Spur", "Application", "Domain"):
            project = BACKEND / (PREFIX + suffix)
            startup_files = {"GlobalUsings.cs", "AssemblyInfo.cs"}
            if suffix in ("Api", "Worker", "Reader.Spur"):
                startup_files.add("Program.cs")
            misplaced = {f.name for f in project.glob("*.cs")} - startup_files
            self.assertFalse(misplaced, f"Place feature implementations in an owned folder: {misplaced}")

    def test_inner_projects_have_no_framework_or_provider_dependencies(self):
        """Domain, use cases and the shared vocabulary build without any persistence, network or host packages."""
        for suffix in ("Domain", "Application", "Resources"):
            project = BACKEND / (PREFIX + suffix) / (PREFIX + suffix + ".csproj")
            xml = ElementTree.parse(project)
            with self.subTest(layer=suffix):
                self.assertEqual(xml.getroot().get("Sdk"), "Microsoft.NET.Sdk")
                for tag in ("PackageReference", "FrameworkReference", "Reference", "COMReference"):
                    self.assertEqual(list(xml.iter(tag)), [], f"{suffix} must not acquire an external {tag}")
                self.assertFalse(any(n.get("Link") or ".." in n.get("Include", "")
                                     for n in xml.iter("Compile")), "Do not compile adapter sources into the core")

    def test_application_harness_has_no_adapter_dependencies(self):
        """Unit scenarios must execute against the application boundary alone, with no production adapters."""
        harness = BACKEND.parent / "tests/application/Application.Tests.csproj"
        tree = ElementTree.parse(harness)
        targets = {(harness.parent / node.attrib["Include"].replace(chr(92), "/")).resolve()
                   for node in tree.iter("ProjectReference")}
        application = BACKEND / (PREFIX + "Application") / (PREFIX + "Application.csproj")
        self.assertEqual(targets, {application})
        for tag in ("PackageReference", "FrameworkReference", "Reference"):
            self.assertFalse(list(tree.iter(tag)), f"The pure harness must not acquire {tag}")

    def test_use_cases_do_not_open_files_or_network_connections(self):
        """The SDK provides HTTP/filesystem classes even without packages; keep those effects in adapters."""
        forbidden = (r"\b(?:HttpClient|HttpRequestMessage|HttpResponseMessage|SocketsHttpHandler|"
                     r"TcpClient|FileStream|FileInfo|DirectoryInfo)\b|\b(?:File|Directory)\.\w+\(")
        for layer in ("Domain", "Application"):
            for path in (BACKEND / (PREFIX + layer)).rglob("*.cs"):
                if {"obj", "bin"}.intersection(path.relative_to(BACKEND).parts):
                    continue
                source = path.read_text(encoding="utf-8")
                code = "\n".join(line for line in source.splitlines() if not line.lstrip().startswith("//"))
                self.assertNotRegex(code, forbidden, f"{path.name} accesses an external adapter directly")

    def test_document_endpoints_do_not_bind_infrastructure(self):
        """Catch coupling that a project-level check cannot: the API also contains the composition root."""
        endpoints = BACKEND / "HackthonSpurs.Api" / "Endpoints"
        for path in [*endpoints.joinpath("Documents").glob("**/*.cs"), *endpoints.joinpath("Search").glob("**/*.cs")]:
            text = path.read_text(encoding="utf-8")
            code = "\n".join(line for line in text.splitlines() if not line.lstrip().startswith("//"))
            self.assertNotRegex(code, r"\b(?:HackthonSpurs\.(?:Documents|Persistence)|Microsoft\.EntityFrameworkCore|Npgsql|Pgvector)\b",
                                f"{path.name} should depend on use cases/read ports, not database or storage adapters")

    def test_references_and_native_ocr_boundary(self):
        """Prevent host-to-host coupling and keep the native Magick package in the disposable helper process."""
        native_owners = set()
        for proj in self.projects:
            name = proj.stem.removeprefix(PREFIX)
            csproj = ElementTree.parse(proj)
            for ref in csproj.iter("ProjectReference"):
                target = (proj.parent / ref.attrib["Include"].replace(chr(92), "/")).resolve()
                self.assertIn(target, self.projects, f"Unowned reference from {name}: {target}")
                self.assertIn(target.stem.removeprefix(PREFIX), ALLOWED[name], f"Invalid dependency: {name} -> {target.stem}")
            if any(p.get("Include", "").lower().startswith("magick.net") for p in csproj.iter("PackageReference")):
                native_owners.add(name)
        self.assertEqual(native_owners, {"Ocr.Bilevel"})


if __name__ == "__main__":
    unittest.main(verbosity=2)
