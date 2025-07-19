// This utility reads the version from VERSION.txt
const fs = require('fs');
const path = require('path');

function getVersion() {
  try {
    return fs.readFileSync(path.join(__dirname, '../VERSION.txt'), 'utf8').trim();
  } catch {
    return 'unknown';
  }
}

module.exports = getVersion;
