namespace HdrSwitch.Core.Localization;

/// <summary>
/// German and Turkish text, keyed by the English source string.
///
/// German uses the formal "Sie" and Turkish the formal "siz", as Windows itself does. Turkish
/// sentences are built so that no suffix is attached to a placeholder: vowel harmony depends on
/// the word, and an app or display name cannot be predicted ("{0} için", not "{0}'da").
/// LocalizationTests fails when a string in the source is missing here or its placeholders differ.
/// </summary>
internal static class Translations
{
    internal static readonly IReadOnlyDictionary<string, string> German = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Language picker
        ["Automatic (Windows language)"] = "Automatisch (Windows-Sprache)",
        ["Language"] = "Sprache",

        // Hotkey registration
        ["{0} is already claimed by another application. Pick a different combination in Settings."] =
            "{0} wird bereits von einer anderen Anwendung verwendet. Wählen Sie in den Einstellungen eine andere Kombination.",
        ["Could not register {0} (Win32 error {1})."] = "{0} konnte nicht registriert werden (Win32-Fehler {1}).",

        // Settings
        ["HDR Switch — Settings"] = "HDR Switch — Einstellungen",
        ["Close"] = "Schließen",
        ["Save"] = "Speichern",
        ["General"] = "Allgemein",
        ["Start HDR Switch when I sign in"] = "HDR Switch bei der Anmeldung starten",
        ["Show a brief confirmation when HDR changes"] = "Kurze Bestätigung anzeigen, wenn sich HDR ändert",
        ["Global hotkey"] = "Globales Tastenkürzel",
        ["e.g. Ctrl+Alt+H, Win+Shift+F9"] = "z. B. Ctrl+Alt+H, Win+Shift+F9",
        ["Command line (for desktop shortcuts, Stream Deck, AutoHotkey)"] = "Befehlszeile (für Verknüpfungen, Stream Deck, AutoHotkey)",
        ["Updates — you have version {0}"] = "Updates — installiert ist Version {0}",
        ["Check for updates now"] = "Jetzt nach Updates suchen",
        ["Screen sharing"] = "Bildschirmfreigabe",
        ["Windows records which apps capture the screen. When one starts while HDR is on, HDR Switch can offer to turn HDR off so viewers do not see washed-out colour."] =
            "Windows erfasst, welche Apps den Bildschirm aufnehmen. Startet eine davon bei eingeschaltetem HDR, kann HDR Switch anbieten, HDR auszuschalten, damit Zuschauer keine blassen Farben sehen.",
        ["Watch for screen sharing"] = "Auf Bildschirmfreigabe achten",
        ["Offer to restore HDR when sharing ends"] = "Nach der Freigabe anbieten, HDR wieder einzuschalten",
        ["Dismiss the prompt after"] = "Hinweis ausblenden nach",
        ["seconds"] = "Sekunden",
        ["What HDR Switch has learned"] = "Was HDR Switch gelernt hat",
        ["App"] = "App",
        ["When it shares my screen"] = "Wenn sie meinen Bildschirm teilt",
        ["Answers"] = "Antworten",
        ["Ask me"] = "Mich fragen",
        ["Always turn HDR off"] = "HDR immer ausschalten",
        ["Never ask"] = "Nie fragen",
        ["Forget"] = "Vergessen",
        ["Games"] = "Spiele",
        ["Turn HDR on automatically while a game is running, and put it back when the game exits."] =
            "HDR automatisch einschalten, solange ein Spiel läuft, und danach wieder zurücksetzen.",
        ["Watch for games"] = "Auf Spiele achten",
        ["Executable"] = "Programmdatei",
        ["Name"] = "Name",
        ["Displays"] = "Bildschirme",
        ["Add game…"] = "Spiel hinzufügen…",
        ["Remove"] = "Entfernen",
        ["Advanced"] = "Erweitert",
        ["Also guess from running processes (approximate)"] = "Zusätzlich anhand laufender Prozesse schätzen (ungenau)",
        ["Windows only records apps that capture through the modern API — which on Windows 11 is essentially all of them. Older capture tools do not appear at all. This fallback simply checks whether an executable is running, so it cannot tell \"open\" from \"sharing\" and will produce false alarms. One executable per line."] =
            "Windows erfasst nur Apps, die über die moderne Schnittstelle aufnehmen — unter Windows 11 sind das praktisch alle. Ältere Aufnahmeprogramme erscheinen gar nicht. Diese Notlösung prüft nur, ob eine Programmdatei läuft, kann also „geöffnet“ nicht von „teilt“ unterscheiden und meldet Fehlalarme. Eine Programmdatei pro Zeile.",
        ["Insert common capture tools"] = "Gängige Aufnahmeprogramme einfügen",
        ["Diagnostics"] = "Diagnose",
        ["Turn HDR off automatically"] = "HDR automatisch ausschalten",
        ["Leave HDR alone, stay quiet"] = "HDR nicht ändern, nicht melden",
        ["off {0} / keep {1}"] = "aus {0} / behalten {1}",
        ["Nothing learned yet"] = "Noch nichts gelernt",
        ["All capable"] = "Alle geeigneten",
        ["{0} selected"] = "{0} ausgewählt",
        ["resolved on first use"] = "wird bei der ersten Verwendung ermittelt",
        ["Display API path: {0}"] = "Bildschirm-API: {0}",
        ["Settings file: {0}"] = "Einstellungsdatei: {0}",
        ["Displays detected: {0} ({1} HDR-capable)"] = "Erkannte Bildschirme: {0} ({1} HDR-fähig)",
        ["The hotkey is off. HDR Switch still responds to the tray icon and the command line."] =
            "Das Tastenkürzel ist aus. HDR Switch reagiert weiterhin auf das Infobereichssymbol und die Befehlszeile.",
        ["Will register {0}."] = "{0} wird registriert.",
        ["That hotkey cannot be used."] = "Dieses Tastenkürzel kann nicht verwendet werden.",
        ["Pick the game executable"] = "Programmdatei des Spiels auswählen",
        ["Programs (*.exe)|*.exe|All files (*.*)|*.*"] = "Programme (*.exe)|*.exe|Alle Dateien (*.*)|*.*",
        ["{0} is already in the list."] = "{0} ist bereits in der Liste.",
        ["Could not change the startup entry: {0}"] = "Der Autostart-Eintrag konnte nicht geändert werden: {0}",

