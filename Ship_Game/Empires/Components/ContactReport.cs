using System.Collections.Generic;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;

namespace Ship_Game.Empires.Components;

/// <summary>
/// One enemy design the player actually had sensor contact with.
/// InternalsKnown is true only if that empire could be scanned at the time of sighting.
/// </summary>
[StarDataType]
public sealed class ContactReportDesign
{
    [StarData] public string DesignName;
    [StarData] public Empire Loyalty;
    [StarData] public RoleName Role;
    [StarData] public string IconPath;
    [StarData] public int Count;
    [StarData] public bool InternalsKnown;

    [StarDataConstructor]
    public ContactReportDesign() {}

    public IShipDesign ResolveDesign()
    {
        return DesignName.NotEmpty() && ResourceManager.Ships.GetDesign(DesignName, out IShipDesign design)
            ? design
            : null;
    }
}

/// <summary>
/// A completed sensor-contact in one system (or deep space).
/// Only contains designs the player could have paused and clicked.
/// </summary>
[StarDataType]
public sealed class ContactReport
{
    [StarData] public int Id;
    [StarData] public SolarSystem System;
    [StarData] public float StarDate;
    [StarData] public Empire PrimaryEmpire;
    [StarData] public Array<ContactReportDesign> Designs = new();

    [StarDataConstructor]
    public ContactReport() {}

    public string LocationName => System?.Name ?? Localizer.Token(GameText.ContactReportDeepSpace);
    public int TotalShips => Designs.Sum(d => d.Count);
    public int TotalDesigns => Designs.Count;
}

/// <summary>
/// In-progress contact for one system while the player still has eyes on hostiles.
/// </summary>
[StarDataType]
public sealed class ActiveContact
{
    [StarData] public SolarSystem System;
    [StarData] public float TimeSinceLastSighting;
    [StarData] public HashSet<int> SeenShipIds = new();
    [StarData] public Array<ContactReportDesign> Designs = new();

    public bool ObservedThisTick;

    [StarDataConstructor]
    public ActiveContact() {}
}
