"""Prepara i lotti di revisione. Uso: python prep.py <cartella_lavoro>
Legge i textmap dal gioco-patch (id_ID = italiano attuale) e dalle altre lingue,
applica le regole deterministiche e scrive i lotti jsonl per la revisione."""
import sys, os, re, json, collections, pickle
L = pickle.load(open(sys.argv[1] + '/L.pkl', 'rb'))
W = sys.argv[1]
en, cur, zh = L['en'], L['id_ID'], L['zh_CN']
OFF = ['de_DE', 'fr_FR', 'es_ES', 'pt_PT']

# --- regole deterministiche ---
NATIVE = {'日本語', 'Español', 'Português', 'Русский', '繁體中文', 'Deutsch', 'Français', 'English', '한국어', '简体中文', 'Tiếng Việt', 'ไทย'}
KEEP_EXC = {'Normal', 'I', 'Indonesia'}          # parole italiane legittime / etichetta lingua
NAMELIKE = re.compile(r"[A-Z][A-Za-z0-9'’.\- ]{1,40}")
fixed = {}          # id -> testo finale senza revisione
for k, s in en.items():
    if s in NATIVE and k in cur:                     # menu lingue: nome nativo
        fixed[k] = s
    elif (s not in KEEP_EXC and NAMELIKE.fullmatch(s) and len(re.sub(r'\W', '', s)) >= 3
          and all(L[l].get(k) == s for l in OFF)):   # nome proprio mai tradotto dalle lingue ufficiali
        fixed[k] = s
json.dump(fixed, open(W + '/fixed.json', 'w', encoding='utf8'), ensure_ascii=False)

# --- coppie (en, it attuale) da rivedere ---
def strip(s): return re.sub(r'<[^>]*>|\{[^}]*\}|%[a-z0-9]', '', s)
pairs = collections.defaultdict(list)
for k, s in en.items():
    if k in fixed: continue
    if not re.search(r'[A-Za-z]{2,}', strip(s)): continue     # solo numeri/tag/simboli
    pairs[(s, cur.get(k))].append(k)
keys = sorted(pairs, key=lambda p: (p[0].lower(), p[1] or ''))
out, batch, size, n = [], [], 0, 0
os.makedirs(W + '/batches', exist_ok=True)
index = {}
def flush():
    global batch, size, n
    if batch:
        with open(f'{W}/batches/{n:03d}.jsonl', 'w', encoding='utf8') as f:
            for r in batch: f.write(json.dumps(r, ensure_ascii=False) + '\n')
        n += 1; batch, size = [], 0
for i, (s, c) in enumerate(keys):
    ids = pairs[(s, c)]
    r = {'n': i, 'en': s, 'it': c}
    if len(s) <= 40: r['zh'] = zh.get(ids[0], '')
    index[i] = ids
    batch.append(r); size += len(s) + len(c or '') + 30
    if size >= 45000: flush()
flush()
json.dump(index, open(W + '/index.json', 'w'))
print('fixed', len(fixed), 'pairs', len(keys), 'batches', n)
