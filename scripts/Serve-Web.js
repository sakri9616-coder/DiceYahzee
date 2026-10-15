const http = require('http');
const fs = require('fs');
const path = require('path');

const root = path.resolve(process.argv[2] || '');
const port = Number(process.argv[3] || 8765);
if (!fs.existsSync(path.join(root, 'index.html')) || !Number.isInteger(port) || port < 1 || port > 65535) {
  throw new Error('Usage: node scripts/Serve-Web.js <Web-build-directory> [port]');
}
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8',
  '.wasm': 'application/wasm', '.data': 'application/octet-stream', '.css': 'text/css; charset=utf-8',
  '.png': 'image/png', '.ico': 'image/x-icon', '.json': 'application/json; charset=utf-8' };
http.createServer((request, response) => {
  let pathname;
  try { pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname); }
  catch { response.writeHead(400).end(); return; }
  const file = path.resolve(root, '.' + pathname);
  if (file !== root && !file.startsWith(root + path.sep)) { response.writeHead(403).end(); return; }
  const target = pathname.endsWith('/') ? path.join(file, 'index.html') : file;
  fs.stat(target, (error, stat) => {
    if (error || !stat.isFile()) { response.writeHead(404).end(); return; }
    response.writeHead(200, { 'Content-Type': types[path.extname(target)] || 'application/octet-stream',
      'Content-Length': stat.size });
    fs.createReadStream(target).pipe(response);
  });
}).listen(port, '127.0.0.1', () => process.stdout.write(`http://127.0.0.1:${port}/\n`));
