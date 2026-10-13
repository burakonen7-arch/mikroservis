"""Identify only this workspace's API processes before preserving or stopping them."""
import os
from pathlib import Path
import subprocess

def matches_api(pid: int, service: str, root: Path) -> bool:
    if os.name == 'nt':
        command = subprocess.run(
            ['powershell.exe', '-NoProfile', '-Command',
             f'(Get-CimInstance Win32_Process -Filter "ProcessId={pid}").CommandLine'],
            capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW,
            check=False).stdout
    else:
        try:
            command = Path(f'/proc/{pid}/cmdline').read_bytes().decode(errors='replace')
        except FileNotFoundError:
            return False
    assembly = root / f'src/{service}/{service}.Api/bin/Release/net10.0/{service}.Api.dll'
    return str(assembly).lower() in command.lower()
