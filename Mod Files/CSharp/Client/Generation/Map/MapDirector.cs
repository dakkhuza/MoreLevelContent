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
            _notificationList = AccessTools.Field(typeof(Map), "mapNotifications");
            _notification = typeof(Map).GetNestedType("MapNotification", BindingFlags.NonPublic);

            _addMethod = AccessTools.Method(_notificationList.FieldType, "Add");
            _notifConstructor = AccessTools.Constructor(_notification, new Type[] { typeof(string), typeof(GUIFont), _notificationList.FieldType, typeof(Location) });
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
    }
}
