﻿// Copyright (c) Arctium Emulation.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using AuthServer.Constants.Net;
using AuthServer.Managers;
using AuthServer.Network.Packets;
using Framework.Cryptography.BNet;
using Framework.Database;
using Framework.Database.Auth.Entities;
using Framework.Logging;
using Framework.Logging.IO;
using Framework.Misc;
using Framework.Network.Packets;

namespace AuthServer.Network.Sessions
{
    class AuthSession : IDisposable
    {
        public Account Account { get; set; }
        public List<GameAccount> GameAccounts { get; set; }
        public GameAccount GameAccount { get; set; }
        public SRP6a SecureRemotePassword { get; set; }
        public BNetCrypt Crypt { get; set; }

        Socket client;
        byte[] dataBuffer = new byte[0x800];

        public AuthSession(Socket socket)
        {
            client = socket;
        }

        public void Accept()
        {
            var socketEventargs = new SocketAsyncEventArgs();

            socketEventargs.SetBuffer(dataBuffer, 0, dataBuffer.Length);

            socketEventargs.Completed += Process;
            socketEventargs.UserToken = client;
            socketEventargs.SocketFlags = SocketFlags.None;

            client.ReceiveAsync(socketEventargs);
        }

        void Process(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                var socket = e.UserToken as Socket;
                var recievedBytes = e.BytesTransferred;

                if (recievedBytes != 0)
                {
                    // Enable packet encryption.
                    if (Crypt == null && dataBuffer[0] == 0x45 && dataBuffer[1] == 0x01)
                    {
                        Crypt = new BNetCrypt(SecureRemotePassword.SessionKey);

                        Buffer.BlockCopy(dataBuffer, 2, dataBuffer, 0, recievedBytes -= 2);

                        Log.Debug($"Encryption for account '{Account.Id}' enabled");
                    }

                    if (Crypt != null && Crypt.IsInitialized)
                        Crypt.Decrypt(dataBuffer, recievedBytes);

                    ProcessPacket(recievedBytes);

                    if (client != null)
                        client.ReceiveAsync(e);
                }
                else
                    socket.Close();
            }
            catch (Exception ex)
            {
                Dispose();

                ExceptionLog.Write(ex);

                Log.Error(ex.Message);
            }
        }

        void ProcessPacket(int size)
        {
            var packet = new AuthPacket(dataBuffer, size);
            
            PacketLog.Write<AuthClientMessage>(packet.Header.Message, packet.Data, client.RemoteEndPoint);

            if (packet != null)
            {
                var currentClient = Manager.SessionMgr.Clients.AsParallel().SingleOrDefault(c => c.Value.Session.Equals(this));

                if (currentClient.Value != null)
                    PacketManager.InvokeHandler(packet, currentClient.Value);
            }
        }

        public void Send(AuthPacket packet)
        {
            try
            {
                packet.Finish();

                if (packet.Header != null)
                    PacketLog.Write<AuthServerMessage>(packet.Header.Message, packet.Data, client.RemoteEndPoint);

                if (Crypt != null && Crypt.IsInitialized)
                    Crypt.Encrypt(packet.Data, packet.Data.Length);

                var socketEventargs = new SocketAsyncEventArgs();

                socketEventargs.SetBuffer(packet.Data, 0, packet.Data.Length);

                socketEventargs.Completed += SendCompleted;
                socketEventargs.UserToken = packet;
                socketEventargs.RemoteEndPoint = client.RemoteEndPoint;
                socketEventargs.SocketFlags = SocketFlags.None;

                client.Send(packet.Data);
            }
            catch (Exception ex)
            {
                Dispose();

                ExceptionLog.Write(ex);

                Log.Error(ex.Message);
            }
        }

        void SendCompleted(object sender, SocketAsyncEventArgs e)
        {
        }

        public void GenerateSessionKey(byte[] clientSalt, byte[] serverSalt)
        {
            var hmac = new HMACSHA1(SecureRemotePassword.SessionKey);
            var wow = Encoding.ASCII.GetBytes("WoW\0");
            var wowSessionKey = new byte[0x28];

            hmac.TransformBlock(wow, 0, wow.Length, wow, 0);
            hmac.TransformBlock(clientSalt, 0, clientSalt.Length, clientSalt, 0);
            hmac.TransformFinalBlock(serverSalt, 0, serverSalt.Length);

            Buffer.BlockCopy(hmac.Hash, 0, wowSessionKey, 0, hmac.Hash.Length);

            hmac.Initialize();
            hmac.TransformBlock(wow, 0, wow.Length, wow, 0);
            hmac.TransformBlock(serverSalt, 0, serverSalt.Length, serverSalt, 0);
            hmac.TransformFinalBlock(clientSalt, 0, clientSalt.Length);

            Buffer.BlockCopy(hmac.Hash, 0, wowSessionKey, hmac.Hash.Length, hmac.Hash.Length);

            GameAccount.SessionKey = wowSessionKey.ToHexString();

            // Update SessionKey in database
            DB.Auth.Update(GameAccount);
        }

        public string GetClientInfo()
        {
            var ipEndPoint = client.RemoteEndPoint as IPEndPoint;

            return ipEndPoint != null ? ipEndPoint.Address + ":" + ipEndPoint.Port : "";
        }

        public void Dispose()
        {
            client = null;
            Account = null;
            SecureRemotePassword = null;
        }
    }
}
