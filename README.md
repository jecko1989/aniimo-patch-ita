# Aniimo - Patch italiana

Traduzione italiana per Aniimo. Sostituisce lo slot lingua "Indonesia" con "Italiano".
La patch si installa e si aggiorna solo tramite il launcher.

## Uso

1. Scarica `AniimoPatchITA.exe` dalla pagina [Releases](../../releases) (una volta sola).
2. Avvialo e controlla la cartella `Aniimo_Data` del gioco (usa **Sfoglia...** se non è quella giusta).
3. Premi **Installa / Aggiorna**: il launcher scarica l'ultima versione della patch e la applica.
4. Premi **Avvia Aniimo** e scegli la lingua **Italiano** nel menu del gioco (è lo slot Indonesia).

Per aggiornare la traduzione basta riaprire il launcher e premere di nuovo **Installa / Aggiorna**:
non serve riscaricare l'exe.

## Note

- **Ripristina originale** rimette i file originali del gioco (il launcher fa un backup prima di installare).
- Dopo un aggiornamento del gioco rilancia **Installa / Aggiorna**.
- Nomi di Aniimo, PNG, luoghi e abilità restano in inglese; BREAK non è tradotto.
- Il popup di Steam "Selamat datang" è in indonesiano perché il gioco comunica a Steam quello slot: non dipende dalla patch.

## Contenuto del repo

- `manifest.json`: versione corrente e checksum
- `*.gz`: file di traduzione compressi, scaricati dal launcher
- `launcher/`: sorgenti del launcher
- `tools/`: strumenti per generare la patch
