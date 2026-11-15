# Ticketlotse

KI-gestützte Einordnung von Kundenservice-Tickets eines Stadtwerks.
Demo-/Bewerbungsprojekt von Fabian Lepie – sauberer, gut erklärbarer Code ist wichtiger als Features.

## Technik
- C# auf .NET 11 (SDK 11.0.100), NUnit für Tests
- Später: ASP.NET Core Minimal API, EF Core mit MS SQL, LLM-Anbindung per HttpClient (REST/JSON)
- Struktur: src/Ticketlotse.Domain, src/Ticketlotse.Application, src/Ticketlotse.Infrastructure, src/Ticketlotse.Api, tests/Ticketlotse.Tests

## Arbeitsweise
- Domain-Driven Design: Fachregeln gehören ins Domain-Projekt, nicht in API oder Prompt
- Test-Driven Development: immer zuerst Tests schreiben (rot), dann Implementierung (grün), dann aufräumen
- Fachbegriffe und Bezeichner auf Deutsch (Ticket, Kategorie, Dringlichkeit, Zustaendigkeit), Code-Kommentare kurz
- Nur ausgedachte Testdaten, niemals echte Kundendaten
- Nach jedem Schritt `dotnet test` ausführen; Änderungen kurz erklären, damit ich sie im Vorstellungsgespräch verstehe

## Fachmodell
- Kategorien: Zaehlerstand, Abschlag, Rechnung, Umzug, Lieferantenwechsel, TarifVertrag, Stoerung, Sonstiges
- Dringlichkeit: Niedrig, Mittel, Hoch
- Teams: Messwesen, Abrechnung, Kundenservice, Vertrieb, Netzbetrieb (Zuordnung aus der Kategorie)
- Status: Neu → WartetAufFreigabe (Konfidenz ≥ 70 %) oder ManuellePruefung (< 70 %) → Freigegeben
- Sicherheitsregel: Notfallbegriffe (Gasgeruch, Stromausfall, kein Strom, kein Wasser, Wasserrohrbruch) → Dringlichkeit immer Hoch, egal was KI oder Mensch vorschlägt
- Ein Mensch gibt jeden KI-Vorschlag frei
- Einordnung: KI-Vorschlag und menschliche Korrekturen werden als Verlauf geführt; die letzte Einordnung gilt
- Korrektur durch einen Menschen setzt den Status zurück auf WartetAufFreigabe (ggf. anderes Team)

## Etappen
1. Fachmodell + Tests (Domain)
2. REST-API (POST /tickets, GET /tickets/{id}, GET /tickets?status=…, PUT /tickets/{id}/freigabe)
3. KI-Anbindung hinter Interface ITicketKlassifizierer (Fake für Tests)
4. Datenbank (EF Core, MS SQL), Beispieldaten; aufgezeichnete KI-Antworten als Grundlage für eine kostenlose Probedemo – README ist vorgezogen und liegt vor
5. Optional: Docker, RabbitMQ, GitHub Actions, Eingabeprüfung und Sicherheit, strukturiertes Logging (JSON), OpenAPI, Dashboard
6. KI-Anbindung über Azure; optional Betrieb auf Azure (App Service oder Container Apps, Azure SQL) – erst wenn das Projekt steht und getestet ist