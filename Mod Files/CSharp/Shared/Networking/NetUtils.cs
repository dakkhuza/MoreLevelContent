﻿
using Barotrauma;
using Barotrauma.Networking;
using System;

namespace MoreLevelContent.Networking
{
    /// <summary>
    /// Shared
    /// </summary>
    public static partial class NetUtil
    {
        internal static IWriteMessage CreateNetMsg(NetEvent target) => GameMain.LuaCs.Networking.Start(Enum.GetName(typeof(NetEvent), target));

        /// <summary>
        /// Register a method to run when the specified NetEvent happens
        /// </summary>
        /// <param name="target"></param>
        /// <param name="netEvent"></param>
        public static void Register(NetEvent target, LuaCsAction netEvent)
        {
            if (GameMain.IsSingleplayer) return;
            GameMain.LuaCs.Networking.Receive(Enum.GetName(typeof(NetEvent), target), netEvent);
        }
    }

    /// <summary>
    /// Events that are sent over the network
    /// </summary>
    public enum NetEvent
    {
        /// <summary>
        /// Send a config message to the server
        /// </summary>
        CONFIG_WRITE_SERVER,

        /// <summary>
        /// Send a config message to the clients
        /// </summary>
        CONFIG_WRITE_CLIENT,

        /// <summary>
        /// Request the current config from the server
        /// </summary>
        CONFIG_REQUEST,

        /// <summary>
        /// Used to test if the target has the mod installed
        /// </summary>
        PING_CLIENT,

        /// <summary>
        /// Used to reply to the server's ping
        /// </summary>
        PONG_SERVER,

        /// <summary>
        /// Send the location of a new distress signal
        /// </summary>
        MAP_SEND_NEWDISTRESS,

        /// <summary>
        /// Send the location of a new lost cargo mission
        /// </summary>
        MAP_SEND_NEWCARGO,

        /// <summary>
        /// Call the create distress method on the server
        /// </summary>
        COMMAND_CREATEDISTRESS,

        /// <summary>
        /// Fakes a world step
        /// </summary>
        COMMAND_STEPWORLD,

        /// <summary>
        /// Request a map connection equality check from the server
        /// </summary>
        MAP_CONNECTION_EQUALITYCHECK_REQUEST,

        /// <summary>
        /// Sends the result to the client
        /// </summary>
        MAP_CONNECTION_EQUALITYCHECK_SENDCLIENT,

        /// <summary>
        /// Reveals the specified map feature
        /// </summary>
        EVENT_REVEALMAPFEATURE,

        /// <summary>
        /// Updates the status of a pirate base
        /// </summary>
        PIRATEBASE_STATUS,

        /// <summary>
        /// Request information on custom data added to the campaign map by mlc
        /// </summary>
        MAP_REQUEST_STATE,
        
        /// <summary>
        /// Information on the current map state, distress beacons, pirate state, etc
        /// </summary>
        MAP_SEND_STATE
    }
}
