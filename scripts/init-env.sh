#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
umask 077
python3 - <<'PYCODE'
import secrets
from pathlib import Path
path = Path('.env')
existing = dict(line.split('=', 1) for line in path.read_text().splitlines() if '=' in line and not line.lstrip().startswith('#')) if path.exists() else {}
names = ['ORACLE_ADMIN_PASSWORD', 'ORACLE_APP_PASSWORD', 'RABBITMQ_PASSWORD', 'JWT_SIGNING_KEY',
         'AUTH_ADMIN_PASSWORD', 'AUTH_READER_PASSWORD', 'AUTH_INVENTORY_PASSWORD', 'AUTH_CATALOG_PASSWORD']
with path.open('a') as f:
    for name in names:
        if not existing.get(name):
            f.write(f'\n{name}={secrets.token_hex(32)}\n')
PYCODE
echo 'Local development credentials are ready in ignored .env; existing values preserved.'
