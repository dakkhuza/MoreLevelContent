using Barotrauma;
using MoreLevelContent.Missions;
using MoreLevelContent.Shared.Data;
using System.Collections.Generic;
using System.Xml.Linq;
using System;
using System.Linq;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared.Utils;

namespace MoreLevelContent.Shared.Generation
{
    internal partial class LostCargoMapModule : TimedEventMapModule
    {
        protected override NetEvent EventCreated => NetEvent.MAP_SEND_NEWCARGO;

        //protected override NetEvent EventUpdated => throw new NotImplementedException();

        protected override string NewEventText => "mlc.lostcargonew";

        protected override string EventTag => "lostcargo";

        protected override int MaxActiveEvents => 5;

        protected override int EventSpawnChance => 100;

        protected override int MinDistance => 1;

        protected override int MaxDistance => 2;

        protected override int MinEventDuration => 4;

        protected override int MaxEventDuration => 6;

        protected override bool ShouldSpawnEventAtStart => true;

        protected override void HandleEventCreation(LevelData_MLCData data, int eventDuration)
        {
            data.HasLostCargo = true;
            data.CargoStepsLeft = eventDuration;
        }

        protected override void HandleUpdate(LevelData_MLCData data, LocationConnection connection)
        {
            data.CargoStepsLeft--;
            if (data.CargoStepsLeft <= 0)
            {
                data.HasLostCargo = false;
                string textTag = MLCUtils.GetRandomTag("mlc.lostcargo.tooslow", connection.LevelData);
                SendEventUpdate(textTag, connection);
            }

        }

        protected override bool LevelHasEvent(LevelData_MLCData data) => data.HasLostCargo;
    }
}
