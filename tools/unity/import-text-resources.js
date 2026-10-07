// Brings what the game's lettering needs into the Unity project, from files the Unity Editor itself ships with:
//   - TextMesh Pro's "Essential Resources" (its shaders and settings) -> unity/Assets/TextMesh Pro
//     (the same files the Editor's "Import TMP Essential Resources" button unpacks, without opening the Editor);
//   - the typeface Roboto Bold from TextMesh Pro's "Examples & Extras" -> unity/Assets/Art/Fonts/Roboto-Bold.ttf.
// Nothing is downloaded. Every file comes with the .meta file Unity packed with it, so its id in the project is the
// one TextMesh Pro expects. Run once; the files are committed. Safe to run again: it writes the same files.
//
// usage (from the repository root): node tools/unity/import-text-resources.js
// Set PUZZLE_UNITY_EDITOR to the Editor's folder (the one that holds Unity.exe) when it is not in the usual place.
const fs = require("fs");
const os = require("os");
const path = require("path");
const { execFileSync } = require("child_process");

const EDITOR_FOLDER = process.env.PUZZLE_UNITY_EDITOR ?? "C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor";
const PACKAGES = path.join(EDITOR_FOLDER, "Data/Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources");
const PROJECT = "unity";
const WINDOWS_TAR = path.join(process.env.SystemRoot ?? "C:/Windows", "System32", "tar.exe");

/** What is taken from each package: a test on the path Unity packed the file under, and where it goes instead (or nowhere else). */
const IMPORTS = [
  {
    package: "TMP Essential Resources.unitypackage",
    // The two graphs are for another render pipeline (HDRP) and would not compile in this project.
    takes: packedPath => !packedPath.includes("HDRP"),
    placedAt: packedPath => packedPath,
  },
  {
    package: "TMP Examples & Extras.unitypackage",
    takes: packedPath => packedPath.endsWith("/Fonts/Roboto-Bold.ttf"),
    placedAt: () => "Assets/Art/Fonts/Roboto-Bold.ttf",
  },
];

function main() {
  const work = fs.mkdtempSync(path.join(os.tmpdir(), "puzzle-text-resources-"));
  try {
    for (const source of IMPORTS) {
      const unpacked = path.join(work, path.basename(source.package, ".unitypackage"));
      unpack(path.join(PACKAGES, source.package), unpacked);
      const written = entriesOf(unpacked).filter(entry => source.takes(entry.packedPath)).map(entry => place(entry, source.placedAt(entry.packedPath)));
      if (written.length === 0) throw new Error(`${source.package} holds nothing this script asks for`);
      console.log(`${source.package}: ${written.length} entries -> ${PROJECT}`);
    }
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
}

/**
 * A .unitypackage is a gzipped tar archive. It is unpacked by the tar that Windows brings: the one of Git's shell
 * takes the colon of a drive letter for the name of another computer.
 */
function unpack(packageFile, into) {
  if (!fs.existsSync(packageFile)) throw new Error(`${packageFile} is missing; set PUZZLE_UNITY_EDITOR to the Editor's folder`);
  fs.mkdirSync(into, { recursive: true });
  execFileSync(WINDOWS_TAR, ["-xzf", packageFile, "-C", into], { stdio: "ignore" });
}

/** The archive holds one folder per asset: the path it had in a project, its .meta file and, unless it is a folder, the file. */
function entriesOf(unpacked) {
  return fs.readdirSync(unpacked)
    .map(id => path.join(unpacked, id))
    .filter(folder => fs.existsSync(path.join(folder, "pathname")))
    .map(folder => ({
      packedPath: fs.readFileSync(path.join(folder, "pathname"), "utf8").split("\n")[0].trim(),
      meta: path.join(folder, "asset.meta"),
      file: fs.existsSync(path.join(folder, "asset")) ? path.join(folder, "asset") : undefined,
    }));
}

function place(entry, projectPath) {
  const target = path.join(PROJECT, projectPath);
  if (entry.file === undefined) {
    fs.mkdirSync(target, { recursive: true });
  } else {
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.copyFileSync(entry.file, target);
  }
  fs.copyFileSync(entry.meta, `${target}.meta`);
  return target;
}

main();
