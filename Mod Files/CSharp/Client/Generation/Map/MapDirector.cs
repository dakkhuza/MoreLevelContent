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

namespace MoreLevelContent.Shared.Generation
{
    // Client
    public partial class MapDirector : Singleton<MapDirector>
    {
        private FieldInfo _notificationList;
        private Type _notification;
        private MethodInfo _addMethod;
        private ConstructorInfo _notifConstructor;
        partial void SetupProjSpecific()
        {
            NetUtil.Register(NetEvent.MAP_CONNECTION_EQUALITYCHECK_SENDCLIENT, ConnectionEqualityCheck);
            NetUtil.Register(NetEvent.EVENT_REVEALMAPFEATURE, NotifyRevealMapFeature);
            _notificationList = AccessTools.Field(typeof(Map), "mapNotifications");
            _notification = typeof(Map).GetNestedType("MapNotification", BindingFlags.NonPublic);

            _addMethod = AccessTools.Method(_notificationList.FieldType, "Add");
            _notifConstructor = AccessTools.Constructor(_notification, new Type[] { typeof(string), typeof(GUIFont), _notificationList.FieldType, typeof(Location) });

            var gameClient_EndGame = AccessTools.Method(typeof(GameClient), nameof(GameClient.EndGame));
            _ = Main.Harmony.Patch(gameClient_EndGame, postfix: new HarmonyMethod(AccessTools.Method(typeof(MapDirector), nameof(MapDirector.EndGame))));
        }

        static void EndGame(CampaignMode.TransitionType transitionType)
        {
            if (transitionType == CampaignMode.TransitionType.None)
            {
                Log.Debug("Game ended");
                // Reset connection lookup
                _validatedConnectionLookup = false;
                IdConnectionLookup.Clear();
                ConnectionIdLookup.Clear();
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
