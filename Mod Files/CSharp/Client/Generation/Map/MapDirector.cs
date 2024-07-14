using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Networking;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Barotrauma;
using Barotrauma.Networking;
using MoreLevelContent.Shared.Data;
using System.Reflection.Emit;
using System.Xml.Linq;
using static Barotrauma.Networking.MessageFragment;

namespace MoreLevelContent.Shared.Generation
{
    // Client
    public partial class MapDirector : Singleton<MapDirector>
    {
        private FieldInfo _notificationList;
        private Type _notification;
        private MethodInfo _addMethod;
        private ConstructorInfo _notifConstructor;
        private MapSyncState SyncState = MapSyncState.Syncing;
        partial void SetupProjSpecific()
        {
            NetUtil.Register(NetEvent.MAP_CONNECTION_EQUALITYCHECK_SENDCLIENT, ConnectionEqualityCheck);
            NetUtil.Register(NetEvent.EVENT_REVEALMAPFEATURE, NotifyRevealMapFeature);
            _notificationList = AccessTools.Field(typeof(Map), "mapNotifications");
            _notification = typeof(Map).GetNestedType("MapNotification", BindingFlags.NonPublic);

            _addMethod = AccessTools.Method(_notificationList.FieldType, "Add");
            _notifConstructor = AccessTools.Constructor(_notification, new Type[] { typeof(string), typeof(GUIFont), _notificationList.FieldType, typeof(Location) });

            NetUtil.Register(NetEvent.MAP_SEND_STATE, ReceiveMapState);
        }

        private void ReceiveMapState(object[] args)
        {
            Log.Debug("Got map state packet");
            IReadMessage inMsg = (IReadMessage)args[0];
            MapSyncState mapState = (MapSyncState)inMsg.ReadByte();
            if (mapState != MapSyncState.MapSynced)
            {
                Log.Debug($"Map did not sync, state: {mapState}");
                SyncState = mapState;
                return;
            }

            byte activeBeacons = inMsg.ReadByte();
            for (int i = 0; i < activeBeacons; i++)
            {
                int connectionID = inMsg.ReadUInt16();
                int stepsLeft = inMsg.ReadByte();
                if (!IdConnectionLookup.TryGetValue(connectionID, out var connection))
                {
                    DebugConsole.ThrowError($"More Level Content tried to add an active distress beacon on connection with ID '{connectionID}' but found no connection on the clients id connection lookup! Do we have connections in the lookup? {IdConnectionLookup.Count > 0}s");
                    continue;
                }
                connection.LevelData.MLC().HasDistress = true;
                connection.LevelData.MLC().DistressStepsLeft = stepsLeft;
            }
            Log.Debug("Synced map state!");
        }

        internal partial void RoundEnd(CampaignMode.TransitionType transitionType)
        {
            if (transitionType == CampaignMode.TransitionType.None)
            {
                Log.Debug($"Cleaned connection lookup");
                // Reset connection lookup
                if (_validatedConnectionLookup)
                {
                    _validatedConnectionLookup = false;
                    IdConnectionLookup.Clear();
                    ConnectionIdLookup.Clear();
                }
            }
        }

        public void AddNewsStory(string message)
        {
            object list = _notificationList.GetValue(GameMain.GameSession.Map);

            var notification = _notifConstructor.Invoke(new object[] {
                message,
                GUIStyle.SubHeadingFont,
                list,
                null
            });

            _ = _addMethod.Invoke(list, new object[] { notification });

            _notificationList.SetValue(GameMain.GameSession.Map, list);
        }

        void NotifyRevealMapFeature(object[] args)
        {
            IReadMessage inMsg = (IReadMessage)args[0];
            Identifier featureName = inMsg.ReadIdentifier();
            Int32 conId = inMsg.ReadInt32();
            LocationConnection con = IdConnectionLookup[conId];
            MapFeatureModule.TryGetFeature(featureName, out MapFeature feature);
            RevealMapFeatureAction.ShowNotification(feature, con);
        }
    }
}
