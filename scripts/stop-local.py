#!/usr/bin/env python3
from pathlib import Path
import os, signal
state=Path(__file__).resolve().parents[1]/'.local'
for service in ('Product','Stock'):
    path=state/f'{service}.pid'
    if not path.exists(): continue
    pid=int(path.read_text())
    try:
        if f'{service}.Api.dll'.encode() in Path(f'/proc/{pid}/cmdline').read_bytes():
            os.kill(pid,signal.SIGTERM)
            print(f'Stopped {service}.')
    except FileNotFoundError: pass
    path.unlink()
