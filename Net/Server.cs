﻿using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Xna.Framework;
using TanksRebirth.GameContent;
using TanksRebirth.GameContent.Systems;

namespace TanksRebirth.Net;

// moderately confused as to why this class isn't static... ¯\_(ツ)_/¯
public class Server
{
    public delegate void ServerStartDelegate(Server server);
    /// <summary>Fired when a server is created. Here you can hook into <see cref="serverNetListener"/>'s "NetworkReceiveEvent" to handle your packets.</summary>
    public static event ServerStartDelegate OnServerStart;
    public static NetManager serverNetManager;

    public static EventBasedNetListener serverNetListener;

    public string Password;
    public string Address;
    public int Port;

    public string Name;

    public static ushort MaxClients = 4;

    public static Client[] ConnectedClients;

    public static int CurrentClientCount;

    public static void CreateServer(ushort maxClients = 4)
    {
        MaxClients = maxClients;

        serverNetListener = new();
        serverNetManager = new(serverNetListener);

        ConnectedClients = new Client[maxClients];

        GameHandler.ClientLog.Write($"Server created.", Internals.LogType.Debug);

        NetPlay.MapServerNetworking();
    }

    public static void StartServer(string name, int port, string address, string password)
    {
        var server = new Server
        {
            Port = port,
            Address = address,
            Password = password,
            Name = name
        };

        NetPlay.CurrentServer = server;
        OnServerStart?.Invoke(server);

        GameHandler.ClientLog.Write($"Server started. (Name = \"{name}\" | Port = \"{port}\" | Address = \"{address}\" | Password = \"{password}\")", Internals.LogType.Debug);

        serverNetManager.Start(port);

        // serverNetManager.NatPunchEnabled = true;

        serverNetListener.ConnectionRequestEvent += request =>
        {
            if (serverNetManager.ConnectedPeersCount < MaxClients)
            {
                request.AcceptIfKey(password);
            }
            else
            {
                ChatSystem.SendMessage("User rejected: Incorrect password.", Color.Red);
                request.Reject();
            }
            serverNetListener.PeerConnectedEvent += peer =>
            {
                NetDataWriter writer = new();

                writer.Put("Client successfully connected.");

                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            };
        };
    }
}
