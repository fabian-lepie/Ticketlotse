using NUnit.Framework;
using Ticketlotse.Domain;

namespace Ticketlotse.Tests;

public class SicherheitTests
{
    [TestCase(-0.1)]
    [TestCase(1.1)]
    public void Sicherheit_muss_zwischen_0_und_1_liegen(double wert)
    {
        Assert.That(() => new Sicherheit(wert), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [TestCase(0.70, true)]
    [TestCase(0.69, false)]
    public void Ab_70_Prozent_ist_die_Sicherheit_ausreichend(double wert, bool erwartet)
    {
        Assert.That(new Sicherheit(wert).IstAusreichend, Is.EqualTo(erwartet));
    }
}