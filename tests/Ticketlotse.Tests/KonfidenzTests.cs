using NUnit.Framework;
using Ticketlotse.Domain;

namespace Ticketlotse.Tests;

public class KonfidenzTests
{
    [TestCase(-0.1)]
    [TestCase(1.1)]
    public void Konfidenz_muss_zwischen_0_und_1_liegen(double wert)
    {
        Assert.That(() => new Konfidenz(wert), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [TestCase(0.70, true)]
    [TestCase(0.69, false)]
    public void Ab_70_Prozent_ist_die_Konfidenz_ausreichend(double wert, bool erwartet)
    {
        Assert.That(new Konfidenz(wert).IstAusreichend, Is.EqualTo(erwartet));
    }
}
