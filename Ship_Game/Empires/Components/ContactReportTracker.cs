using System.Collections.Generic;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;

namespace Ship_Game.Empires.Components;

/// <summary>
/// Player-only tracker. A contact starts when sensors see a combat-capable
/// foreign ship in a system, and closes when that system has had no visible
/// hostiles for <see cref="ContactLingerSeconds"/>.
/// </summary>
[StarDataType]
public sealed class ContactReportTracker
{
    public const float ContactLingerSeconds = 5f;
    public const int MaxStoredReports = 16;

    [StarData] Array<ContactReport> Reports = new();
    [StarData] Array<ActiveContact> Active = new();
    [StarData] int NextReportId = 1;

    [StarDataConstructor]
    public ContactReportTracker() {}

    public ContactReport[] GetReports() => Reports.ToArr();

    public ContactReport GetReport(int id)
    {
        for (int i = 0; i < Reports.Count; i++)
        {
            if (Reports[i].Id == id)
                return Reports[i];
        }
        return null;
    }

    /// <summary>
    /// Record this interval's sensor contacts, then close any that have been
    /// out of vision for <see cref="ContactLingerSeconds"/>. No-ops (and drops
    /// in-progress contacts) when the player has disabled contact reports.
    /// </summary>
    public void ProcessSightings(Empire player, IEnumerable<Ship> seen, FixedSimTime timeStep)
    {
        if (!GlobalStats.EnableContactReports)
        {
            DiscardActive();
            return;
        }

        ObserveSeenShips(player, seen);
        Update(player, timeStep);
    }

    public void DiscardActive()
    {
        Active.Clear();
    }

    /// <summary>
    /// Record ships the player's sensors scanned this interval.
    /// Must not be called while holding ThreatMatrix's Seen lock.
    /// </summary>
    public void ObserveSeenShips(Empire player, IEnumerable<Ship> seen)
    {
        foreach (Ship ship in seen)
        {
            if (!IsContactWorthy(player, ship))
                continue;

            ActiveContact contact = GetOrCreateActive(ship.System);
            contact.ObservedThisTick = true;
            contact.TimeSinceLastSighting = 0f;

            IShipDesign design = ship.ShipData;
            if (design == null)
                continue;

            bool internalsKnown = ship.Loyalty.CanBeScannedByPlayer;
            bool firstSighting = contact.SeenShipIds.Add(ship.Id);

            ContactReportDesign entry = FindDesign(contact.Designs, ship.Loyalty, design.Name);
            if (entry == null)
            {
                entry = new ContactReportDesign
                {
                    DesignName     = design.Name,
                    Loyalty        = ship.Loyalty,
                    Role           = design.Role,
                    IconPath       = design.IconPath,
                    Count          = firstSighting ? 1 : 0,
                    InternalsKnown = internalsKnown
                };
                contact.Designs.Add(entry);
            }
            else
            {
                if (firstSighting)
                    entry.Count++;
                if (internalsKnown)
                    entry.InternalsKnown = true;
            }
        }
    }

    public void Update(Empire player, FixedSimTime timeStep)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            ActiveContact contact = Active[i];
            if (contact.ObservedThisTick)
            {
                contact.ObservedThisTick = false;
                continue;
            }

            contact.TimeSinceLastSighting += timeStep.FixedTime;
            if (contact.TimeSinceLastSighting >= ContactLingerSeconds)
                CloseContact(player, contact, i);
        }
    }

    void CloseContact(Empire player, ActiveContact contact, int activeIndex)
    {
        Active.RemoveAt(activeIndex);
        if (contact.Designs.IsEmpty)
            return;

        var report = new ContactReport
        {
            Id            = NextReportId++,
            System        = contact.System,
            StarDate      = player.Universe.StarDate,
            PrimaryEmpire = PickPrimaryEmpire(contact.Designs),
            Designs       = new Array<ContactReportDesign>(contact.Designs)
        };

        Reports.Add(report);
        while (Reports.Count > MaxStoredReports)
            Reports.RemoveAt(0);

        player.Universe.Notifications?.AddContactReport(report);
    }

    ActiveContact GetOrCreateActive(SolarSystem system)
    {
        for (int i = 0; i < Active.Count; i++)
        {
            if (Active[i].System == system)
                return Active[i];
        }

        var created = new ActiveContact { System = system };
        Active.Add(created);
        return created;
    }

    static ContactReportDesign FindDesign(Array<ContactReportDesign> designs, Empire loyalty, string name)
    {
        for (int i = 0; i < designs.Count; i++)
        {
            ContactReportDesign d = designs[i];
            if (d.Loyalty == loyalty && d.DesignName == name)
                return d;
        }
        return null;
    }

    static Empire PickPrimaryEmpire(Array<ContactReportDesign> designs)
    {
        Empire best = null;
        int bestCount = -1;
        for (int i = 0; i < designs.Count; i++)
        {
            ContactReportDesign d = designs[i];
            if (d.Count > bestCount)
            {
                bestCount = d.Count;
                best = d.Loyalty;
            }
        }
        return best;
    }

    public static bool IsContactWorthy(Empire player, Ship ship)
    {
        if (ship == null || !ship.Active || ship.Loyalty == null)
            return false;
        if (ship.Loyalty == player || player.IsAlliedWith(ship.Loyalty))
            return false;
        if (ship.IsMeteor || ship.IsFreighter || ship.IsConstructor)
            return false;
        if (ship.IsResearchStation || ship.IsMiningStation)
            return false;
        if (ship.IsSupplyShuttle || ship.IsSubspaceProjector)
            return false;
        return ship.BaseStrength > 0 || ship.IsDefaultTroopShip;
    }
}
