// Fetches the game's stand-in sounds: one short file per sound cue of the game (unity/Assets/Scripts/Flow/SoundCue.cs),
// chosen from two of Kenney's packs (kenney.nl: "Interface Sounds" and "Impact Sounds", both under Creative Commons
// CC0 1.0, which asks for nothing, not even a name). They stand in for sounds the owner chooses later, as the
// game's first typeface did: an agent picks them by name and by measure, not by ear.
//
// Each pack is fetched as the archive its page links to, into a new empty folder of its own under out/sounds-fetch,
// and only the audio files of the archive are taken out of it, into a folder of their own beside it; the archive's
// own License.txt must say CC0 as the page does. Nothing that
// arrives is run or opened by anything but this script: an archive is read as bytes, an audio file is kept only when
// its first bytes are those of an OGG or WAV file, under a name of the cue it stands for. The files kept go to
// unity/Assets/Art/Sounds, and where each came from, with its licence, its bytes and its length, to
// art/sounds/licences.json. The Editor's ArtCatalogueBuilder stops on a cue that has no file there.
//
// usage (from the repository root):
//   node tools/unity/fetch-sounds.js          fetches the packs and keeps the chosen files
//   node tools/unity/fetch-sounds.js --list   fetches the packs and lists every audio file in them with its length
// After a file has changed, run ProjectSetup.Run: it lists every cue with its file, its length and its peak level.
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const zlib = require("zlib");

const SOUNDS_FOLDER = path.join("unity", "Assets", "Art", "Sounds");
const RECORD = path.join("art", "sounds", "licences.json");
const FETCHED_FOLDER = path.join("out", "sounds-fetch");
const LICENCE = "Creative Commons CC0 1.0 Universal (public domain dedication)";
const LICENCE_URL = "https://creativecommons.org/publicdomain/zero/1.0/";

/** The stand-ins together must stay this small: they are short clicks and knocks, not music. */
const MOST_BYTES_IN_ALL = 1024 * 1024;

/** No cue lasts longer than this: a sound of play that is still ringing when the next comes is noise. */
const LONGEST_SECONDS = 1.5;

/** The packs, each with the page that links its archive; the archive's address is read from the page. */
const PACKS = [
  { name: "interface-sounds", title: "Interface Sounds", page: "https://kenney.nl/assets/interface-sounds" },
  { name: "impact-sounds", title: "Impact Sounds", page: "https://kenney.nl/assets/impact-sounds" },
];

/**
 * The file each cue sounds with: its pack, and its name in the pack's archive. Chosen by name, and by length from
 * `--list`: short clicks for presses and switches, a dull knock for a refusal, glass and metal for what breaks and
 * what pays. The cue's own name is the name the file is kept under.
 */
