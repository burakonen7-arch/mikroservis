#!/usr/bin/env python3
"""Real integration check against both running services and their dependencies."""
import json, os, time, urllib.request, urllib.error, uuid
product = os.getenv('PRODUCT_URL', 'http://localhost:5001')
stock = os.getenv('STOCK_URL', 'http://localhost:5002')
def request(base,path,data=None,expected=200):
    body=None if data is None else json.dumps(data).encode()
    req=urllib.request.Request(base+path,body,{'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=10) as response:
            assert response.status == expected, response.status
            return json.load(response)
    except urllib.error.HTTPError as error:
        if error.code != expected: raise
        payload=error.read()
        return json.loads(payload) if payload else None
def wait(fn,seconds=90):
    end=time.monotonic()+seconds
    while time.monotonic()<end:
        try:
            result=fn()
            if result: return result
        except (urllib.error.URLError,TimeoutError): pass
        time.sleep(1)
    raise AssertionError('Timed out waiting for asynchronous processing')
for base in (product,stock): wait(lambda: request(base,'/health/ready'))
request(product,'/products',{'name':'','price':1},400)
p=request(product,'/products',{'name':'Integration '+str(uuid.uuid4()),'price':19.95},201)
def initialized():
    try: return request(stock,'/stocks/'+p['id'])
    except urllib.error.HTTPError as error:
        if error.code == 404: return None
        raise
s=wait(initialized)
assert s['quantity']==0 and s['version']==1, s
assert request(product,'/products/'+p['id'])['name']==p['name']
# Warm caches, then verify invalidation and asynchronous projection.
assert request(stock,'/stocks/'+p['id'])==s
updated=request(stock,'/stocks/'+p['id']+'/adjustments',{'delta':7,'expectedVersion':s['version']})
assert updated['quantity']==7 and updated['version']==2, updated
request(stock,'/stocks/'+p['id']+'/adjustments',{'delta':1,'expectedVersion':s['version']},409)
request(stock,'/stocks/'+p['id']+'/adjustments',{'delta':-8,'expectedVersion':updated['version']},400)
assert request(stock,'/stocks/'+p['id'])==updated
def projected():
    value=request(product,'/products/'+p['id'])
    return value if value['availableQuantity']==7 and value['stockVersion']==2 else None
wait(projected)
request(product,'/products/'+str(uuid.uuid4()),expected=404)
request(stock,'/stocks/'+str(uuid.uuid4()),expected=404)
print('PASS: readiness, create, RabbitMQ round trip, MongoDB/Oracle persistence, Redis invalidation, validation, conflict, 404')
print('Product ID:',p['id'])
