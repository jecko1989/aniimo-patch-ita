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

# Titoli di abilita'/talenti/mosse e nomi inventati segnalati dai revisori: restano in inglese (decisione dell'utente)
KEEP_EN = set("""Charged Blow|Charged Geyser|Charged Impact|Charge Sweep|Charged Lightning Strike|Battle Fervor|Bastion of Iron|Biting Wind|Blind Panic|Bloodthirst|Boiling Blood|Body of Iron|Born for More|Blade Bloom|Hunt|Hunter's Instinct|Hunting Instinct|Hustle|Hypnotic Spores|Flame Cyclone|Fission Needles|Flame Fist Combo|High Jump|High-Speed Air Blade|High-Voltage Dash|Healing Rose|Healing Angel|Pact: Ferocious Fang|Overflowing Power|Mushroom Convergence|Mushroom Fanatic|Nightbloom Owl Wings|Delayed Pain|Desperate Potential|Desperation Surge|Fully Loaded I|Fully Loaded II|Fully Loaded III|Spring of Life|Spotlight Moment|Sword of Bravery|Sword of Protection|Quick Training|Training Mastery|Perfect Counter|Lucky Strike|Blink|Haste|Weaken|Waltz|Triple Movement|Symphony of Life|Emergency Repair|Emergency Evasion|Emergency Treatment|Weakness Mark|Fox Mark|Soothe|Water Pillar|Water Splash|Holy Awakening|Holy Roar|Holy Storm|Joint Defense|Judgment Storm|Keen Senses|Layered Strength|Claw of Madness|Cleaving Waves|Close-Quarter Strike|Cloudwalk|Cloud Shield|Comet Aureus|Gale Guard|Frostbite Sunder|Coral Impact|Crimson Blade|Critical Strike I|Crushing Momentum|Energy Burst|Enhanced Lightning Blade|Essence Leech|Ethereal Light Wave|Unyielding Will|Veiled Truth|Vein Abundance|Starbound Journey|Safe Evacuation|Safe Expansion I|Safe Expansion II|Safe Reinforcement|Refined Gaze|Reflective Barrier|Shield of Golden Protection|Shield of Golden Tenacity|Shadow Domain|Shock Wave|Shooting Stars|Silent Step|Silent Owl Spirit|Soundwave Clash|Soundwave Shake|Windborn Stamps|Pathfinding|Battle Art Department|Aniimology|Ballistic Guard|Bouncy Sling|Black Hole Domain|Blossoming Moment|Butterfly Dance|Energy Orb|Healing Water|Lunar Eclipse|Secret Fragrance Mark|Selene’s Judgment|Electrified Impact|Dark Claw|Swift Retreat|Self-Destruction|Beat of Passion|Disruptive Current|Electric Dance Frenzy|Enhanced Light Blade|Collectible Drop|Vitality Blooms|Vine Entanglement|Lotus Bloom|Irisalis Shadow|Florae Descent|Adversity Charge|Adversity Parry|Adversity Surge|Aerial Spin|Amethyst Glide|Annihilation Bomb|Aurora Flow|Aurora Ice Shield|Backstab|Barrage|Battle Rhythm|Battlebond|Battlebond III|Battlelust|Become the Pinball|Black Hole Generation|White Hole Generation|Blade Reversal|Blasting Roses|Bubble Gun|Bulwark|Chain Lightening|Chain Ultimate|Brain Crash|Brute Force Works Wonders|Blade Gap Mastery|Bounce Toward the Stars|Charge for Love|Against the Beat|Air Superiority|Anticipate the Move|Born to Dig|Born to Meddle|Born to Ride the Wind|Built for Pressure|Binocular Focus|Burst of Red|Aeroboard|ATK Aura|REGEN Aura|Defense Command|Element Synergy|Elemental Shot|Leader's Call|Mega Mallet|Plushie Pummel|Power Break|Quick Twine|Robust Physique I|Robust Physique II|Robust Physique III|Robust Physique IV|Robust Physique V|Silent Step|Sonic Slam|Swoleshroom|Symphony of Life|Tweetin' Target|Gravity Pull|Cloudsoft Slumber|Contempt of the Strong|Converging Sense|Core Descent|Courage Born of Fear|Courageous Wit|Crossfire|Crosswind|Cyclone|Darknight Shadow|Deadly Steel Claws|Detect Weakness|Devastation Cannon|Disintegration|Dispel Sadness|Don't Panic in a Crisis|Don't Underestimate the Spark|Double Delight|Dragon's Vision|Dreamdrift Waltz|Dreamsong|Drum Whirl|Earth Shield|Earth Spikes|Ebb Tide|Echoing Grimoire|Efficient Support|Elastic Potential|Electric Discharge|Electric Flow|Electromagnetic Shield|Elegant Waltz|Elemental Switch|Elemental Ward|Elusive Presence|Energized Start|Energy Absorption and Conversion|Energy Surge|Enhanced Bubble|Enhanced Cloud Shield|Enhanced Counter|Enhanced Normal Attack|Everything Has a Weakness|Evenly Matched|Extreme Gusto|Extreme Output|Extreme Speed Lightning|Extrovert Effort|Eye of Potential|Eyes Locked|Fatal Shot|Fears Become Reality|Fearsome Heat|Feather Bathed in Light|Featherblade Waltz|Feathered Breeze|Feathered Dawn|Featherlight Lift|Feint|Ferocious Fang|Fierce Fang|Final Stand|Fingertip Sparks|Fireball Split Shot|Flame Dive|Flame Shield|Flame Spiral|Flame Vortex|Flameblade|Flamewreath Armor|Flaming Claws|Flash Freeze|Flawless Defense|Floral Burst|Floral Storm|Flowing Water Shield|Flowing Water Slash|Flustered Fury|Flutter Heal|Flying Kick|Focused Beam|Focused Impact|Focused Strike|Following the Strong|Forming Floating Ice|Fortune's Favor|Fractured Echo|Fragrance Bomb|Fragrance Shield|Freedom's Breeze|Freezing Beam|Freezing Claws|Frequency Overload|Frost Condensation|Frost Edge|Frost Fangblade|Frost Spike|Frostlit Trace|Frostwolf Edge|Frostwolf's Edge|Frostwolf Huntshadow|Frostwolf Ice Soul|Frostwolf Spirit|Full Barrage|Gale-Chasing|Gentle Healing Water|Glimmermist Whispers|Grassland Assimilation|Guidance of the Core|Healing Aura|Healing Blossoms|Healing Flame|Healing Spirit|Healing Tune|Healing Waves|Heart's Resonance|Helios' Awakening|Helios' Judgment|Here Comes the Bubble|Hypnotic Smoke|Ice Burst|Ice Shield|Ice Spikes|Icy Finish|Ironclad Armament|Irresistible Charm|Lightning Dart|Lightning Lock|Lightning Needle|Lingering Malice|Lingering Omen|Lingering Will|Loaded for Bear|Lunar Erosion|Luring Light|Mass Surge|Measured Strike|Mobile Sweep|Momentum|Nimble Dodge|Nimble Sprouting|Nightbat Shadow|Nighttime Flames|No Retreat|None Can Stop You|Overcharged Claw Strike|Overcharged Shield|Owl's Poise|Owlish Elegance|Owlshade Nightfall|Parasitic Aggravation|Parasitic Domain|Parasitic Shock Wave|Pillar of Fire|Pinecone Bomb|Power of the Crowd|Power Surge|Prismana's Embrace|Prismana's Luminous Veil|Protective Healing|Pursuit|Radiant Dart|Radiant Dreamshadow|Rage Outburst|Rain of Blessing|Rapid Volley|Raging Fire|Raging Flame|Reborn in Flames|Resolute Defense|Rocksteady Heart|Roar of the Bouldus|Rousing Rhythm|Rushing Rapids|Remnant Spirit Catalysis|Resonance of the Heart|Safety Net I|Safety Net II|Safety Net III|Searing Cool Touch|Seismic Wave|Selene's Awakening|Shielding Smoke|Share Your Power With Me|Slumberscent|Snow Cocoon|Soaring Through the Nine Heavens|Sonata of the Wind|Soul of Electric Shock|Soul of Falling Stars|Spring Surge|Spring of Healing|Squirt Gun|Star Glide|Starfall Glide|Starfall Rain|Starlight Cascade|Starlight Healing|Starlit Glide|Starry Flow|Steady Breathing|Steal the Spotlight|Stealth Hunt|Strange Flash|Swift Beats|Take Teammate's Power|Time Bomb|Trick Bomb|Twirly Whispers|Two Minds|Vigor of Life|Voice of Silence|Voice of the Stars|Water Pressure Imbalance|Wind Guard|Wind Shield|Wind of Regeneration|Wind of Rejuvenation|Winter's Warmth|Wrath of the Sea|Warriors Never Die|Wolf Soul Energy|Zephyr's Breath""".split('|'))
for k in final:
    if en[k].strip() in KEEP_EN: final[k] = en[k]
    if en[k].strip() == 'Back': final[k] = 'Indietro'    # pulsante, non la parte del corpo

