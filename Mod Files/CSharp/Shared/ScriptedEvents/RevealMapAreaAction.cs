
using Barotrauma;
using Microsoft.Xna.Framework;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.XML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MoreLevelContent
{
    /// <summary>
    /// Remove the fog of war from a nearby area
    /// </summary>
    [InjectScriptedEvent]
    internal class RevealMapAreaAction : EventAction
    {
        private bool isFinished = false;
        private readonly Random random;

        public RevealMapAreaAction(ScriptedEvent parentEvent, ContentXElement element) : base(parentEvent, element)
        {
            //the action chooses the same mission if
            // 1. event seed is the same (based on level seed, changes when events are completed)
            // 2. event is the same (two different events shouldn't choose the same mission)
            // 3. the MissionAction is the same (two different actions in the same event shouldn't choose the same mission)
            // Taken from MissionAction
            random = new MTRandom(
                parentEvent.RandomSeed +
                ToolBox.StringToInt(ParentEvent.Prefab.Identifier.Value) +
                ParentEvent.Actions.Count);
        }

        public override void Update(float deltaTime)
        {
            isFinished = true;
            if (GameMain.GameSession.GameMode is CampaignMode campaign)
            {
                Location loc = MLCUtils.FindUnlockLocation(new MLCUtils.FindLocationInfo()
                {
                    MinDistance = 3,
                    MustBeFurtherOnMap = true,
                    MustBeHidden = true
                });

                if (loc == null)
                {
                    Log.Error("Failed to find a location to unlock");
                    return;
                }

                campaign.Map.Discover(loc, false);

                // Probably have to do some syncing here, maybe not
                if (campaign is MultiPlayerCampaign mpCampaign)
                {
                    mpCampaign.IncrementLastUpdateIdForFlag(MultiPlayerCampaign.NetFlags.MapAndMissions);
                }

#if CLIENT
                ShowNotification(loc);
#endif

            }
        }

#if CLIENT
        public static void ShowNotification(Location loc)
        {
            if (GameMain.GameSession.GameMode is not CampaignMode campaign) return;
            _ = new GUIMessageBox(TextManager.Get("mapupdate.generic.header"), TextManager.GetWithVariable("mapupdate.revealed.location", "[location1]", loc.DisplayName),
                Array.Empty<LocalizedString>(), type: GUIMessageBox.Type.InGame, iconStyle: "", relativeSize: new Vector2(0.2f, 0.06f), minSize: new Point(64, 74));
        }
#endif



        public override bool IsFinished(ref string goToLabel) => isFinished;
        public override void Reset() => isFinished = false;
        public override string ToDebugString()
        {
            return $"{ToolBox.GetDebugSymbol(isFinished)} {nameof(RevealMapFeatureAction)}";
        }

    }
}
