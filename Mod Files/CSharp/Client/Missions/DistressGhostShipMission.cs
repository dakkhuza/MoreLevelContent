using Barotrauma;
using Barotrauma.Networking;
using MoreLevelContent.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Missions
{
    partial class DistressGhostshipMission : DistressMission
    {
        private readonly Identifier EXPLORE_SUB = "MLCDISTRESS_GHOSTSHIP_EXPLORE_SUB";
        private readonly Identifier SALVAGE_SUB = "MLCDISTRESS_GHOSTSHIP_SALVAGE";

        public override bool DisplayAsFailed => false;
        public override bool DisplayAsCompleted => State >= 2;
        public override void ClientReadInitial(IReadMessage msg)
        {
            base.ClientReadInitial(msg);
            Log.Debug("message read init");
            missionNPCs.Read(msg);
        }

        public override RichString GetMissionRewardText(Submarine sub) => !SubSalvaged ? base.GetMissionRewardText(sub) : GetBaseMissionRewardText(sub);
        private GhostshipState CurrentState = GhostshipState.WaitForBoardSub;
        private ObjectiveManager.Segment ExploreSegment;
        private List<Hull> HullToExplore = new();
        private List<Hull> ExploredHulls = new();

        private enum GhostshipState
        {
            WaitForBoardSub,
            WaitForExplore,
            WaitForSalvage,
            Salvage
        }

        private bool _salvaged = false;

        partial void UpdateProjSpecific(float deltaTime)
        {
            if (SubSalvaged && !_salvaged)
            {
                ObjectiveManager.CompleteSegment(SALVAGE_SUB);
                _salvaged = true;
                CoroutineManager.StartCoroutine(_showMessageBox(TextManager.Get("missionheader0.distress_ghostship"), TextManager.Get("dgs.inrageforsalvage")));
            }
            IEnumerable<CoroutineStatus> _showMessageBox(LocalizedString header, LocalizedString message)
            {
                while (GUIMessageBox.VisibleBox?.UserData is RoundSummary)
                {
                    yield return new WaitForSeconds(1.0f);
                }
                CreateMessageBox(header, message);
                yield return CoroutineStatus.Success;
            }

            switch (State)
            {
                case 2:
                    if (CurrentState == GhostshipState.WaitForSalvage)
                    {
                        return;
                    }
                    if (CurrentState == GhostshipState.WaitForExplore)
                    {
                        foreach (var crewMember in GameSession.GetSessionCrewCharacters(CharacterType.Player))
                        {
                            if (HullToExplore.Contains(crewMember.CurrentHull))
                            {
                                if (!ExploredHulls.Contains(crewMember.CurrentHull))
                                {
                                    ExploredHulls.Add(crewMember.CurrentHull);
                                }
                            }
                        }
                        if (ExploredHulls.Count >= HullToExplore.Count / 2)
                        {
                            CurrentState = GhostshipState.WaitForSalvage;
                            ObjectiveManager.CompleteSegment(EXPLORE_SUB);
                            ObjectiveManager.TriggerSegment(ObjectiveManager.Segment.CreateObjectiveSegment(SALVAGE_SUB, "dgs.obj.optionalsalvage"));
                        }

                        return;
                    }
                    HullToExplore = ghostship.GetHulls(false).Where(h => h.AvoidStaying == false).ToList();
                    CurrentState = GhostshipState.WaitForExplore;
                    ExploreSegment = ObjectiveManager.Segment.CreateObjectiveSegment(EXPLORE_SUB, "dgs.obj.exploreship");
                    ExploreSegment.CanBeCompleted = true;
                    ObjectiveManager.TriggerSegment(ExploreSegment);
                    Log.Debug("Triggered state");
                    break;
            }
        }
    }
}
