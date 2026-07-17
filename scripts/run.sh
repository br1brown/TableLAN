#!/usr/bin/env bash
# Avvio di TableLAN su Linux. Viaggia dentro lo zip della release, accanto
# all'eseguibile.
#
# La finestra nativa (Photino) su Linux si appoggia a librerie di sistema: non
# si possono impacchettare nel single-file, e non sono garantite su una
# macchina pulita. Quindi non si chiede e non si installa niente — si guarda se
# ci sono. Se ci sono, finestra nativa. Se no, il server parte senza finestra e
# fa da finestra il browser di sistema, che per un tavolo va uguale.
set -euo pipefail

# Il doppio clic dal file manager parte dalla home, non da qui: senza questo
# cd l'eseguibile non si troverebbe, e soprattutto il database finirebbe
# nella cartella sbagliata.
cd "$(dirname "$(readlink -f "$0")")"

# Due librerie, non una. WebKitGTK è quella ovvia — è il browser dentro la
# finestra. libnotify è la sorpresa: Photino.Native ci si lega comunque, anche
# se TableLAN non manda nessuna notifica, e senza non si carica.
#
# Controllarle entrambe non è pignoleria: se ne manca una sola e si prova ad
# aprire la finestra, l'app non ripiega — muore con uno stack trace davanti al
# Master. Il ripiego qui sotto esiste per non far succedere questo, e serve a
# qualcosa solo se la condizione copre tutto ciò che serve davvero.
manca=""
ldconfig -p 2>/dev/null | grep -q webkit2gtk    || manca="WebKitGTK"
ldconfig -p 2>/dev/null | grep -q libnotify.so.4 || manca="${manca:+$manca e }libnotify"

if [ -z "$manca" ]; then
  exec ./TableLAN "$@"
fi

echo "  Manca $manca: TableLAN parte nel browser invece che in una finestra"
echo "  propria. Va bene così — per la finestra nativa, vedi il README."
echo

port="${TABLELAN_PORT:-5000}"
url="http://127.0.0.1:${port}/admin/"

./TableLAN --headless "$@" &
server=$!
trap 'kill "$server" 2>/dev/null || true' EXIT

# Si aspetta che Kestrel sia in ascolto prima di aprire il browser, altrimenti
# si apre su una pagina di errore. /dev/tcp è una primitiva di bash: niente
# curl o netcat da avere installati.
for _ in $(seq 1 100); do
  if (exec 3<>"/dev/tcp/127.0.0.1/${port}") 2>/dev/null; then
    exec 3>&- 2>/dev/null || true
    break
  fi
  sleep 0.1
done

# Se manca xdg-open non è un errore: il QR e gli indirizzi sono già a schermo.
xdg-open "$url" >/dev/null 2>&1 || true

# Restituisce il terminale al server: Ctrl-C lo ferma, e il trap chiude tutto.
wait "$server"
