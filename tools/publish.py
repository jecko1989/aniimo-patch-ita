"""Genera la cartella publish/ da caricare online (manifest.json + payload compressi).
Uso: python publish.py <versione> <gg/mm/aaaa> [note]   (dopo build.py)"""
import sys, os, gzip, json, hashlib, zipfile
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(HERE)
LUA = ROOT + '/Aniimo_Data/cvs/res/lua'
ver, date = int(sys.argv[1]), sys.argv[2]
notes = sys.argv[3] if len(sys.argv) > 3 else ''
out = ROOT + '/publish'; os.makedirs(out, exist_ok=True)
z = zipfile.ZipFile(LUA + '/LuaScripts.xdf')
files = {
    'bin':  ('Compress_id_ID.bin.gz', open(LUA + '/LuaScripts/Data/I18N/Compress_id_ID.bin', 'rb').read()),
    'json': ('NewTextMap_id_ID.json.gz', z.read('xfs/luascripts/Data/I18N/NewTextMap_id_ID.json')),
}
payloads = []
for pid, (name, data) in files.items():
    with open(f'{out}/{name}', 'wb') as f: f.write(gzip.compress(data, 9, mtime=0))
    payloads.append({'id': pid, 'url': name, 'sha256': hashlib.sha256(data).hexdigest(), 'size': len(data)})
manifest = {'version': ver, 'date': date, 'notes': notes, 'payloads': payloads, 'apply': [
    {'payload': 'bin',  'loose': 'LuaScripts/Data/I18N/Compress_id_ID.bin', 'xdfEntry': 'xfs/luascripts/Data/I18N/Compress_id_ID.bin'},
    {'payload': 'json', 'xdfEntry': 'xfs/luascripts/Data/I18N/NewTextMap_id_ID.json'}]}
json.dump(manifest, open(out + '/manifest.json', 'w', encoding='utf8'), indent=2)
print('publish/ pronta, versione', ver)
