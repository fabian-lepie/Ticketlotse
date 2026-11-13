using System;
using System.Collections.Generic;
using System.Text;
using Ticketlotse.Application;
using Ticketlotse.Domain;

namespace Ticketlotse.Infrastructure
{
    public sealed class KeineKiAngebunden : ITicketKlassifizierer
    {
        public Task<KiVorschlag> EinordnenAsync(Ticket ticket, CancellationToken abbruch = default) =>
            throw new KlassifizierungNichtMoeglich("Es ist keine KI angebunden.");
    }
}
