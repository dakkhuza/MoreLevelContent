using Barotrauma;
using MoreLevelContent.Missions;
using MoreLevelContent.Shared.Data;
using System.Collections.Generic;
using System.Xml.Linq;
using System;
using System.Linq;
using MoreLevelContent.Shared.Generation.Pirate;

namespace MoreLevelContent.Shared.Generation
{
    internal partial class PirateOutpostMapModule : MapModule
    {
        protected override void InitProjSpecific() { }

        public override void OnLevelDataGenerate(LevelData __instance, LocationConnection locationConnection) => SetPirateData(locationConnection.LevelData, locationConnection.LevelData.MLC(), locationConnection);

        public override void OnMapLoad(Map __instance)
        {
            // Map has no pirate outposts, lets generate some
            if (!__instance.Connections.Any(c => c.LevelData.MLC().PirateData.HasPirateOutpost))
            {
                Log.Debug("Map has no pirate outposts, adding some...");
                for (int i = 0; i < __instance.Connections.Count; i++)
                {
                    var connection = __instance.Connections[i];
                    SetPirateData(connection.LevelData, connection.LevelData.MLC(), connection);
                }
            }
        }

        void SetPirateData(LevelData levelData, LevelData_MLCData additionalData, LocationConnection locationConnection)
        {
            Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
            PirateSpawnData spawnData = new PirateSpawnData(rand, levelData.Difficulty);
            additionalData.PirateData = new PirateData(spawnData);
        }
    }
}
