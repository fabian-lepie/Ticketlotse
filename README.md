# Ticketlotse

KI-gestützte Einordnung von Kundenservice-Tickets eines Stadtwerks.
Demo- und Bewerbungsprojekt von Fabian Lepie – sauberer, erklärbarer Code ist wichtiger als Funktionsumfang.

## Problem und Lösung

Bei einem Stadtwerk laufen täglich Kundenanfragen ein: Zählerstände, Abschlagsänderungen,
Umzüge, Störungsmeldungen. Sie von Hand zu sortieren und an das richtige Team zu verteilen,
kostet Zeit – und Eilfälle gehen in der Masse unter.

Ticketlotse nimmt eine Anfrage an, lässt sie von einem Sprachmodell einordnen (Kategorie,
Dringlichkeit, Konfidenz, Antwortvorschlag) und legt sie dem zuständigen Team vor.

**Die Einordnung ist ein Vorschlag, kein Urteil.** Freigegeben wird sie immer von einem
Menschen, und der kann sie vorher korrigieren. Jedes Ticket führt dazu einen Verlauf: der
KI-Vorschlag mit dem Modell, das geantwortet hat, und darunter jede menschliche Korrektur
mit Bearbeiter, Begründung und Zeitpunkt. Die letzte Einordnung gilt.

## Fachregeln

**Notfallregel.** Enthält ein Ticket einen Notfallbegriff – Gasgeruch, Stromausfall, kein
Strom, kein Wasser, Wasserrohrbruch –, ist die Dringlichkeit immer `Hoch`. Das gilt
unabhängig davon, was die KI vorschlägt **und** was ein Sachbearbeiter einträgt. Die Regel
liegt im Domänenmodell, nicht im Prompt und nicht in der API: Kein Prompt und kein Formular
kann sie aushebeln.

**Konfidenz-Schwelle 70 %.** Ist die KI zu mindestens 70 % sicher, wartet das Ticket auf
Freigabe (`WartetAufFreigabe`). Darunter geht es in die manuelle Prüfung
(`ManuellePruefung`). Eine menschliche Korrektur setzt den Status zurück auf
`WartetAufFreigabe` – durch die neue Kategorie ist oft ein anderes Team zuständig.

**Fällt die KI aus, geht keine Anfrage verloren.** Das Ticket wird zuerst gespeichert und
dann eingeordnet. Scheitert die Einordnung, bleibt es auf `Neu` liegen und lässt sich
später über einen eigenen Endpunkt nachholen.

## Architektur

```
src/Ticketlotse.Domain          Fachmodell und Fachregeln
src/Ticketlotse.Application     Anwendungsfälle, Schnittstellen (Repository, Klassifizierer)
src/Ticketlotse.Infrastructure  Ablage, Anbindung an das Sprachmodell
src/Ticketlotse.Api             ASP.NET Core Minimal API
tests/Ticketlotse.Tests         NUnit
```

**Abhängigkeitsregel: alle Pfeile zeigen nach innen.**

```
Api ──────────┐
              ├──▶ Application ──▶ Domain
Infrastructure┘
```

Die Domain kennt niemanden – kein HTTP, kein JSON, keine Datenbank. Fachregeln stehen
ausschließlich dort; die API übersetzt nur Ausnahmen in HTTP-Statuscodes, und die
Infrastruktur erfüllt Schnittstellen, die die Application vorgibt. Ein Austausch des
Sprachmodells oder des Datenspeichers berührt genau ein Projekt.

## Technik

C# auf .NET 11, ASP.NET Core Minimal API, NUnit. Entstanden nach Test-Driven Development:
zuerst der rote Test, dann die Implementierung.

Der Testlauf ist hermetisch – kein Test braucht Netzwerk, Zugangsdaten oder Guthaben. Das
Sprachmodell wird über ein Fake hinter `ITicketKlassifizierer` ersetzt, HTTP-Aufrufe über
einen eigenen `HttpMessageHandler`.

## Starten und testen

Voraussetzung: .NET SDK 11.

```bash
dotnet test
dotnet run --project src/Ticketlotse.Api
```

Die API läuft auf `http://localhost:5288` bzw. `https://localhost:7258`. In Visual Studio
muss `Ticketlotse.Api` als Startprojekt gesetzt sein.

`src/Ticketlotse.Api/Ticketlotse.Api.http` enthält alle Aufrufe inklusive der Fehlerfälle
und ist im `.http`-Editor von Visual Studio oder VS Code direkt ausführbar.

### Sprachmodell anbinden

Ohne Zugangsschlüssel läuft die Anwendung vollständig – Tickets bleiben dann auf `Neu` zur
manuellen Bearbeitung. Mit Anbindung:

```bash
dotnet user-secrets --project src/Ticketlotse.Api set "Claude:Schluessel" "sk-ant-…"
```

Der Schlüssel gehört in die User Secrets, **nicht** in `appsettings.json`. Modell,
Tokengrenze und Aufwand stehen dort und sind ohne Codeänderung austauschbar:

```json
"Claude": { "Modell": "claude-haiku-4-5", "MaxTokens": 4096, "Aufwand": "low" }
```

## Beispielaufrufe