const CHOICES = [
  { cue: "press", pack: "interface-sounds", entry: "Audio/click_001.ogg", why: "a short click under a finger on a button" },
  { cue: "refuse", pack: "impact-sounds", entry: "Audio/impactSoft_medium_000.ogg", why: "a dull, soft knock: two pieces that will not trade, a booster that changes nothing" },
  { cue: "swap", pack: "interface-sounds", entry: "Audio/minimize_007.ogg", why: "a short falling sweep as long as the trade itself" },
  { cue: "match", pack: "interface-sounds", entry: "Audio/drop_001.ogg", why: "the shortest drop: pieces that pop, many times a turn" },
  { cue: "special-made", pack: "interface-sounds", entry: "Audio/maximize_003.ogg", why: "a short rising sweep for a special piece that appears" },
  { cue: "blast", pack: "impact-sounds", entry: "Audio/impactGlass_heavy_003.ogg", why: "the shortest heavy glass impact for a rocket, a bomb or a colour bomb going off" },
  { cue: "booster", pack: "impact-sounds", entry: "Audio/impactWood_heavy_000.ogg", why: "a heavy wooden knock as the spatula or the brush strikes" },
  { cue: "move-thrown", pack: "interface-sounds", entry: "Audio/pluck_001.ogg", why: "the shorter pluck: a run of them as the finale throws its moves" },
  { cue: "card-down", pack: "interface-sounds", entry: "Audio/open_001.ogg", why: "the shortest 'open' for a card that comes down" },
  { cue: "win", pack: "interface-sounds", entry: "Audio/confirmation_002.ogg", why: "the longest confirmation, half a second: the card of a won level" },
  { cue: "lose", pack: "interface-sounds", entry: "Audio/error_006.ogg", why: "a half-second error: the card of a level lost for good" },
  { cue: "coin", pack: "impact-sounds", entry: "Audio/impactMetal_light_004.ogg", why: "the shortest light metal impact: coins that arrive" },
  { cue: "piece-placed", pack: "impact-sounds", entry: "Audio/impactWood_light_000.ogg", why: "a light wooden tap as a jigsaw piece drops into its place" },
  { cue: "painting-whole", pack: "impact-sounds", entry: "Audio/impactBell_heavy_002.ogg", why: "a bell under a second long: a painting made whole" },
  { cue: "toggle", pack: "interface-sounds", entry: "Audio/toggle_001.ogg", why: "a toggle's click for a switch on the settings card" },
];

const sha256 = bytes => crypto.createHash("sha256").update(bytes).digest("hex");

async function answerOf(url) {
  const response = await fetch(url, { headers: { "User-Agent": "Mozilla/5.0 (fetch-sounds.js)" } });
  if (!response.ok) throw new Error(`${url} answered ${response.status}`);
  return { bytes: Buffer.from(await response.arrayBuffer()), type: response.headers.get("content-type") ?? "" };
}

