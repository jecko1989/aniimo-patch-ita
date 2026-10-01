"""Unisce le correzioni dei revisori (out/*.jsonl) nella traduzione finale, con validazione.
Uso: python apply.py <cartella_lavoro> [--dry]
Scrive <tools>/traduzione_it.json (id -> testo italiano) e work/rejected.jsonl."""
import sys, os, re, json, glob, pickle, collections
W = sys.argv[1]
HERE = os.path.dirname(os.path.abspath(__file__))
L = pickle.load(open(W + '/L.pkl', 'rb'))
en, cur = L['en'], L['id_ID']
fixed = json.load(open(W + '/fixed.json', encoding='utf8'))
index = {int(k): v for k, v in json.load(open(W + '/index.json')).items()}
TOK = re.compile(r'<[^>]+>|\{[^}]*\}|%[-0-9.]*[a-zA-Z]')
for _f in glob.glob(W + '/index[0-9]*.json'): index.update({int(k): v for k, v in json.load(open(_f)).items()})
def sig(s): return collections.Counter(TOK.findall(s))
def ok(e, t):
    if sig(e) != sig(t): return 'tag/segnaposto diversi'
    if e.count('\n') != t.count('\n'): return 'a capo diversi'
    if (len(e) - len(e.lstrip()) > 0) != (len(t) - len(t.lstrip()) > 0) or (len(e) - len(e.rstrip()) > 0) != (len(t) - len(t.rstrip()) > 0): return 'spazi iniziali/finali diversi'
    if not t.strip(): return 'vuoto'
    if 'BREAK' in e and 'BREAK' not in t: return 'BREAK perso'
    return None

def norm(e, t):
    # 'Alpha' resta 'Alpha', nell'ordine dell'inglese (correzione di un revisore che scriveva 'X Alfa')
    if 'Alfa' in t and 'Alpha' in e:
        t = t.replace('Alfa', 'Alpha')
        m = re.search(r'(\S+) Alpha\b', t)
        if m and ('Alpha ' + m.group(1)) in e: t = t.replace(m.group(0), 'Alpha ' + m.group(1))
    return t

final = {k: cur.get(k, en[k]) for k in en}          # base: italiano attuale (fallback inglese)
final.update(fixed)
rej, questions, nch = [], [], 0
for f in sorted(glob.glob(W + '/out/*.jsonl')):
    for ln, line in enumerate(open(f, encoding='utf8'), 1):
        line = line.strip()
        if not line: continue
        try: r = json.loads(line)
        except Exception as ex: rej.append({'file': os.path.basename(f), 'line': ln, 'why': 'json non valido', 'raw': line[:200]}); continue
        if 'q' in r: questions.append(r['q']); continue
        n, t = r.get('n'), r.get('it')
        if n not in index or not isinstance(t, str): rej.append({'file': os.path.basename(f), 'line': ln, 'why': 'riga malformata', 'raw': line[:200]}); continue
        ids = index[n]; e = en[ids[0]]
        t = norm(e, t)
        why = ok(e, t)
        if why: rej.append({'file': os.path.basename(f), 'n': n, 'why': why, 'en': e, 'it': t}); continue
        for k in ids: final[k] = t
        nch += 1
for k in final:                      # 'Alpha' mai tradotto
    if re.search(r'\bAlpha\b', en[k]): final[k] = re.sub(r'\b[Aa]lfa\b', 'Alpha', final[k])
final['1823332970'] = 'Italiano'    # etichetta dello slot lingua (era Indonesia)
print('alfa residui:', sum(1 for k in final if 'lfa' in final[k] and 'Alpha' in en[k]))
print('correzioni applicate:', nch, '| rifiutate:', len(rej), '| domande:', len(questions))
if '--dry' not in sys.argv:
    json.dump(final, open(HERE + '/traduzione_it.json', 'w', encoding='utf8'), ensure_ascii=False)
open(W + '/rejected.jsonl', 'w', encoding='utf8').write('\n'.join(json.dumps(r, ensure_ascii=False) for r in rej))
open(W + '/questions.txt', 'w', encoding='utf8').write('\n'.join(questions))
