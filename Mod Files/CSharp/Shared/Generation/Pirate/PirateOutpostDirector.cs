using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Generation.Interfaces;
using MoreLevelContent.Shared.Store;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MoreLevelContent.Shared.Generation.Pirate
{
    public class PirateOutpostDirector : GenerationDirector<PirateOutpostDirector>, IGenerateSubmarine, IGenerateNPCs, ILevelStartGenerate
    {
        public bool ForceSpawn { get; set; } = false;
        public bool ForceHusk { get; set; } = false;


        public float BaseHuskChance = 0.1f;
        public float SpawnChanceNoise = 10.0f;
        public float DifficultyNoise = 10.0f;
        public float AddedMissionDifficultyPerPlayer = 0;

        private PirateSpawnData levelSpawnData;
        private PirateOutpost enemyOutpost;

        public override bool Active => PirateStore.HasContent;

        public override void Setup() => PirateStore.Instance.Setup();

        void ILevelStartGenerate.OnLevelGenerationStart(LevelData levelData, bool _)
        {
            enemyOutpost = null;
            levelSpawnData = GetSpawnData(levelData);

            if (levelSpawnData.WillSpawn) enemyOutpost = new PirateOutpost(levelSpawnData);
        }

        public void GenerateSub() => enemyOutpost?.Generate();
        public void SpawnNPCs() => enemyOutpost?.Populate();

        private PirateSpawnData GetSpawnData(LevelData levelData)
        {
            Log.InternalDebug("Rolling for a priate spawn...");
            Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
            PirateSpawnData spawnData = new PirateSpawnData(rand, levelData.Difficulty);
            Log.InternalDebug(spawnData.ToString());
            return spawnData;
        }
    }

    public class PirateSpawnData
    {
        public PirateSpawnData(Random rand, float levelDiff)
        {
            UpdatePirateSpawnData(levelDiff, rand);

            int spawnInt = rand.Next(100);
            int huskInt = rand.Next(100);

            WillSpawn = PirateOutpostDirector.Instance.ForceSpawn ? PirateOutpostDirector.Instance.ForceSpawn : modifiedSpawnChance > spawnInt;
            Husked = modifiedHuskChance > huskInt;

            Log.InternalDebug($"spawn int {spawnInt}, husk int {huskInt}");
        }

        public bool WillSpawn { get; set; }
        public bool Husked { get; set; }
        public float PirateDifficulty { get; private set; }

        public override string ToString() => $"Will Spawn: {WillSpawn}, Is Husked: {Husked}";

        private float modifiedSpawnChance;
        private float modifiedHuskChance;

        private void UpdatePirateSpawnData(float levelDiff, Random rand)
        {
            float baseChance = levelDiff < 100 ? MathF.Min(levelDiff / 2, (levelDiff / 5) + 15) : 100f;
            float spawnOffset = MathHelper.Lerp(-PirateOutpostDirector.Instance.SpawnChanceNoise, PirateOutpostDirector.Instance.SpawnChanceNoise, (float)rand.NextDouble());

            modifiedSpawnChance = baseChance + spawnOffset;
            Log.Debug($"Modified pirate spawn chance for diff {levelDiff} is {modifiedSpawnChance}, base chance {baseChance}, offset {spawnOffset}");

            float diffOffset = Math.Abs(MathHelper.Lerp(-PirateOutpostDirector.Instance.DifficultyNoise, PirateOutpostDirector.Instance.DifficultyNoise, (float)rand.NextDouble()));
            PirateDifficulty = levelDiff + diffOffset;
            Log.Debug($"Modified pirate diff is {PirateDifficulty}, level diff {levelDiff}, offset {diffOffset}");

            modifiedHuskChance = MathF.Max(PirateOutpostDirector.Instance.BaseHuskChance, levelDiff / 10);
            Log.Debug($"Modified chance for pirates to be husked is {modifiedHuskChance}");
        }

    }

}
