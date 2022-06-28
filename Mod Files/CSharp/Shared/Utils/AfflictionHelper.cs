using Barotrauma;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MoreLevelContent.Shared.Utils
{
    internal static class AfflictionHelper
    {
        internal static bool TryGetAffliction(string identifier, out AfflictionPrefab affliction)
        {
            affliction = AfflictionPrefab.List.FirstOrDefault(a => a.Identifier == identifier);
            return affliction != null;
        }
    }
}