        // Sharing prompt
        ["Keep HDR"] = "HDR behalten",
        ["Turn HDR off"] = "HDR ausschalten",
        ["Turn HDR off on {0}"] = "HDR ausschalten auf {0}",
        ["All screens"] = "Alle Bildschirme",
        ["Never ask for {0}"] = "Für {0} nie fragen",

        // Tray
        ["HDR Switch is running"] = "HDR Switch läuft",
        ["Click the tray icon to flip HDR, or press {0}. If an app starts sharing your screen while HDR is on, you'll get a heads-up."] =
            "Klicken Sie auf das Symbol im Infobereich oder drücken Sie {0}, um HDR umzuschalten. Beginnt eine App bei eingeschaltetem HDR, Ihren Bildschirm zu teilen, erhalten Sie einen Hinweis.",
        ["{0} apps"] = "{0} Apps",
        ["{0} sharing · HDR on"] = "{0} teilt · HDR ein",
        ["{0} sharing · HDR off"] = "{0} teilt · HDR aus",
        ["HDR Switch — no HDR-capable display"] = "HDR Switch — kein HDR-fähiger Bildschirm",
        ["HDR Switch — {0}: HDR on"] = "HDR Switch — {0}: HDR ein",
        ["HDR Switch — {0}: HDR off"] = "HDR Switch — {0}: HDR aus",
        ["HDR Switch — HDR all off"] = "HDR Switch — HDR überall aus",
        ["HDR Switch — HDR all on"] = "HDR Switch — HDR überall ein",
        ["HDR Switch — HDR {0} of {1} on"] = "HDR Switch — HDR {0} von {1} ein",
        ["No displays detected"] = "Keine Bildschirme erkannt",
        ["Turn all HDR off"] = "HDR überall ausschalten",
        ["Turn all HDR on"] = "HDR überall einschalten",
        ["Warn me when sharing my screen"] = "Warnen, wenn ich meinen Bildschirm teile",
        ["Settings…"] = "Einstellungen…",
        ["Check for updates…"] = "Nach Updates suchen…",
        ["Exit"] = "Beenden",
        ["● {0} may be capturing your screen"] = "● {0} nimmt möglicherweise Ihren Bildschirm auf",
        ["● {0} is sharing your screen"] = "● {0} teilt Ihren Bildschirm",
        ["Restore HDR now ({0})"] = "HDR jetzt wieder einschalten ({0})",
        ["Restore HDR now"] = "HDR jetzt wieder einschalten",
        ["Turn HDR off now…"] = "HDR jetzt ausschalten…",
        ["Auto HDR for games"] = "Auto HDR für Spiele",
        ["Auto HDR enabled"] = "Auto HDR eingeschaltet",
        ["Auto HDR disabled"] = "Auto HDR ausgeschaltet",
        ["Games that are already running need to be restarted before this takes effect."] =
            "Bereits laufende Spiele müssen neu gestartet werden, damit dies wirkt.",
        ["Could not change Auto HDR"] = "Auto HDR konnte nicht geändert werden",
        ["No HDR-capable display"] = "Kein HDR-fähiger Bildschirm",
        ["HDR is blocked by system policy on {0}."] = "HDR ist durch eine Systemrichtlinie gesperrt auf: {0}.",
        ["None of the connected displays report HDR support."] = "Keiner der angeschlossenen Bildschirme meldet HDR-Unterstützung.",
        ["HDR did not change"] = "HDR wurde nicht geändert",
        ["{0} failed."] = "{0}: fehlgeschlagen.",
        ["HDR on"] = "HDR ein",
        ["HDR off"] = "HDR aus",
        ["Screen-share detection degraded"] = "Erkennung der Bildschirmfreigabe eingeschränkt",
        ["{0} is running and may be capturing. HDR is on for {1}, which usually looks washed out and desaturated to whoever is watching."] =
            "{0} läuft und nimmt möglicherweise auf. HDR ist eingeschaltet auf {1} — für Zuschauer wirkt das meist blass und entsättigt.",
        ["HDR is on for {0} screens. Which one are you sharing? The other keeps HDR; captured HDR reaches viewers washed out."] =
            "HDR ist auf {0} Bildschirmen eingeschaltet. Welchen teilen Sie? Der andere behält HDR; aufgenommenes HDR kommt bei Zuschauern blass an.",
        ["HDR is on for {0}. Captured HDR usually reaches viewers washed out and desaturated, because it gets flattened to SDR on the way."] =
            "HDR ist eingeschaltet auf {0}. Aufgenommenes HDR kommt bei Zuschauern meist blass und entsättigt an, weil es unterwegs auf SDR reduziert wird.",
        ["{0} may be capturing your screen"] = "{0} nimmt möglicherweise Ihren Bildschirm auf",
        ["{0} is sharing your screen"] = "{0} teilt Ihren Bildschirm",
        ["Learned: HDR off for {0}"] = "Gelernt: HDR aus für {0}",
        ["Next time it shares your screen, HDR will switch off automatically on the screen you picked. You can change this in Settings."] =
            "Wenn diese App das nächste Mal Ihren Bildschirm teilt, wird HDR auf dem gewählten Bildschirm automatisch ausgeschaltet. Sie können das in den Einstellungen ändern.",
        ["Next time it shares your screen, HDR will switch off automatically. You can change this in Settings."] =
            "Wenn diese App das nächste Mal Ihren Bildschirm teilt, wird HDR automatisch ausgeschaltet. Sie können das in den Einstellungen ändern.",
        ["HDR off — {0} is sharing"] = "HDR aus — {0} teilt",
        ["Switched off on {0}, other screens left alone, because that is what you chose before."] =
            "Ausgeschaltet auf {0}, andere Bildschirme unverändert — so hatten Sie es zuvor gewählt.",
        ["Switched off on {0}, because that is what you chose before."] = "Ausgeschaltet auf {0} — so hatten Sie es zuvor gewählt.",
        ["Undo and ask me next time"] = "Rückgängig und nächstes Mal fragen",
        ["HDR restored for {0}"] = "HDR wieder eingeschaltet für {0}",
        ["HDR Switch will ask again next time instead of deciding for you."] = "HDR Switch fragt nächstes Mal wieder, statt für Sie zu entscheiden.",
        ["{0} stopped sharing"] = "{0} teilt nicht mehr",
        ["HDR is still off. Want it back on?"] = "HDR ist noch aus. Wieder einschalten?",
        ["Turn HDR back on"] = "HDR wieder einschalten",
        ["HDR restored"] = "HDR wieder eingeschaltet",
        ["HDR on for {0}"] = "HDR ein für {0}",
        ["It will go back off when the game exits."] = "Es wird wieder ausgeschaltet, wenn das Spiel beendet wird.",
        ["Hotkey unavailable"] = "Tastenkürzel nicht verfügbar",

