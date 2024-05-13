using Barotrauma;
using MoreLevelContent.Shared.Generation.Interfaces;
using MoreLevelContent.Shared.Generation.Pirate;
using System.Collections.Generic;

namespace MoreLevelContent.Shared.Generation
{
    public class LevelContentProducer : IActive
    {
        /// <summary>
        /// If the the generator has outposts to spawn or not
        /// </summary>
        public bool Active { get; private set; }

        public LevelContentProducer()
        {
            Log.Verbose("LevelContentProducer::ctr..");
            AddDirector(PirateOutpostDirector.Instance);
            AddDirector(MissionGenerationDirector.Instance);
            AddDirector(CaveGenerationDirector.Instance);
            // AddDirector(PirateEncounterDirector.Instance);
        }

        private readonly List<IGenerateSubmarine> submarineGenerators = new List<IGenerateSubmarine>();
        private readonly List<IGenerateNPCs> npcGenerators = new List<IGenerateNPCs>();
        private readonly List<ILevelStartGenerate> levelStartGenerators = new List<ILevelStartGenerate>();
        private readonly List<IRoundStatus> roundStart = new List<IRoundStatus>();


        public void Cleanup() => Log.Verbose("LevelContentProducer::Cleanup");

        public void AddDirector<Director>(GenerationDirector<Director> director) where Director : class
        {
            director.Setup();
            if (!director.Active)
            {
                Log.Error($"Did not add director {director} as it was not active!");
            }

            Active = true;

            if (director is IGenerateSubmarine)
            {
                submarineGenerators.Add(director as IGenerateSubmarine);
                Log.Verbose($"Added {director} to Submarine generators");
            }

            if (director is IGenerateNPCs)
            {
                npcGenerators.Add(director as IGenerateNPCs);
                Log.Verbose($"Added {director} to NPC generators");
            }

            if (director is ILevelStartGenerate)
            {
                levelStartGenerators.Add(director as ILevelStartGenerate);
                Log.Verbose($"Added {director} to level start generators");
            }

            if (director is IRoundStatus)
            {
                roundStart.Add(director as IRoundStatus);
                Log.Verbose($"Added {director} to round start generators");
            }
        }

        internal void LevelGenerate(LevelData levelData, bool mirror)
        {
            Log.Verbose("Called level generate");
            foreach (ILevelStartGenerate levelStart in levelStartGenerators)
            {
                levelStart.OnLevelGenerationStart(levelData, mirror);
            }
        }

        public void StartRound()
        {
            Log.Verbose("Called start round");
            foreach (IRoundStatus generator in roundStart)
            {
                generator.BeforeRoundStart();
            }
        }

        public void EndRound()
        {
            Log.Verbose("Called end round");
            foreach (IRoundStatus generator in roundStart)
            {
                generator.RoundEnd();
            }

        }

        public void CreateWrecks()
        {
            Log.Verbose("Called create wrecks");
            foreach (IGenerateSubmarine generateSubmarine in submarineGenerators)
            {
                generateSubmarine.GenerateSub();
            }
        }

        public void SpawnNPCs()
        {
            Log.Verbose("Called spawn NPCS");
            foreach (IGenerateNPCs generateNPCs in npcGenerators)
            {
                generateNPCs.SpawnNPCs();
            }
        }
    }
}
