#!/usr/bin/env python3
from pathlib import Path
import os, signal, subprocess
from local_processes import matches_api
state=Path(__file__).resolve().parents[1]/'.local'
for service in ('Gateway','Product','Stock'):
    path=state/f'{service}.pid'
    if not path.exists(): continue
    pid=int(path.read_text())
    try:
        if matches_api(pid,service,state.parent):
            if os.name == 'nt':
                subprocess.run(['powershell.exe','-NoProfile','-Command',f'Stop-Process -Id {pid}'],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
            else:
                os.kill(pid,signal.SIGTERM)
            print(f'Stopped {service}.')
    except FileNotFoundError: pass
    path.unlink()
