using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Barotrauma.Steam;
using MoreLevelContent.Shared;
using System;
using System.Linq;

public class CompatabilityHelper : Singleton<CompatabilityHelper>
{
    public bool HazardousReactorsInstalled { get; private set; }
    public bool NeurotraumaInstalled { get; private set; }

    internal static void SetupHazReactor(Reactor reactor)
    {
        Item reactorItem = reactor.Item;
        reactorItem.InvulnerableToDamage = true;

        // tell bots not to stay in the hull the reactor is in
        if (reactorItem.CurrentHull != null) reactorItem.CurrentHull.AvoidStaying = true;
    }

    public override void Setup()
    {
        HazardousReactorsInstalled = CheckInstalled(2547888957);
        NeurotraumaInstalled = CheckInstalled(2776270649);

        Log.Debug(
            $"-= MLC Compatability =-\n" +
            $"- HazReactor: {HazardousReactorsInstalled}\n" +
            $"- Neurotrauma: {NeurotraumaInstalled}");
    }

    bool CheckInstalled(UInt64 workshopID) => ContentPackageManager.EnabledPackages.All.Any(p => p.TryExtractSteamWorkshopId(out SteamWorkshopId idOut) && idOut.Value == workshopID);
}
