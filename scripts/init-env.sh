#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ -e .env ]]; then echo '.env already exists; preserved.'; exit 0; fi
umask 077
python3 - <<'PYCODE'
import secrets
from pathlib import Path
with Path('.env').open('x') as f:
    for name in ['ORACLE_ADMIN_PASSWORD', 'ORACLE_APP_PASSWORD', 'RABBITMQ_PASSWORD']:
        f.write(f'{name}={secrets.token_hex(16)}\n')
PYCODE
echo 'Generated local development credentials in ignored .env.'
