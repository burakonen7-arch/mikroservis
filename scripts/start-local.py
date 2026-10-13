#!/usr/bin/env python3
"""Start Product, Stock and Gateway on the host; Compose supplies infrastructure."""
from pathlib import Path
import json, os, shutil, subprocess, sys, time, urllib.request
from local_processes import matches_api
root=Path(__file__).resolve().parents[1]
state=root/'.local'
state.mkdir(exist_ok=True)
values={}
for line in (root/'.env').read_text().splitlines():
    if line.strip() and not line.lstrip().startswith('#') and '=' in line:
        key,value=line.split('=',1); values[key.strip()]=value.strip()
values.update({k:os.environ[k] for k in values if k in os.environ})
dotnet=shutil.which('dotnet') or (str(Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'dotnet/dotnet.exe') if os.name == 'nt' else '/workspace/tools/dotnet/dotnet')
if not values.get('JWT_SIGNING_KEY'): sys.exit('Run scripts/prepare-debug.ps1 or scripts/init-env.sh to generate JWT settings.')
base=os.environ.copy()
base.update({'DOTNET_CLI_HOME':os.getenv('DOTNET_CLI_HOME',str(state/'dotnet-home')),
             'ASPNETCORE_ENVIRONMENT':'Development',
             'Jwt__SigningKey':values['JWT_SIGNING_KEY'],
             'Jwt__Issuer':'mikroservis','Jwt__Audience':'mikroservis-api',
             'Logging__LogLevel__Microsoft.EntityFrameworkCore':'Warning',
             'OTEL_EXPORTER_OTLP_ENDPOINT':'http://localhost:4317',
             'OTEL_EXPORTER_OTLP_PROTOCOL':'grpc',
             'ConnectionStrings__Redis':'localhost:6379,abortConnect=false',
             'RabbitMQ__Uri':f'amqp://mikroservis:{values["RABBITMQ_PASSWORD"]}@localhost:5672',
             'Database__Initialize':'true'})
for service,port,connection in [
    ('Product',5001,'mongodb://localhost:27017/?replicaSet=rs0&directConnection=true'),
    ('Stock',5002,f'User Id=stock;Password={values["ORACLE_APP_PASSWORD"]};Data Source=localhost:1521/FREEPDB1'),
    ('Gateway',5000,'')]:
    pidfile=state/f'{service}.pid'
    if pidfile.exists():
        pid=int(pidfile.read_text())
        if matches_api(pid, service, root):
            print(f'{service} already running ({pid}); preserved.');continue
    assembly=root/f'src/{service}/{service}.Api/bin/Release/net10.0/{service}.Api.dll'
    if not assembly.exists(): sys.exit('Build the solution in Release before starting.')
    env=base | {'ASPNETCORE_URLS':f'http://localhost:{port}','ConnectionStrings__Database':connection}
    if service == 'Gateway':
        for key in ('ConnectionStrings__Database', 'ConnectionStrings__Redis', 'RabbitMQ__Uri', 'Database__Initialize'):
            env.pop(key, None)
        for role in ('admin','reader','inventory','catalog'):
            env[f'DevelopmentAuth__{role}__Password'] = values[f'AUTH_{role.upper()}_PASSWORD']
    with (state/f'{service}.log').open('a') as log:
        launch={'creationflags':subprocess.CREATE_NO_WINDOW} if os.name == 'nt' else {'start_new_session':True}
        process=subprocess.Popen([dotnet,str(assembly)],cwd=assembly.parents[3],env=env,stdout=log,stderr=subprocess.STDOUT,**launch)
    pidfile.write_text(str(process.pid))
    print(f'Started {service} ({process.pid}).')
http=urllib.request.build_opener(urllib.request.ProxyHandler({}))
for port in (5001,5002,5000):
    for attempt in range(120):
        try:
            route='health/live' if port == 5000 else 'health/ready'
            with http.open(f'http://localhost:{port}/{route}',timeout=3) as response:
                if response.status==200: break
        except (OSError,TimeoutError): time.sleep(1)
    else: sys.exit(f'API on {port} not ready; inspect .local/*.log')
print('Product, Stock and Gateway are ready.')
