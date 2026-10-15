// Serves a test build's download page and its APK to this computer only (127.0.0.1), so that
// `tailscale serve --https=10000 http://127.0.0.1:8110` can hand them to the owner's own devices.
// usage: node tools/apk-share/serve.mjs --folder D:\puzzle-cache\apk-share [--port 8110]
//
// Only the folder's own index.html and its .apk files are served, by name; nothing else is reachable, and only GET
// and HEAD are answered.
import { createServer } from 'node:http';
import { createReadStream, readdirSync, statSync } from 'node:fs';
import { basename, extname, join } from 'node:path';
import { parseArgs } from 'node:util';

const { values } = parseArgs({
  options: {
    folder: { type: 'string' },
    port: { type: 'string', default: '8110' },
  },
});
if (!values.folder) {
  console.error('usage: node tools/apk-share/serve.mjs --folder <folder> [--port 8110]');
  process.exit(2);
}

const folder = values.folder;
const port = Number(values.port);

const contentTypes = {
  '.html': 'text/html; charset=utf-8',
  '.apk': 'application/vnd.android.package-archive',
};

/** The file a request's path names, when it is one the folder offers; undefined otherwise. */
function offeredFile(urlPath) {
  const name = urlPath === '/' ? 'index.html' : decodeURIComponent(urlPath.slice(1));
  if (name !== basename(name)) return undefined;
  if (!(extname(name) in contentTypes)) return undefined;
  if (!readdirSync(folder).includes(name)) return undefined;
  return join(folder, name);
}

const server = createServer((request, response) => {
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    response.writeHead(405, { Allow: 'GET, HEAD' }).end();
    return;
  }

  const path = new URL(request.url, 'http://localhost').pathname;
  const file = offeredFile(path);
  if (file === undefined) {
    response.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' }).end('Bulunamadı');
    return;
  }

  const headers = {
    'Content-Type': contentTypes[extname(file)],
    'Content-Length': statSync(file).size,
    'Cache-Control': 'no-store',
  };
  if (extname(file) === '.apk') headers['Content-Disposition'] = `attachment; filename="${basename(file)}"`;
  response.writeHead(200, headers);
  if (request.method === 'HEAD') {
    response.end();
    return;
  }

  createReadStream(file)
    .on('error', failure => {
      console.error(`${new Date().toISOString()} reading ${basename(file)} failed: ${failure.message}`);
      response.destroy(failure);
    })
    .pipe(response);
  console.log(`${new Date().toISOString()} ${request.method} ${path}`);
});

server.listen(port, '127.0.0.1', () => console.log(`Serving ${folder} on http://127.0.0.1:${port}`));