# ripuliture meccaniche finali (decisioni dell'utente)
_ART = {"dall'": "dal ", "dell'": "del ", "all'": "al ", "nell'": "nel ", "sull'": "sul ", "l'": "il ", "L'": "Il "}
for k in final:
    e, t = en[k], final[k]
    if 'Holo' in e:
        t = re.sub(r"\b([Oo])lo-", lambda m: m.group(1) + 'lo-' if False else 'Holo-', t)
    if 'Polaris Institute' in e:
        t = re.sub(r"(dall'|dell'|all'|nell'|sull'|\bl'|\bL')Istituto Polaris", lambda m: _ART[m.group(1)] + 'Polaris Institute', t)
        t = t.replace('Istituto Polaris', 'Polaris Institute')
    if re.search(r'sanctum', e, re.I):
        t = re.sub(r'[Ss]antuari(?:o)?', 'Sanctum', t)
    t = t.replace('Operation: Egg Heist', 'Operazione: Egg Heist')
    t = t.replace('Moondew Radish', 'Ravanello Moondew').replace('Ravanello rugiadalunare', 'Ravanello Moondew')
    t = re.sub(r'([Pp]eperoncino) (?:di |della )?(?:luna crescente|lunacera)', r' della luna crescente', t)
    final[k] = t
final['1823332970'] = 'Italiano'    # etichetta dello slot lingua (era Indonesia)
print('alfa residui:', sum(1 for k in final if 'lfa' in final[k] and 'Alpha' in en[k]))
print('correzioni applicate:', nch, '| rifiutate:', len(rej), '| domande:', len(questions))
if '--dry' not in sys.argv:
    json.dump(final, open(HERE + '/traduzione_it.json', 'w', encoding='utf8'), ensure_ascii=False)
open(W + '/rejected.jsonl', 'w', encoding='utf8').write('\n'.join(json.dumps(r, ensure_ascii=False) for r in rej))
open(W + '/questions.txt', 'w', encoding='utf8').write('\n'.join(questions))
