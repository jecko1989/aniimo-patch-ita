# Guida di revisione – traduzione italiana di Aniimo

Lavori su lotti `batches/NNN.jsonl`. Ogni riga: `{"n":<id>, "en":"originale inglese", "it":"italiano attuale", "zh":"cinese (solo stringhe corte, serve a capire se e' un nome)"}`.
`it` e' null se manca (da tradurre). L'italiano attuale e' una traduzione automatica dall'inglese: spesso buona, ma con errori di senso, registro incoerente e nomi tradotti per sbaglio.

## Cosa devi fare
Per ogni riga decidi se l'italiano attuale e' corretto, naturale e rispetta le regole sotto.
Scrivi nel file di output SOLO le righe da cambiare (o da tradurre se `it` e' null):
`{"n":<id>, "it":"nuovo testo"}`  (JSON valido su una riga, `\n` per gli a capo, `\"` per le virgolette).
Se hai un dubbio che serve all'utente (termine ambiguo, scelta di glossario), aggiungi una riga `{"q":"domanda breve con esempio e id"}`. Non bloccarti: scegli la soluzione migliore e continua.

## Regole di contenuto (decise dall'utente)
1. **Nomi degli Aniimo (le creature) e ogni loro riferimento: restano IDENTICI all'inglese** (es. Pomegg, Pebbling, Bailites→"Bailites" ma in italiano plurale = invariato o con articolo, mai tradotto; "Resting Baleetle"→"Baleetle a riposo" ok solo se il nome resta). Anche nomi di forme/evoluzioni se sono nomi propri. La parola "Aniimo" e' invariabile (un Aniimo, due Aniimo, gli Aniimo).
2. **BREAK non si traduce mai, sempre maiuscolo `BREAK`**: e' lo status di stordimento dei nemici. "Break State"/"in BREAK state" -> "stato BREAK" / "in stato BREAK". Mai Rottura, Frattura, Collasso, Interruzione, Crack, Frantumazione, ecc. Verbi/sostantivi comuni "break" (rompere una roccia, fare una pausa) si traducono normalmente. Non perdere BREAK quando l'inglese lo contiene (es. "BREAK Pursuit" resta "BREAK Pursuit", non un nome inventato).
3. **Restano in inglese, identici**: nomi di PNG/personaggi (Ban, Pat, Grace, Jiff, Selens, Stellalord, Summer...), nomi di luoghi/habitat/regioni/santuari (Lumenfly Valley, Susuta Habitat, Sanctum...), nomi di abilita', mosse, talenti e buff/debuff (Primal Breath, Hard Winter - BREAK, Nuclear explosion - BUFF...), e le sigle di stat/combattimento: ATK, DEF, M.ATK, M.DEF, HP, SP, CRIT, DMG, EXP, REGEN, RV, BREAK ecc. (non "DAN", "DIF. M", "Liv. VR": scrivi `DMG`, `M.DEF`, `RV`). "Lv." puo' restare "Liv." come ora.
   Se una stringa e' un titolo corto in Title Case che nomina un luogo/abilita'/mossa, tienila in inglese. Le etichette d'interfaccia generiche (Cost, Head, Confirm, Bond...) e i nomi di oggetti comuni (Seafarer Straw Hat -> Cappello di paglia da marinaio) si traducono.
4. Sinonimi/termini ricorrenti: usa sempre la stessa resa. Se l'italiano attuale usa una resa dominante sensata per un termine di gioco (Ricerca, Habitat, Legame...), mantienila.

## Regole di forma
- Mantieni IDENTICI tag e segnaposto: `<style=...>…</style>`, `<color=...>`, `<sprite ...>`, `<customRichText(...)>`, `{0}`, `%d`, `%s`, `[Difesa]`-simili solo se presenti nell'inglese, `\n` e spazi/a capo iniziali e finali. Non tradurre il contenuto dei tag.
- Il testo dentro parentesi quadre tipo `[Defense]` e' un termine di gioco: usa la resa italiana gia' usata nel testo attuale se sensata, altrimenti lasciala com'e'.
- Italiano naturale, registro informale con il "tu" (come ora), frasi brevi da videogioco. Niente calchi dall'inglese. Attenzione a genere/numero/accordi e alle preposizioni articolate.
- Accenti corretti (è, é, à, ò, ù, ì; maiuscola "È"), apostrofo dritto `'` come nel testo attuale, puntini di sospensione come nell'originale.
- Lunghezza: le etichette UI devono restare brevi (non piu' lunghe dell'originale inglese se evitabile).
- Non aggiungere ne' togliere informazioni. Se l'originale inglese e' un segnaposto di sviluppo (contiene 占位 o "placeholder"), traduci alla lettera senza commenti.
- Se l'italiano attuale e' corretto, NON toccarlo (non riscrivere per gusto).

## Decisioni di glossario (fisse, valgono per tutti)
- "Umbral X" (varianti di Aniimo): resta "Umbral X" in inglese; "Umbral Society" resta inglese.
- Ultimate (mossa/abilita' finale): "la Suprema" ("Costo della Suprema", "DMG della Suprema", "Resistenza alla Suprema").
- Bloom (sistema): "Fioritura". Pathfinder e Trailblazer: "Pioniere". "Senior" (titolo): resta "Senior".
- "RV Park" resta inglese. Mosse "Charged ..." restano inglesi. Twine = "Intreccio"; Bond = "Legame". Home = "Casa". Badge = "Distintivo". Held items = "oggetti equipaggiati".
- Crit Rate = "Tasso CRIT". "Ultimate DMG" = "DMG della Suprema".
- "Alpha" (Aniimo capo) resta "Alpha" (mai "Alfa"), nell'ordine dell'inglese ("Alpha Aniimo", "Alpha Pomegg"). Nurture = "Accudimento". Affix: lascia com'e'.
- Elemento Dark = "Oscurità"; Light = "Luce". Talenti/abilita' con nome in Title Case restano inglesi.
- Branch = "Sezione" (mai "Filiale"/"Ramo"); Breezy (aggettivo nei titoli) resta "Breezy" se fa parte di un nome.
- "RV Lv. N" = "Camper Liv. N" (accettato); "RV Park" resta inglese.
- Frasi lunghe (dialoghi, descrizioni, missioni): controlla soprattutto senso fedele all'inglese, naturalezza, nomi/BREAK/sigle secondo le regole, coerenza col glossario. Non riscrivere per gusto se il testo e' corretto.
- Elemento Psychic = "Psico".