        // Updates
        ["HDR Switch updated to {0}"] = "HDR Switch auf {0} aktualisiert",
        ["Your settings and learned rules carried over."] = "Ihre Einstellungen und gelernten Regeln wurden übernommen.",
        ["What's new"] = "Neuigkeiten",
        ["Could not check for updates"] = "Suche nach Updates fehlgeschlagen",
        ["HDR Switch is up to date"] = "HDR Switch ist auf dem neuesten Stand",
        ["You have version {0}, the latest release."] = "Sie haben Version {0}, die neueste Version.",
        ["HDR Switch {0} is available"] = "HDR Switch {0} ist verfügbar",
        ["You have {0}. This build cannot update itself; download the new one from the release page."] =
            "Sie haben {0}. Dieser Build kann sich nicht selbst aktualisieren; laden Sie die neue Version von der Release-Seite herunter.",
        ["Open release page"] = "Release-Seite öffnen",
        ["Update now"] = "Jetzt aktualisieren",
        ["What's new:"] = "Neu:",
        ["You have {0}. Updating downloads the new version, checks it against the published SHA-256 and restarts, in a few seconds."] =
            "Sie haben {0}. Das Update lädt die neue Version herunter, prüft sie anhand der veröffentlichten SHA-256-Prüfsumme und startet neu — in wenigen Sekunden.",
        ["Updating to {0}…"] = "Aktualisierung auf {0}…",
        ["Downloading."] = "Wird heruntergeladen.",
        ["Updated to {0} — restart to finish"] = "Auf {0} aktualisiert — zum Abschließen neu starten",
        ["The new version is installed but did not start ({0}). Exit HDR Switch and start it again."] =
            "Die neue Version ist installiert, wurde aber nicht gestartet ({0}). Beenden Sie HDR Switch und starten Sie es erneut.",
        ["HDR Switch cannot write to {0}. Move HdrSwitch.exe to a folder you own (for example Documents or Desktop), or download the update by hand."] =
            "HDR Switch kann nicht in {0} schreiben. Verschieben Sie HdrSwitch.exe in einen eigenen Ordner (etwa Dokumente oder Desktop) oder laden Sie das Update manuell herunter.",
        ["Update failed — nothing was changed"] = "Update fehlgeschlagen — nichts wurde geändert",
        ["GitHub did not answer in time. Check your connection and try again."] =
            "GitHub hat nicht rechtzeitig geantwortet. Prüfen Sie Ihre Verbindung und versuchen Sie es erneut.",
        ["GitHub answered {0} {1}."] = "GitHub antwortete mit {0} {1}.",
        ["Could not reach GitHub. Check your connection and try again."] =
            "GitHub ist nicht erreichbar. Prüfen Sie Ihre Verbindung und versuchen Sie es erneut.",
        ["Could not open the browser"] = "Der Browser konnte nicht geöffnet werden",

