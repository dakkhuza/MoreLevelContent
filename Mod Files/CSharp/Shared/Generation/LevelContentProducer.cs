using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Generation.Interfaces;
using MoreLevelContent.Shared.Generation.Pirate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

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
        }

        private readonly List<IGenerateSubmarine> submarineGenerators = new List<IGenerateSubmarine>();
        private readonly List<IGenerateNPCs> npcGenerators = new List<IGenerateNPCs>();
        private readonly List<ILevelStartGenerate> levelStartGenerators = new List<ILevelStartGenerate>();


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
        }

        internal void LevelGenerate(LevelData levelData, bool mirror)
        {
            foreach (ILevelStartGenerate levelStart in levelStartGenerators)
            {
                levelStart.OnLevelGenerationStart(levelData, mirror);
            }
        }

        public void CreateWrecks()
        {
            foreach (IGenerateSubmarine generateSubmarine in submarineGenerators)
            {
                generateSubmarine.GenerateSub();
            }
        }

        public void SpawnNPCs()
        {
            foreach (IGenerateNPCs generateNPCs in npcGenerators)
            {
                generateNPCs.SpawnNPCs();
            }
        }
    }
}
