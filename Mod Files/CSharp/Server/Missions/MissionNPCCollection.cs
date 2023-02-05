using Barotrauma.Networking;
using Barotrauma;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoreLevelContent.Shared.Data;

namespace MoreLevelContent.Missions
{
    // Server
    partial class MissionNPCCollection
    {
        internal void Write(IWriteMessage msg)
        {
            msg.WriteBoolean(characters.Count > 0);
            if (characters.Count == 0) return;

            msg.WriteByte((byte)characters.Count);
            foreach (Character character in characters)
            {
                character.WriteSpawnData(msg, character.ID, restrictMessageSize: false);
                msg.WriteBoolean(character.MLC().NPCElement.GetAttributeBool("allowordering", false));
                msg.WriteUInt16((ushort)characterItems[character].Count());
                foreach (Item item in characterItems[character])
                {
                    item.WriteSpawnData(msg, item.ID, item.ParentInventory?.Owner?.ID ?? Entity.NullEntityID, 0, item.ParentInventory?.FindIndex(item) ?? -1);
                }
            }
        }
    }
}