        // Hotkey parsing
        ["No hotkey specified."] = "Kein Tastenkürzel angegeben.",
        ["'{0}' names more than one key. Use modifiers plus a single key."] =
            "„{0}“ enthält mehr als eine Taste. Verwenden Sie Zusatztasten plus eine einzelne Taste.",
        ["'{0}' is not a key HDR Switch recognises."] = "„{0}“ ist keine Taste, die HDR Switch kennt.",
        ["'{0}' has no main key -- add one, e.g. Ctrl+Alt+H."] = "„{0}“ hat keine Haupttaste — fügen Sie eine hinzu, z. B. Ctrl+Alt+H.",
        ["A global hotkey needs at least one modifier (Ctrl, Alt, Shift or Win)."] =
            "Ein globales Tastenkürzel braucht mindestens eine Zusatztaste (Ctrl, Alt, Shift oder Win).",

        // Settings file
        ["Could not read settings ({0}). Defaults are in use."] =
            "Einstellungen konnten nicht gelesen werden ({0}). Es gelten die Standardwerte.",

        // Displays
        ["Display {0}"] = "Bildschirm {0}",
        ["HDR not supported"] = "HDR nicht unterstützt",
        ["HDR blocked by system policy"] = "HDR durch Systemrichtlinie gesperrt",
        ["{0} does not support HDR."] = "{0} unterstützt kein HDR.",
        ["Windows accepted the change but {0} stayed on. The display or link may not allow it right now."] =
            "Windows hat die Änderung angenommen, aber {0} blieb eingeschaltet. Bildschirm oder Verbindung lassen es derzeit womöglich nicht zu.",
        ["Windows accepted the change but {0} stayed off. The display or link may not allow it right now."] =
            "Windows hat die Änderung angenommen, aber {0} blieb ausgeschaltet. Bildschirm oder Verbindung lassen es derzeit womöglich nicht zu.",
        ["Windows rejected the HDR request for {0} (invalid parameter). This usually means the display no longer matches the cached configuration -- rescan and retry."] =
            "Windows hat die HDR-Anfrage für {0} abgelehnt (ungültiger Parameter). Meist passt der Bildschirm nicht mehr zur zwischengespeicherten Konfiguration — neu erkennen und erneut versuchen.",
        ["{0} reports HDR support but the driver refused the request."] = "{0} meldet HDR-Unterstützung, aber der Treiber hat die Anfrage abgelehnt.",
        ["Access denied changing HDR on {0}."] = "Zugriff verweigert beim Ändern von HDR auf {0}.",
        ["The display driver failed the HDR request for {0}."] = "Der Grafiktreiber konnte die HDR-Anfrage für {0} nicht ausführen.",
        ["Changing HDR on {0} failed with Win32 error {1}."] = "Das Ändern von HDR auf {0} ist mit Win32-Fehler {1} fehlgeschlagen.",

