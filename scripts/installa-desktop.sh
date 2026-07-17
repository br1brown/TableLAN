#!/usr/bin/env bash
# Dà a TableLAN una faccia nel menù applicazioni di Ubuntu.
#
# Viaggia dentro lo zip della release, accanto a run.sh. Si lancia una volta:
# scrive la voce .desktop e installa l'icona nel tema dell'utente.
#
# Perché serve, se la finestra un'icona ce l'ha già: sono due cose diverse.
# SetIconFile dà l'icona alla finestra mentre gira; questa dà a TableLAN una
# voce nel menù, una faccia nel dock, e un posto da cui lanciarlo senza
# ricordarsi dove sta la cartella.
#
# Tutto sotto ~/.local: niente sudo, niente file fuori dalla home, e si
# disinstalla cancellando due file (lo dice in fondo).
set -euo pipefail

QUI="$(dirname "$(readlink -f "$0")")"
APP="$QUI/run.sh"
PNG="$QUI/tablelan.png"

for f in "$APP" "$PNG"; do
  if [ ! -f "$f" ]; then
    echo "Manca $(basename "$f"): lancia questo script dalla cartella dove hai estratto lo zip." >&2
    exit 1
  fi
done
chmod +x "$APP"

ICONE="$HOME/.local/share/icons/hicolor/256x256/apps"
VOCI="$HOME/.local/share/applications"
mkdir -p "$ICONE" "$VOCI"
cp "$PNG" "$ICONE/tablelan.png"

# I percorsi sono assoluti e calcolati adesso: una voce .desktop non ha una
# nozione di "cartella corrente", e Exec con un percorso relativo non parte.
#
# StartupWMClass lega la finestra a questa voce. Senza, GNOME non riconosce la
# finestra come "questa app" e nel dock spunta una seconda icona anonima
# accanto a quella giusta.
cat > "$VOCI/tablelan.desktop" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=TableLAN
Comment=Il tavolo da gioco in LAN: schede, tiri e combattimento
Exec=$APP
Icon=$ICONE/tablelan.png
Terminal=false
Categories=Game;RolePlaying;
StartupWMClass=TableLAN
EOF
chmod +x "$VOCI/tablelan.desktop"

# GNOME rilegge il menù da solo, ma non sempre subito: questo lo sveglia.
# Se il comando non c'è non è un problema — basta rifare il login.
update-desktop-database "$VOCI" 2>/dev/null || true
gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true

echo "Fatto: TableLAN è nel menù applicazioni."
echo "Se non lo vedi subito, esci e rientra dalla sessione."
echo
echo "Per toglierlo:"
echo "  rm ~/.local/share/applications/tablelan.desktop"
echo "  rm ~/.local/share/icons/hicolor/256x256/apps/tablelan.png"
