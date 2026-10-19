// Reads one file out of a zip archive that a GitHub release offers, without fetching the whole archive: the
// font releases of the Noto project come as archives of 25 to 660 MB with every weight and format of a family,
// and the game keeps one file of one of them. A zip archive lists its files at its end (the central directory),
// each with where its bytes begin, so three asks for parts of the archive (an HTTP range each) reach one file:
// the end, the directory, and the file's own bytes. The file is checked against the checksum the archive names
// for it before it is handed back.
//
// usage (from the repository root): node tools/unity/release-member.js <archive URL> [<path in the archive>]
// lists the archive's files, or writes the one named to the current folder under its own name.
const fs = require("fs");
const path = require("path");
const zlib = require("zlib");

/** The signatures of a zip archive's records, as they lie in the file (little-endian). */
const END_OF_DIRECTORY = 0x06054b50;
const DIRECTORY_ENTRY = 0x02014b50;
const LOCAL_HEADER = 0x04034b50;

/** The end record is 22 bytes and may carry a comment of up to 65,535 more. */
const END_RECORD_LENGTH = 22;
const LONGEST_END = END_RECORD_LENGTH + 0xffff;

/** How a zip archive keeps a file: as it is, or deflated. */
const STORED = 0;
const DEFLATED = 8;

/** A 32-bit field of this value says the true one lies in a ZIP64 record, which these archives do not need. */
const IN_A_ZIP64_RECORD = 0xffffffff;

/** Bytes first to last (both included) of what lies at a URL; GitHub sends a release's file from another host, which the ask follows. */
async function rangeOf(url, first, last) {
  const response = await fetch(url, { headers: { Range: `bytes=${first}-${last}` }, redirect: "follow" });
  if (response.status !== 206) throw new Error(`${url} answered ${response.status} to an ask for bytes ${first}-${last}; it must send parts of the file`);
  return Buffer.from(await response.arrayBuffer());
}

/** How many bytes lie at a URL, from its answer to an ask for its first byte (the host answers no ask counted from the end). */
async function lengthOf(url) {
  const response = await fetch(url, { headers: { Range: "bytes=0-0" }, redirect: "follow" });
  const range = /\/(\d+)$/.exec(response.headers.get("content-range") ?? "");
  await response.arrayBuffer();
  if (response.status !== 206 || !range) throw new Error(`${url} answered ${response.status} without the length of the file`);
  return Number(range[1]);
}

/**
 * The files an archive lists, each with what is needed to read it: its name, how it is kept, its checksum, its
 * sizes and where its local record begins.
 */
async function filesOf(url) {
  const length = await lengthOf(url);
  const tailStart = Math.max(0, length - LONGEST_END);
  const tail = await rangeOf(url, tailStart, length - 1);
  let end = -1;
  for (let at = tail.length - END_RECORD_LENGTH; at >= 0; at--) {
    if (tail.readUInt32LE(at) === END_OF_DIRECTORY) {
      end = at;
      break;
    }
  }
  if (end < 0) throw new Error(`${url} is not a zip archive: it has no end record`);

  const directorySize = tail.readUInt32LE(end + 12);
  const directoryStart = tail.readUInt32LE(end + 16);
  if (directorySize === IN_A_ZIP64_RECORD || directoryStart === IN_A_ZIP64_RECORD) throw new Error(`${url} keeps its directory in a ZIP64 record, which this reader does not read`);
  const directory = await rangeOf(url, directoryStart, directoryStart + directorySize - 1);
  const files = [];
  for (let at = 0; at < directory.length; ) {
    if (directory.readUInt32LE(at) !== DIRECTORY_ENTRY) throw new Error(`${url}: the directory breaks off at byte ${at}`);
    const nameLength = directory.readUInt16LE(at + 28);
    const extraLength = directory.readUInt16LE(at + 30);
    const commentLength = directory.readUInt16LE(at + 32);
    files.push({
      name: directory.toString("utf8", at + 46, at + 46 + nameLength),
      method: directory.readUInt16LE(at + 10),
      crc: directory.readUInt32LE(at + 16),
      packedSize: directory.readUInt32LE(at + 20),
      size: directory.readUInt32LE(at + 24),
      localRecord: directory.readUInt32LE(at + 42),
    });
    at += 46 + nameLength + extraLength + commentLength;
  }
  return { length, files };
}

/** One file of an archive, by its path in the archive, checked against the checksum the archive names. */
async function fileOf(url, name) {
  const { files } = await filesOf(url);
  const file = files.find(each => each.name === name);
  if (!file) throw new Error(`${url} has no ${name}`);
  if (file.packedSize === IN_A_ZIP64_RECORD || file.localRecord === IN_A_ZIP64_RECORD) throw new Error(`${url}: ${name} lies in a ZIP64 record, which this reader does not read`);

  const header = await rangeOf(url, file.localRecord, file.localRecord + 29);
  if (header.readUInt32LE(0) !== LOCAL_HEADER) throw new Error(`${url}: ${name} has no local record where the directory says`);
  const dataStart = file.localRecord + 30 + header.readUInt16LE(26) + header.readUInt16LE(28);
  const packed = await rangeOf(url, dataStart, dataStart + file.packedSize - 1);
  let bytes;
  if (file.method === STORED) bytes = packed;
  else if (file.method === DEFLATED) bytes = zlib.inflateRawSync(packed);
  else throw new Error(`${url}: ${name} is kept in a way (${file.method}) this reader does not read`);

  if (bytes.length !== file.size || zlib.crc32(bytes) !== file.crc) throw new Error(`${url}: ${name} did not arrive whole`);
  return bytes;
}

module.exports = { filesOf, fileOf };

async function main([url, name]) {
  if (!url) throw new Error("usage: node tools/unity/release-member.js <archive URL> [<path in the archive>]");
  if (!name) {
    const { length, files } = await filesOf(url);
    console.log(`${url}: ${(length / 1048576).toFixed(1)} MB, ${files.length} files`);
    for (const file of files) console.log(`  ${file.name}  ${(file.size / 1024).toFixed(0)} KB`);
    return;
  }
  const bytes = await fileOf(url, name);
  fs.writeFileSync(path.basename(name), bytes);
  console.log(`${path.basename(name)}: ${(bytes.length / 1024).toFixed(0)} KB`);
}

if (require.main === module) {
  main(process.argv.slice(2)).catch(failure => {
    console.error(failure.message);
    process.exitCode = 1;
  });
}