/** The archive a pack's page links to; a page that asks for anything else first (a sign-in, a payment, a challenge) names none and stops the fetch. */
async function archiveAddressOf(pack) {
  const page = (await answerOf(pack.page)).bytes.toString("utf8");
  const links = [...new Set(page.match(/https:\/\/kenney\.nl\/media\/pages\/assets\/[^"'\s]+\.zip/g) ?? [])];
  if (links.length !== 1) throw new Error(`${pack.page} links ${links.length} archives where one was expected; look at the page before going on`);
  if (!/Creative Commons CC0/.test(page)) throw new Error(`${pack.page} no longer says the pack is under Creative Commons CC0`);
  return links[0];
}

/** A folder that is new and empty: whatever an earlier fetch left there is removed first. */
function emptyFolder(folder) {
  fs.rmSync(folder, { recursive: true, force: true });
  fs.mkdirSync(folder, { recursive: true });
  return folder;
}

/** The entries of a ZIP archive, read from its central directory: name, how they are stored, and where their data begins. */
function entriesOf(archive) {
  const endSignature = 0x06054b50;
  let end = archive.length - 22;
  while (end >= 0 && archive.readUInt32LE(end) !== endSignature) end--;
  if (end < 0) throw new Error("what arrived is not a ZIP archive");
  const count = archive.readUInt16LE(end + 10);
  let at = archive.readUInt32LE(end + 16);
  const entries = [];
  for (let entry = 0; entry < count; entry++) {
    if (archive.readUInt32LE(at) !== 0x02014b50) throw new Error("the archive's directory is damaged");
    const nameLength = archive.readUInt16LE(at + 28);
    const local = archive.readUInt32LE(at + 42);
    entries.push({
      name: archive.toString("utf8", at + 46, at + 46 + nameLength),
      method: archive.readUInt16LE(at + 10),
      packedBytes: archive.readUInt32LE(at + 20),
      dataAt: local + 30 + archive.readUInt16LE(local + 26) + archive.readUInt16LE(local + 28),
    });
    at += 46 + nameLength + archive.readUInt16LE(at + 30) + archive.readUInt16LE(at + 32);
  }

  return entries;
}

/** The bytes of an entry: stored as they are, or deflated. */
function bytesOf(archive, entry) {
  const packed = archive.subarray(entry.dataAt, entry.dataAt + entry.packedBytes);
  if (entry.method === 0) return packed;
  if (entry.method === 8) return zlib.inflateRawSync(packed);
  throw new Error(`${entry.name} is packed in a way this script does not read (${entry.method})`);
}

/** What kind of audio file the bytes are, by their first bytes; undefined for anything else. */
function audioKindOf(bytes) {
  if (bytes.length >= 4 && bytes.toString("latin1", 0, 4) === "OggS") return "ogg";
  if (bytes.length >= 12 && bytes.toString("latin1", 0, 4) === "RIFF" && bytes.toString("latin1", 8, 12) === "WAVE") return "wav";
  return undefined;
}

/**
 * How long an OGG Vorbis file plays, in seconds: the last page's granule position counts the samples of one
 * channel, and the stream's first packet names the sample rate.
 */
function oggSeconds(bytes) {
  const firstPacket = 27 + bytes[26];
  if (bytes.toString("latin1", firstPacket + 1, firstPacket + 7) !== "vorbis") throw new Error("an OGG file that holds no Vorbis sound");
  const rate = bytes.readUInt32LE(firstPacket + 12);
  let samples = 0n;
  for (let page = 0; page + 27 <= bytes.length;) {
    if (bytes.toString("latin1", page, page + 4) !== "OggS") throw new Error("the OGG file's pages are damaged");
    const granule = bytes.readBigInt64LE(page + 6);
    if (granule > samples) samples = granule;
    const segments = bytes[page + 26];
    let bodyBytes = 0;
    for (let segment = 0; segment < segments; segment++) bodyBytes += bytes[page + 27 + segment];
    page += 27 + segments + bodyBytes;
  }

  return Number(samples) / rate;
}

/** How long a WAV file of plain samples plays, in seconds. */
function wavSeconds(bytes) {
  let byteRate;
  for (let chunk = 12; chunk + 8 <= bytes.length;) {
    const id = bytes.toString("latin1", chunk, chunk + 4);
    const size = bytes.readUInt32LE(chunk + 4);
    if (id === "fmt ") byteRate = bytes.readUInt32LE(chunk + 16);
    if (id === "data" && byteRate) return size / byteRate;
    chunk += 8 + size + (size % 2);
  }

  throw new Error("a WAV file without its format or its samples");
}

const secondsOf = (kind, bytes) => (kind === "ogg" ? oggSeconds(bytes) : wavSeconds(bytes));

/**
 * Fetches a pack's archive into a new empty folder and takes its audio files out of it into another, flat, under
 * their own names. Returns the files taken, by their name in the archive.
 */
async function fetchPack(pack) {
  const archiveAddress = await archiveAddressOf(pack);
  const archived = await answerOf(archiveAddress);
  const downloads = emptyFolder(path.join(FETCHED_FOLDER, pack.name, "archive"));
  const archivePath = path.join(downloads, path.basename(new URL(archiveAddress).pathname));
  fs.writeFileSync(archivePath, archived.bytes);

  const entries = entriesOf(archived.bytes);
  const licenceText = entries.find(entry => entry.name === "License.txt");
  if (!licenceText || !/Creative Commons Zero, CC0/.test(bytesOf(archived.bytes, licenceText).toString("utf8"))) {
    throw new Error(`${pack.name}: the archive's License.txt does not say the pack is under Creative Commons CC0`);
  }

  const audioFolder = emptyFolder(path.join(FETCHED_FOLDER, pack.name, "audio"));
  const files = new Map();
  for (const entry of entries) {
    if (!/\.(ogg|wav)$/i.test(entry.name)) continue;
    const bytes = bytesOf(archived.bytes, entry);
    const kind = audioKindOf(bytes);
    if (!kind) throw new Error(`${pack.name}: ${entry.name} is named as a sound and is none`);

    // Only the file's own name is used, so no name in the archive can lead outside the folder.
    const name = path.basename(entry.name);
    fs.writeFileSync(path.join(audioFolder, name), bytes);
    files.set(entry.name, { name, kind, bytes, seconds: secondsOf(kind, bytes) });
  }

  console.log(`${pack.title}: ${(archived.bytes.length / 1024).toFixed(0)} KB from ${archiveAddress}, ${files.size} sound files`);
  return { archiveAddress, archiveSha256: sha256(archived.bytes), files };
}

function list(fetched) {
  for (const pack of PACKS) {
    console.log(`\n${pack.title}`);
    for (const [entryName, file] of [...fetched.get(pack.name).files].sort(([a], [b]) => a.localeCompare(b))) {
      console.log(`  ${entryName.padEnd(48)} ${file.seconds.toFixed(3)} s  ${(file.bytes.length / 1024).toFixed(1).padStart(6)} KB`);
    }
  }
}

/** Keeps the chosen file of every cue under the cue's name, and returns what the record says of each. */
function keepTheChosen(fetched) {
  fs.mkdirSync(SOUNDS_FOLDER, { recursive: true });
  fs.mkdirSync(path.dirname(RECORD), { recursive: true });
  const kept = [];
  for (const choice of CHOICES) {
    const pack = PACKS.find(each => each.name === choice.pack);
    const fromThePack = fetched.get(choice.pack);
    const file = fromThePack.files.get(choice.entry);
    if (!file) throw new Error(`${choice.cue}: ${choice.pack} has no ${choice.entry}`);
    if (file.seconds > LONGEST_SECONDS) throw new Error(`${choice.cue}: ${choice.entry} lasts ${file.seconds.toFixed(2)} s, longer than ${LONGEST_SECONDS} s`);

    const keptAs = `${choice.cue}.${file.kind}`;
    fs.writeFileSync(path.join(SOUNDS_FOLDER, keptAs), file.bytes);
    kept.push({
      cue: choice.cue, file: keptAs, seconds: Number(file.seconds.toFixed(3)), bytes: file.bytes.length, sha256: sha256(file.bytes),
      pack: pack.title, entryInThePack: choice.entry, author: "Kenney (www.kenney.nl)", licence: LICENCE, licenceUrl: LICENCE_URL,
      page: pack.page, archive: fromThePack.archiveAddress, archiveSha256: fromThePack.archiveSha256, why: choice.why,
    });
  }

  const inAll = kept.reduce((sum, file) => sum + file.bytes, 0);
  if (inAll > MOST_BYTES_IN_ALL) throw new Error(`the sounds come to ${(inAll / 1024).toFixed(0)} KB, more than ${MOST_BYTES_IN_ALL / 1024} KB`);
  const stray = fs.readdirSync(SOUNDS_FOLDER).filter(name => !name.endsWith(".meta") && !kept.some(file => file.file === name));
  if (stray.length > 0) throw new Error(`${SOUNDS_FOLDER} holds files no cue names: ${stray.join(", ")}; remove them with their .meta files`);

  fs.writeFileSync(RECORD, JSON.stringify({ licence: LICENCE, licenceUrl: LICENCE_URL, sounds: kept }, null, 2) + "\n");
  for (const file of kept) console.log(`${file.cue.padEnd(14)} ${file.file.padEnd(20)} ${file.seconds.toFixed(3)} s  ${(file.bytes / 1024).toFixed(1)} KB  ${file.pack}: ${file.entryInThePack}`);
  console.log(`${kept.length} sounds, ${(inAll / 1024).toFixed(0)} KB in all; record in ${RECORD}`);
}

async function main() {
  const fetched = new Map();
  for (const pack of PACKS) fetched.set(pack.name, await fetchPack(pack));
  if (process.argv.includes("--list")) {
    list(fetched);
    return;
  }

  keepTheChosen(fetched);
}

main().catch(failure => {
  console.error(failure.message);
  process.exitCode = 1;
});
