"""Verify backend project ownership without restoring packages or running a service."""
import unittest
from pathlib import Path
from xml.etree import ElementTree

BACKEND = Path(__file__).resolve().parents[2] / "backend"
PREFIX = "HackthonSpurs."
# These are allowed reference edges, not a requirement to reference an unused project.
ALLOWED = {
    "Api": {"Documents", "Resources"},
    "Worker": {"Documents", "Resources"},
    "Documents": {"Resources"},
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

    def test_host_and_document_roots_are_not_feature_folders(self):
        """Keep endpoint, queue and entity implementations out of the multi-feature project roots."""
        for suffix in ("Api", "Documents", "Worker", "Reader.Spur"):
            project = BACKEND / (PREFIX + suffix)
            startup_files = {"GlobalUsings.cs", "AssemblyInfo.cs"}
            if suffix != "Documents":
                startup_files.add("Program.cs")
            misplaced = {f.name for f in project.glob("*.cs")} - startup_files
            self.assertFalse(misplaced, f"Place feature implementations in an owned folder: {misplaced}")

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
