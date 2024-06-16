using MoreLevelContent.Shared.Generation.Interfaces;
using Barotrauma;
using System.Linq;

namespace MoreLevelContent.Shared.Generation.Pirate
{
    public class PirateEncounterDirector : GenerationDirector<PirateEncounterDirector>
    {
        public override bool Active => false;
        private MissionPrefab pirateMission;

        public override void Setup()
        {
            pirateMission = MissionPrefab.Prefabs.Find(m => m.Type == MissionType.Pirate);
            if (pirateMission == null)
            {
                Log.Error("Couldn't find a pirate mission!");
            }
        }

        //public void BeforeRoundStart()
        //{
        //    Location[] locations = { GameMain.GameSession.StartLocation, GameMain.GameSession.EndLocation };
        //    var mission = pirateMission.Instantiate(locations, Submarine.MainSub);
        //    var missionList = GameMain.GameSession.GameMode.Missions.ToList();
        //}
        //
        //public void RoundEnd() { }
    }
}