Beide Beispiele senden dieselbe Anfrage. Unterschiedlich ist nur, ob ein Sprachmodell
angebunden ist.

```http
POST http://localhost:5288/tickets
Content-Type: application/json

{
  "absender": "kunde@example.de",
  "betreff": "Abschlag zu hoch",
  "text": "Mein monatlicher Abschlag ist zu hoch, bitte neu berechnen."
}
```

### 1. Ohne Sprachmodell – der Standardfall nach dem Klonen

Ist kein Zugangsschlüssel konfiguriert, läuft die Anwendung ohne KI: Das Ticket wird
angelegt und gespeichert, die Einordnung bleibt leer.

```http
HTTP/1.1 201 Created
Location: /tickets/8cc107fb-e151-4f58-a737-45dba8191935
```

```json
{
  "id": "8cc107fb-e151-4f58-a737-45dba8191935",
  "absender": "kunde@example.de",
  "betreff": "Abschlag zu hoch",
  "text": "Mein monatlicher Abschlag ist zu hoch, bitte neu berechnen.",
  "eingegangenAm": "2026-11-12T19:48:58.937+00:00",
  "status": "Neu",
  "kategorie": null,
  "dringlichkeit": null,
  "team": null,
  "konfidenz": null,
  "begruendung": null,
  "antwortvorschlag": null
}
```

Status `Neu` heißt: liegt vor, noch nicht eingeordnet. Die Anfrage ist nicht verloren –
sobald ein Schlüssel konfiguriert ist, holt `POST /tickets/{id}/einordnung` die Einordnung
nach. Bis dahin ist `GET /tickets?status=Neu` die Arbeitsliste für die Sachbearbeitung.

### 2. Mit Sprachmodell

```http
HTTP/1.1 201 Created
Location: /tickets/8cc107fb-e151-4f58-a737-45dba8191935
```

```json
{
  "id": "8cc107fb-e151-4f58-a737-45dba8191935",
  "absender": "kunde@example.de",
  "betreff": "Abschlag zu hoch",
  "text": "Mein monatlicher Abschlag ist zu hoch, bitte neu berechnen.",
  "eingegangenAm": "2026-11-12T19:48:58.937+00:00",
  "status": "WartetAufFreigabe",
  "kategorie": "Abschlag",
  "dringlichkeit": "Mittel",
  "team": "Abrechnung",
  "konfidenz": 0.88,
  "begruendung": "Der Kunde beanstandet die Höhe der monatlichen Zahlung.",
  "antwortvorschlag": "Guten Tag, wir prüfen Ihren Abschlag und melden uns …"
}
```

Die Konfidenz liegt über 70 %, also wartet das Ticket auf Freigabe. Das Team ergibt sich
aus der Kategorie und wird nicht von der KI bestimmt. Den vollständigen Verlauf samt
KI-Vorschlag und späteren Korrekturen zeigt `GET /tickets/{id}`.

Weitere Endpunkte:

| Aufruf | Zweck |
|---|---|
| `GET /tickets?status=…` | Liste, optional nach Status gefiltert |
| `GET /tickets/{id}` | Einzelabruf mit vollständigem Verlauf |
| `POST /tickets/{id}/einordnung` | Einordnung nachholen |
| `PUT /tickets/{id}/klassifizierung` | Korrektur durch einen Menschen |
| `PUT /tickets/{id}/freigabe` | Freigabe |

Fehler kommen als ProblemDetails nach RFC 9457.

## Stand

Fertig: Fachmodell mit Verlauf, REST-API, Anbindung eines Sprachmodells hinter
`ITicketKlassifizierer` mit Fake für die Tests.

Bewusst noch offen: Ablage im Arbeitsspeicher statt Datenbank, keine Authentifizierung
(der Bearbeiter einer Korrektur wird im Request mitgegeben und ist damit nicht
fälschungssicher), keine Längengrenzen für Eingaben, kein OpenAPI-Dokument.

## Nächste Etappen

1. **Datenbank** – EF Core mit MS SQL. `ITicketRepository` ist dafür schon asynchron
   geschnitten; dazu optimistische Nebenläufigkeit, damit zwei gleichzeitige Freigaben
   desselben Tickets nicht in ein Rennen laufen. Dazu Beispieldaten, die die interessanten
   Fälle abdecken – eindeutige Kategorie, Grenzfall unter 70 %, Notfall, mehrdeutiger Text.
2. **Docker** – Containerisierung von API und Datenbank für einen reproduzierbaren Start.
3. **KI über Azure, optional Betrieb auf Azure** – Anbindung des Sprachmodells über Azure
   statt direkt beim Anbieter; das betrifft nur `Ticketlotse.Infrastructure`. Dazu optional
   der Betrieb selbst (App Service oder Container Apps, Azure SQL). Zuletzt, wenn das
   Projekt steht und getestet ist – und eine Authentifizierung vorhanden ist.

Später denkbar: Eingabeprüfung und Längengrenzen, strukturiertes Logging als JSON, ein
OpenAPI-Dokument, ein Dashboard für liegengebliebene und eilige Tickets,
Nachrichtenverarbeitung per RabbitMQ, GitHub Actions.

---

Alle Testdaten sind erfunden – im Projekt werden keine echten Kundendaten verwendet.
