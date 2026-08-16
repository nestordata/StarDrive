using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SDGraphics;
using SDUtils;
using Ship_Game;
using Ship_Game.Empires.Components;
using Ship_Game.Ships;

namespace UnitTests.Empires;

[TestClass]
public class ContactReportTests : StarDriveTest
{
    Planet PlayerPlanet;
    ContactReportTracker Tracker => Player.ContactReports;

    public ContactReportTests()
    {
        LoadStarterShips("Vulcan Scout", "Unarmed Scout");
        CreateUniverseAndPlayerEmpire();
        PlayerPlanet = AddDummyPlanetToEmpire(new Vector2(200_000, 200_000), Player);
        PlayerPlanet.System.SetExploredBy(Player);
        Enemy.SetCanBeScannedByPlayer(false);
        GlobalStats.EnableContactReports = true;
    }

    Ship SpawnEnemyInSystem(string design, Vector2 offset)
    {
        Ship ship = SpawnShip(design, Enemy, PlayerPlanet.Position + offset);
        ship.SetSystem(PlayerPlanet.System);
        PlayerPlanet.System.ShipList.Add(ship);
        return ship;
    }

    void Observe(params Ship[] ships)
    {
        Tracker.ObserveSeenShips(Player, ships);
    }

    void Tick(float seconds = 1f)
    {
        Tracker.Update(Player, new FixedSimTime(seconds));
    }

    // Production: Observe then Update on the same interval only consumes ObservedThisTick.
    // Linger starts on the next empty interval.
    void CloseContact()
    {
        Tick();
        Tick(ContactReportTracker.ContactLingerSeconds);
    }

    [TestMethod]
    public void NoReportUntilSightingEnds()
    {
        Ship enemy = SpawnEnemyInSystem("Vulcan Scout", new Vector2(1000, 0));
        Observe(enemy);
        AssertEqual(0, Tracker.GetReports().Length, "Contact still open while ships are visible");
        Tick();
        Observe(enemy);
        AssertEqual(0, Tracker.GetReports().Length);
    }

    [TestMethod]
    public void ReportFiresAfterLingerWithoutVisibleHostiles()
    {
        Ship enemy = SpawnEnemyInSystem("Vulcan Scout", new Vector2(1000, 0));
        Observe(enemy);
        CloseContact();

        ContactReport[] reports = Tracker.GetReports();
        AssertEqual(1, reports.Length);
        AssertEqual(PlayerPlanet.System, reports[0].System);
        AssertEqual(1, reports[0].TotalDesigns);
        AssertEqual(1, reports[0].TotalShips);
        AssertEqual("Vulcan Scout", reports[0].Designs[0].DesignName);
        Assert.AreSame(Enemy, reports[0].Designs[0].Loyalty);
        Assert.IsFalse(reports[0].Designs[0].InternalsKnown, "No scan unlock: hull only");
    }

    [TestMethod]
    public void LingerResetsIfTheyComeBackIntoView()
    {
        Ship enemy = SpawnEnemyInSystem("Vulcan Scout", new Vector2(1000, 0));
        Observe(enemy);
        Tick();
        Tick(ContactReportTracker.ContactLingerSeconds - 1f);
        Observe(enemy);
        Tick();
        Tick(ContactReportTracker.ContactLingerSeconds - 1f);
        AssertEqual(0, Tracker.GetReports().Length, "Re-sighting must keep the same contact open");
        Tick(1f);
        AssertEqual(1, Tracker.GetReports().Length);
    }

    [TestMethod]
    public void CountsUniqueShipsNotScanTicks()
    {
        Ship a = SpawnEnemyInSystem("Vulcan Scout", new Vector2(1000, 0));
        Ship b = SpawnEnemyInSystem("Vulcan Scout", new Vector2(2000, 0));
        Observe(a, b);
        Observe(a, b);
        CloseContact();

        ContactReport report = Tracker.GetReports()[0];
        AssertEqual(1, report.TotalDesigns);
        AssertEqual(2, report.TotalShips);
    }

    [TestMethod]
    public void IgnoresPlayerShipsAndFiltersNonCombat()
    {
        Ship ours = SpawnShip("Vulcan Scout", Player, PlayerPlanet.Position);
        ours.SetSystem(PlayerPlanet.System);
        Ship unarmed = SpawnEnemyInSystem("Unarmed Scout", new Vector2(1500, 0));
        Ship combat = SpawnEnemyInSystem("Vulcan Scout", new Vector2(2500, 0));

        Assert.IsFalse(ContactReportTracker.IsContactWorthy(Player, ours));
        Assert.IsFalse(ContactReportTracker.IsContactWorthy(Player, null));
        Assert.IsFalse(ContactReportTracker.IsContactWorthy(Player, unarmed),
            "Unarmed Scout has no combat strength and must not appear in a contact report");

        Observe(ours, unarmed, combat);
        CloseContact();

        ContactReport report = Tracker.GetReports()[0];
        AssertEqual(1, report.TotalShips);
        AssertEqual("Vulcan Scout", report.Designs[0].DesignName);
        Assert.AreSame(Enemy, report.Designs[0].Loyalty);
    }

    [TestMethod]
    public void InternalsKnownOnlyWhenEmpireCanBeScanned()
    {
        Enemy.SetCanBeScannedByPlayer(true);
        Ship enemy = SpawnEnemyInSystem("Vulcan Scout", new Vector2(1000, 0));
        Observe(enemy);
        CloseContact();

        Assert.IsTrue(Tracker.GetReports()[0].Designs[0].InternalsKnown);
    }

    [TestMethod]
    public void ScanUnlockMidContactUpgradesTheEntry()
    {
        Ship enemy = SpawnEnemyInSystem("Vulcan Scout", new Vector2(1000, 0));
        Observe(enemy);
        AssertEqual(0, Tracker.GetReports().Length);
        Enemy.SetCanBeScannedByPlayer(true);
        Observe(enemy);
        CloseContact();

        Assert.IsTrue(Tracker.GetReports()[0].Designs[0].InternalsKnown,
            "If you could have paused and scanned later in the same contact, the report should include internals");
    }

    [TestMethod]
    public void SeparateSystemsAreSeparateReports()
    {
        Planet other = AddDummyPlanetToEmpire(new Vector2(-200_000, -200_000), Player);
        Ship inHome = SpawnEnemyInSystem("Vulcan Scout", new Vector2(1000, 0));
        Ship inOther = SpawnShip("Vulcan Scout", Enemy, other.Position + new Vector2(1000, 0));
        inOther.SetSystem(other.System);
        other.System.ShipList.Add(inOther);

        Observe(inHome, inOther);
        CloseContact();

        ContactReport[] reports = Tracker.GetReports();
        AssertEqual(2, reports.Length);
        Assert.IsTrue(reports.Any(r => r.System == PlayerPlanet.System));
        Assert.IsTrue(reports.Any(r => r.System == other.System));
    }

    [TestMethod]
    public void SettingOffDropsActiveContactsAndFiresNothing()
    {
        Ship enemy = SpawnEnemyInSystem("Vulcan Scout", new Vector2(1000, 0));
        Observe(enemy);

        GlobalStats.EnableContactReports = false;
        try
        {
            Tracker.ProcessSightings(Player, Empty<Ship>.Array, new FixedSimTime(1f));
            CloseContact();
            AssertEqual(0, Tracker.GetReports().Length);
        }
        finally
        {
            GlobalStats.EnableContactReports = true;
        }
    }
}
