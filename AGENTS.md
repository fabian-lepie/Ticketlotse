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
- Status: Neu → WartetAufFreigabe (Sicherheit ≥ 70 %) oder ManuellePruefung (< 70 %) → Freigegeben
- Sicherheitsregel: Notfallbegriffe (Gasgeruch, Stromausfall, kein Strom, kein Wasser, Wasserrohrbruch) → Dringlichkeit immer Hoch, egal was die KI vorschlägt
- Ein Mensch gibt jeden KI-Vorschlag frei

## Etappen
1. Fachmodell + Tests (Domain)
2. REST-API (POST /tickets, GET /tickets/{id}, GET /tickets?status=…, PUT /tickets/{id}/freigabe)
3. KI-Anbindung hinter Interface ITicketKlassifizierer (Fake für Tests)
4. Datenbank, README, Beispieldaten
5. Optional: RabbitMQ, Docker, GitHub Actions