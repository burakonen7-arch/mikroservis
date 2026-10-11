#!/usr/bin/env python3
"""Start only the API processes on the host; Compose supplies infrastructure."""
from pathlib import Path
import json, os, shutil, subprocess, sys, time, urllib.request
root=Path(__file__).resolve().parents[1]
state=root/'.local'
state.mkdir(exist_ok=True)
values={}
for line in (root/'.env').read_text().splitlines():
    if line and not line.startswith('#'):
        key,value=line.split('=',1); values[key]=value
values.update({k:os.environ[k] for k in values if k in os.environ})
dotnet=shutil.which('dotnet') or '/workspace/tools/dotnet/dotnet'
base=os.environ.copy()
base.update({'DOTNET_CLI_HOME':os.getenv('DOTNET_CLI_HOME',str(state/'dotnet-home')),
             'OTEL_EXPORTER_OTLP_ENDPOINT':'http://localhost:4317',
             'OTEL_EXPORTER_OTLP_PROTOCOL':'grpc',
             'ConnectionStrings__Redis':'localhost:6379,abortConnect=false',
             'RabbitMQ__Uri':f'amqp://mikroservis:{values["RABBITMQ_PASSWORD"]}@localhost:5672',
             'Database__Initialize':'true'})
for service,port,connection in [
    ('Product',5001,'mongodb://localhost:27017/?replicaSet=rs0&directConnection=true'),
    ('Stock',5002,f'User Id=stock;Password={values["ORACLE_APP_PASSWORD"]};Data Source=localhost:1521/FREEPDB1')]:
    pidfile=state/f'{service}.pid'
    if pidfile.exists():
        pid=int(pidfile.read_text())
        try:
            command=Path(f'/proc/{pid}/cmdline').read_bytes()
            if f'{service}.Api.dll'.encode() in command:
                print(f'{service} already running ({pid}); preserved.');continue
        except FileNotFoundError: pass
    assembly=root/f'src/{service}/{service}.Api/bin/Release/net10.0/{service}.Api.dll'
    if not assembly.exists(): sys.exit('Build the solution in Release before starting.')
    env=base | {'ASPNETCORE_URLS':f'http://localhost:{port}','ConnectionStrings__Database':connection}
    with (state/f'{service}.log').open('a') as log:
        process=subprocess.Popen([dotnet,str(assembly)],cwd=assembly.parent,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
    pidfile.write_text(str(process.pid))
    print(f'Started {service} ({process.pid}).')
for port in (5001,5002):
    for attempt in range(120):
        try:
            with urllib.request.urlopen(f'http://localhost:{port}/health/ready',timeout=3) as response:
                if response.status==200: break
        except (OSError,TimeoutError): time.sleep(1)
    else: sys.exit(f'API on {port} not ready; inspect .local/*.log')
print('Both APIs are ready.')
