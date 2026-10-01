"""Ricostruisce i file della patch dall'italiano finale (tools/traduzione_it.json).
Aggiorna: Aniimo_Data/cvs/res/lua/LuaScripts/Data/I18N/Compress_id_ID.bin
          e dentro LuaScripts.xdf: Data/I18N/Compress_id_ID.bin + NewTextMap_id_ID.json.
Uso: python build.py [--base <xdf_di_partenza>]"""
import sys, os, json, zipfile, io, shutil, textmap
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
LUA = ROOT + '/Aniimo_Data/cvs/res/lua'
BIN_E = 'xfs/luascripts/Data/I18N/Compress_id_ID.bin'
JSON_E = 'xfs/luascripts/Data/I18N/NewTextMap_id_ID.json'

xdf = LUA + '/LuaScripts.xdf'
base = sys.argv[sys.argv.index('--base') + 1] if '--base' in sys.argv else xdf
ita = json.load(open(HERE + '/traduzione_it.json', encoding='utf8'))

zin = zipfile.ZipFile(base)
ref = json.loads(zin.read(JSON_E).decode('utf8'))
texts = {k: ita[k] for k in ref if not k.startswith('_') and k in ita}
missing = [k for k in ita if k not in texts]          # id presenti in inglese ma non nell'indice it (i 7 "mancanti")
for k in missing: texts[k] = ita[k]
os.makedirs('tmp_build', exist_ok=True)
textmap.write(texts, ref, 'tmp_build/id.bin', 'tmp_build/id.json')
newbin, newjson = open('tmp_build/id.bin', 'rb').read(), open('tmp_build/id.json', 'rb').read()

tmp = xdf + '.new'
with zipfile.ZipFile(tmp, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as zout:
    for zi in zin.infolist():
        data = {BIN_E: newbin, JSON_E: newjson}.get(zi.filename)
        if data is None: data = zin.read(zi)
        ni = zipfile.ZipInfo(zi.filename, zi.date_time)
        ni.compress_type, ni.external_attr, ni.create_system = zi.compress_type, zi.external_attr, zi.create_system
        zout.writestr(ni, data)
zin.close()
os.replace(tmp, xdf)
open(LUA + '/LuaScripts/Data/I18N/Compress_id_ID.bin', 'wb').write(newbin)
shutil.rmtree('tmp_build')
print('ok:', len(texts), 'stringhe; nuovi id aggiunti all\'indice:', len(missing), '; bin', len(newbin), 'byte')