        // Capture watcher
        ["Could not subscribe to registry change notifications; falling back to polling every 10 seconds."] =
            "Registrierungsänderungen können nicht abonniert werden; stattdessen wird alle 10 Sekunden geprüft.",
    };

    internal static readonly IReadOnlyDictionary<string, string> Turkish = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Language picker
        ["Automatic (Windows language)"] = "Otomatik (Windows dili)",
        ["Language"] = "Dil",

        // Hotkey registration
        ["{0} is already claimed by another application. Pick a different combination in Settings."] =
            "{0} başka bir uygulama tarafından kullanılıyor. Ayarlar'dan farklı bir kombinasyon seçin.",
        ["Could not register {0} (Win32 error {1})."] = "Kısayol kaydedilemedi: {0} (Win32 hatası {1}).",

        // Settings
        ["HDR Switch — Settings"] = "HDR Switch — Ayarlar",
        ["Close"] = "Kapat",
        ["Save"] = "Kaydet",
        ["General"] = "Genel",
        ["Start HDR Switch when I sign in"] = "Oturum açtığımda HDR Switch'i başlat",
        ["Show a brief confirmation when HDR changes"] = "HDR değiştiğinde kısa bir onay göster",
        ["Global hotkey"] = "Genel kısayol tuşu",
        ["e.g. Ctrl+Alt+H, Win+Shift+F9"] = "örn. Ctrl+Alt+H, Win+Shift+F9",
        ["Command line (for desktop shortcuts, Stream Deck, AutoHotkey)"] = "Komut satırı (masaüstü kısayolları, Stream Deck, AutoHotkey için)",
        ["Updates — you have version {0}"] = "Güncellemeler — yüklü sürüm: {0}",
        ["Check for updates now"] = "Güncellemeleri şimdi denetle",
        ["Screen sharing"] = "Ekran paylaşımı",
        ["Windows records which apps capture the screen. When one starts while HDR is on, HDR Switch can offer to turn HDR off so viewers do not see washed-out colour."] =
            "Windows, ekranı hangi uygulamaların yakaladığını kaydeder. HDR açıkken biri başladığında HDR Switch, izleyenler soluk renkler görmesin diye HDR'yi kapatmayı önerebilir.",
        ["Watch for screen sharing"] = "Ekran paylaşımını izle",
        ["Offer to restore HDR when sharing ends"] = "Paylaşım bitince HDR'yi geri açmayı öner",
        ["Dismiss the prompt after"] = "Bildirimi kapatma süresi:",
        ["seconds"] = "saniye",
        ["What HDR Switch has learned"] = "HDR Switch'in öğrendikleri",
        ["App"] = "Uygulama",
        ["When it shares my screen"] = "Ekranımı paylaştığında",
        ["Answers"] = "Yanıtlar",
        ["Ask me"] = "Bana sor",
        ["Always turn HDR off"] = "HDR'yi her zaman kapat",
        ["Never ask"] = "Asla sorma",
        ["Forget"] = "Unut",
        ["Games"] = "Oyunlar",
        ["Turn HDR on automatically while a game is running, and put it back when the game exits."] =
            "Bir oyun çalışırken HDR'yi otomatik aç, oyun kapanınca eski haline getir.",
        ["Watch for games"] = "Oyunları izle",
        ["Executable"] = "Program dosyası",
        ["Name"] = "Ad",
        ["Displays"] = "Ekranlar",
        ["Add game…"] = "Oyun ekle…",
        ["Remove"] = "Kaldır",
        ["Advanced"] = "Gelişmiş",
        ["Also guess from running processes (approximate)"] = "Çalışan işlemlerden de tahmin et (yaklaşık)",
        ["Windows only records apps that capture through the modern API — which on Windows 11 is essentially all of them. Older capture tools do not appear at all. This fallback simply checks whether an executable is running, so it cannot tell \"open\" from \"sharing\" and will produce false alarms. One executable per line."] =
            "Windows yalnızca modern API ile yakalama yapan uygulamaları kaydeder — Windows 11'de bu neredeyse hepsidir. Eski yakalama araçları hiç görünmez. Bu yedek yöntem yalnızca bir programın çalışıp çalışmadığına bakar; \"açık\" ile \"paylaşıyor\" arasındaki farkı anlayamaz ve yanlış alarm verir. Her satıra bir program dosyası.",
        ["Insert common capture tools"] = "Yaygın yakalama araçlarını ekle",
        ["Diagnostics"] = "Tanılama",
        ["Turn HDR off automatically"] = "HDR'yi otomatik kapat",
        ["Leave HDR alone, stay quiet"] = "HDR'ye dokunma, bildirim gösterme",
        ["off {0} / keep {1}"] = "kapat {0} / koru {1}",
        ["Nothing learned yet"] = "Henüz öğrenilen bir şey yok",
        ["All capable"] = "Uygun olanların tümü",
        ["{0} selected"] = "{0} seçili",
        ["resolved on first use"] = "ilk kullanımda belirlenir",
        ["Display API path: {0}"] = "Ekran API yolu: {0}",
        ["Settings file: {0}"] = "Ayar dosyası: {0}",
        ["Displays detected: {0} ({1} HDR-capable)"] = "Algılanan ekranlar: {0} ({1} tanesi HDR destekli)",
        ["The hotkey is off. HDR Switch still responds to the tray icon and the command line."] =
            "Kısayol tuşu kapalı. HDR Switch, bildirim alanı simgesine ve komut satırına yanıt vermeye devam eder.",
        ["Will register {0}."] = "Kaydedilecek kısayol: {0}.",
        ["That hotkey cannot be used."] = "Bu kısayol tuşu kullanılamaz.",
        ["Pick the game executable"] = "Oyunun program dosyasını seçin",
        ["Programs (*.exe)|*.exe|All files (*.*)|*.*"] = "Programlar (*.exe)|*.exe|Tüm dosyalar (*.*)|*.*",
        ["{0} is already in the list."] = "Zaten listede: {0}.",
        ["Could not change the startup entry: {0}"] = "Başlangıç kaydı değiştirilemedi: {0}",

        // Sharing prompt
        ["Keep HDR"] = "HDR'yi koru",
        ["Turn HDR off"] = "HDR'yi kapat",
        ["Turn HDR off on {0}"] = "HDR'yi kapat: {0}",
        ["All screens"] = "Tüm ekranlar",
        ["Never ask for {0}"] = "{0} için bir daha sorma",

        // Tray
        ["HDR Switch is running"] = "HDR Switch çalışıyor",
        ["Click the tray icon to flip HDR, or press {0}. If an app starts sharing your screen while HDR is on, you'll get a heads-up."] =
            "HDR'yi değiştirmek için bildirim alanı simgesine tıklayın veya {0} tuşlarına basın. HDR açıkken bir uygulama ekranınızı paylaşmaya başlarsa sizi uyaracağız.",
        ["{0} apps"] = "{0} uygulama",
        ["{0} sharing · HDR on"] = "{0} paylaşıyor · HDR açık",
        ["{0} sharing · HDR off"] = "{0} paylaşıyor · HDR kapalı",
        ["HDR Switch — no HDR-capable display"] = "HDR Switch — HDR destekli ekran yok",
        ["HDR Switch — {0}: HDR on"] = "HDR Switch — {0}: HDR açık",
        ["HDR Switch — {0}: HDR off"] = "HDR Switch — {0}: HDR kapalı",
        ["HDR Switch — HDR all off"] = "HDR Switch — HDR hepsinde kapalı",
        ["HDR Switch — HDR all on"] = "HDR Switch — HDR hepsinde açık",
        ["HDR Switch — HDR {0} of {1} on"] = "HDR Switch — HDR {1} ekranın {0} tanesinde açık",
        ["No displays detected"] = "Ekran algılanmadı",
        ["Turn all HDR off"] = "Tüm ekranlarda HDR'yi kapat",
        ["Turn all HDR on"] = "Tüm ekranlarda HDR'yi aç",
        ["Warn me when sharing my screen"] = "Ekranımı paylaşırken beni uyar",
        ["Settings…"] = "Ayarlar…",
        ["Check for updates…"] = "Güncellemeleri denetle…",
        ["Exit"] = "Çıkış",
        ["● {0} may be capturing your screen"] = "● {0} ekranınızı yakalıyor olabilir",
        ["● {0} is sharing your screen"] = "● {0} ekranınızı paylaşıyor",
        ["Restore HDR now ({0})"] = "HDR'yi şimdi geri aç ({0})",
        ["Restore HDR now"] = "HDR'yi şimdi geri aç",
        ["Turn HDR off now…"] = "HDR'yi şimdi kapat…",
        ["Auto HDR for games"] = "Oyunlar için Otomatik HDR",
        ["Auto HDR enabled"] = "Otomatik HDR açıldı",
        ["Auto HDR disabled"] = "Otomatik HDR kapatıldı",
        ["Games that are already running need to be restarted before this takes effect."] =
            "Bunun etkili olması için çalışmakta olan oyunların yeniden başlatılması gerekir.",
        ["Could not change Auto HDR"] = "Otomatik HDR değiştirilemedi",
        ["No HDR-capable display"] = "HDR destekli ekran yok",
        ["HDR is blocked by system policy on {0}."] = "HDR, sistem ilkesi tarafından engellendi: {0}.",
        ["None of the connected displays report HDR support."] = "Bağlı ekranların hiçbiri HDR desteği bildirmiyor.",
        ["HDR did not change"] = "HDR değişmedi",
        ["{0} failed."] = "{0}: başarısız oldu.",
        ["HDR on"] = "HDR açık",
        ["HDR off"] = "HDR kapalı",
        ["Screen-share detection degraded"] = "Ekran paylaşımı algılama kısıtlı çalışıyor",
        ["{0} is running and may be capturing. HDR is on for {1}, which usually looks washed out and desaturated to whoever is watching."] =
            "{0} çalışıyor ve yakalama yapıyor olabilir. {1} için HDR açık; bu, izleyenlere genellikle soluk ve donuk görünür.",
        ["HDR is on for {0} screens. Which one are you sharing? The other keeps HDR; captured HDR reaches viewers washed out."] =
            "{0} ekranda HDR açık. Hangisini paylaşıyorsunuz? Diğeri HDR'de kalır; yakalanan HDR izleyenlere soluk ulaşır.",
        ["HDR is on for {0}. Captured HDR usually reaches viewers washed out and desaturated, because it gets flattened to SDR on the way."] =
            "{0} için HDR açık. Yakalanan HDR yolda SDR'ye indirgendiği için izleyenlere genellikle soluk ve donuk ulaşır.",
        ["{0} may be capturing your screen"] = "{0} ekranınızı yakalıyor olabilir",
        ["{0} is sharing your screen"] = "{0} ekranınızı paylaşıyor",
        ["Learned: HDR off for {0}"] = "Öğrenildi: {0} için HDR kapalı",
        ["Next time it shares your screen, HDR will switch off automatically on the screen you picked. You can change this in Settings."] =
            "Bu uygulama ekranınızı bir dahaki paylaşışında, seçtiğiniz ekranda HDR otomatik kapanacak. Bunu Ayarlar'dan değiştirebilirsiniz.",
        ["Next time it shares your screen, HDR will switch off automatically. You can change this in Settings."] =
            "Bu uygulama ekranınızı bir dahaki paylaşışında HDR otomatik kapanacak. Bunu Ayarlar'dan değiştirebilirsiniz.",
        ["HDR off — {0} is sharing"] = "HDR kapalı — {0} paylaşıyor",
        ["Switched off on {0}, other screens left alone, because that is what you chose before."] =
            "Kapatıldı: {0}. Diğer ekranlara dokunulmadı; daha önce böyle seçmiştiniz.",
        ["Switched off on {0}, because that is what you chose before."] = "Kapatıldı: {0}. Daha önce böyle seçmiştiniz.",
        ["Undo and ask me next time"] = "Geri al ve bir dahaki sefere sor",
        ["HDR restored for {0}"] = "{0} için HDR geri açıldı",
        ["HDR Switch will ask again next time instead of deciding for you."] = "HDR Switch bir dahaki sefere sizin yerinize karar vermek yerine yine soracak.",
        ["{0} stopped sharing"] = "{0} paylaşımı durdurdu",
        ["HDR is still off. Want it back on?"] = "HDR hâlâ kapalı. Geri açılsın mı?",
        ["Turn HDR back on"] = "HDR'yi geri aç",
        ["HDR restored"] = "HDR geri açıldı",
        ["HDR on for {0}"] = "{0} için HDR açık",
        ["It will go back off when the game exits."] = "Oyun kapanınca yeniden kapanacak.",
        ["Hotkey unavailable"] = "Kısayol tuşu kullanılamıyor",

        // Updates
        ["HDR Switch updated to {0}"] = "HDR Switch güncellendi: {0}",
        ["Your settings and learned rules carried over."] = "Ayarlarınız ve öğrenilen kurallar korundu.",
        ["What's new"] = "Yenilikler",
        ["Could not check for updates"] = "Güncellemeler denetlenemedi",
        ["HDR Switch is up to date"] = "HDR Switch güncel",
        ["You have version {0}, the latest release."] = "En son sürüm olan {0} yüklü.",
        ["HDR Switch {0} is available"] = "HDR Switch {0} kullanıma sunuldu",
        ["You have {0}. This build cannot update itself; download the new one from the release page."] =
            "Yüklü sürüm: {0}. Bu derleme kendini güncelleyemez; yeni sürümü yayın sayfasından indirin.",
        ["Open release page"] = "Yayın sayfasını aç",
        ["Update now"] = "Şimdi güncelle",
        ["What's new:"] = "Yenilikler:",
        ["You have {0}. Updating downloads the new version, checks it against the published SHA-256 and restarts, in a few seconds."] =
            "Yüklü sürüm: {0}. Güncelleme yeni sürümü indirir, yayınlanan SHA-256 ile doğrular ve birkaç saniye içinde yeniden başlatır.",
        ["Updating to {0}…"] = "Güncelleniyor: {0}…",
        ["Downloading."] = "İndiriliyor.",
        ["Updated to {0} — restart to finish"] = "Güncellendi: {0} — tamamlamak için yeniden başlatın",
        ["The new version is installed but did not start ({0}). Exit HDR Switch and start it again."] =
            "Yeni sürüm yüklendi ancak başlamadı ({0}). HDR Switch'ten çıkıp yeniden başlatın.",
        ["HDR Switch cannot write to {0}. Move HdrSwitch.exe to a folder you own (for example Documents or Desktop), or download the update by hand."] =
            "HDR Switch şu klasöre yazamıyor: {0}. HdrSwitch.exe'yi size ait bir klasöre (örneğin Belgeler veya Masaüstü) taşıyın ya da güncellemeyi elle indirin.",
        ["Update failed — nothing was changed"] = "Güncelleme başarısız — hiçbir şey değişmedi",
        ["GitHub did not answer in time. Check your connection and try again."] =
            "GitHub zamanında yanıt vermedi. Bağlantınızı kontrol edip yeniden deneyin.",
        ["GitHub answered {0} {1}."] = "GitHub yanıtı: {0} {1}.",
        ["Could not reach GitHub. Check your connection and try again."] =
            "GitHub'a ulaşılamadı. Bağlantınızı kontrol edip yeniden deneyin.",
        ["Could not open the browser"] = "Tarayıcı açılamadı",

        // Hotkey parsing
        ["No hotkey specified."] = "Kısayol tuşu belirtilmedi.",
        ["'{0}' names more than one key. Use modifiers plus a single key."] =
            "\"{0}\" birden fazla tuş içeriyor. Değiştirici tuşlar ve tek bir tuş kullanın.",
        ["'{0}' is not a key HDR Switch recognises."] = "\"{0}\", HDR Switch'in tanıdığı bir tuş değil.",
        ["'{0}' has no main key -- add one, e.g. Ctrl+Alt+H."] = "\"{0}\" içinde ana tuş yok — bir tane ekleyin, örn. Ctrl+Alt+H.",
        ["A global hotkey needs at least one modifier (Ctrl, Alt, Shift or Win)."] =
            "Genel kısayol tuşu en az bir değiştirici tuş gerektirir (Ctrl, Alt, Shift veya Win).",

        // Settings file
        ["Could not read settings ({0}). Defaults are in use."] =
            "Ayarlar okunamadı ({0}). Varsayılan ayarlar kullanılıyor.",

        // Displays
        ["Display {0}"] = "Ekran {0}",
        ["HDR not supported"] = "HDR desteklenmiyor",
        ["HDR blocked by system policy"] = "HDR sistem ilkesiyle engellendi",
        ["{0} does not support HDR."] = "{0} HDR desteklemiyor.",
        ["Windows accepted the change but {0} stayed on. The display or link may not allow it right now."] =
            "Windows değişikliği kabul etti ancak {0} açık kaldı. Ekran veya bağlantı şu anda buna izin vermiyor olabilir.",
        ["Windows accepted the change but {0} stayed off. The display or link may not allow it right now."] =
            "Windows değişikliği kabul etti ancak {0} kapalı kaldı. Ekran veya bağlantı şu anda buna izin vermiyor olabilir.",
        ["Windows rejected the HDR request for {0} (invalid parameter). This usually means the display no longer matches the cached configuration -- rescan and retry."] =
            "Windows, {0} için HDR isteğini reddetti (geçersiz parametre). Bu genellikle ekranın önbellekteki yapılandırmayla artık eşleşmediği anlamına gelir — yeniden tarayıp tekrar deneyin.",
        ["{0} reports HDR support but the driver refused the request."] = "{0} HDR desteği bildiriyor ancak sürücü isteği reddetti.",
        ["Access denied changing HDR on {0}."] = "HDR değiştirilirken erişim reddedildi: {0}.",
        ["The display driver failed the HDR request for {0}."] = "Ekran sürücüsü, {0} için HDR isteğini yerine getiremedi.",
        ["Changing HDR on {0} failed with Win32 error {1}."] = "HDR değiştirilemedi: {0} (Win32 hatası {1}).",

        // Capture watcher
        ["Could not subscribe to registry change notifications; falling back to polling every 10 seconds."] =
            "Kayıt defteri değişiklik bildirimlerine abone olunamadı; bunun yerine 10 saniyede bir denetlenecek.",
    };
}
