// This script writes the current version (YYYY.MM.DD) to VERSION.txt for Docusaurus config consumption
const fs = require('fs');
const path = require('path');
const now = new Date();
const version = `${now.getFullYear()}.${String(now.getMonth() + 1).padStart(2, '0')}.${String(now.getDate()).padStart(2, '0')}`;

fs.writeFileSync(path.join(__dirname, '../VERSION.txt'), version, 'utf8');