using Barotrauma;
using System.Runtime.CompilerServices;
using System;
using System.Xml.Linq;

namespace MoreLevelContent.Shared.Data
{
    class Character_MLCData
    {
        public XElement NPCElement;
        public bool IsDistressShuttle;
        public bool IsDistressDiver;
    }

    public static partial class MLCData
    {
        private static readonly ConditionalWeakTable<Character, Character_MLCData> character_data = new();
        
        internal static Character_MLCData MLC(this Character characterData) => character_data.GetOrCreateValue(characterData);
        
        internal static void AddData(this Character characterData, Character_MLCData additional)
        {
            try
            {
                character_data.Add(characterData, additional);
            }
            catch (Exception e) { Log.Error(e.ToString()); }
        }
    }
}
