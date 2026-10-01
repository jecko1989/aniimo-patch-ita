"""Lettura/scrittura di Compress_<lang>.bin + NewTextMap_<lang>.json (formato Aniimo).

bin  = 4 byte (_version little-endian) + stringhe UTF-8 concatenate, deduplicate
json = {"_count", "_version", "<id>": [offset_byte_dal_file, lunghezza_byte], ...} (indent 4, CRLF)
"""
import json, struct

def read(bin_path, json_path):
    """-> (texts, idx): texts = {id: str} nell'ordine dell'indice; idx = json originale."""
    idx = json.load(open(json_path, encoding='utf8'))
    b = open(bin_path, 'rb').read()
    texts = {k: b[v[0]:v[0] + v[1]].decode('utf8') for k, v in idx.items() if not k.startswith('_')}
    return texts, idx

def write(texts, ref_idx, bin_path, json_path):
    """Riscrive bin+json con i testi dati. L'ordine nel bin segue quello del riferimento
    (offset originale), cosi' a testi invariati il risultato e' identico all'originale."""
    version = ref_idx['_version']
    ref = {k: v[0] for k, v in ref_idx.items() if not k.startswith('_')}
    buf = bytearray(struct.pack('<I', version))
    seen, pos = {}, {}
    for k in sorted(texts, key=lambda k: (ref.get(k, 1 << 60))):
        s = texts[k]
        if s not in seen:
            e = s.encode('utf8')
            seen[s] = (len(buf), len(e))
            buf += e
        pos[k] = seen[s]
    idx = {'_count': len(texts), '_version': version}
    for k in texts:          # ordine delle chiavi = ordine di 'texts'
        idx[k] = list(pos[k])
    open(bin_path, 'wb').write(bytes(buf))
    open(json_path, 'w', encoding='utf8', newline='').write(json.dumps(idx, indent=4, ensure_ascii=False).replace('\n', '\r\n'))
