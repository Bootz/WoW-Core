﻿// Copyright (c) Arctium Emulation.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Framework.Network.Packets;

namespace WorldServer.Packets.Structures.Misc
{
    struct RestrictedFactionChangeRule : IServerStruct
    {
        public int Mask    { get; set; }
        public byte RaceID { get; set; }

        public void Write(Packet packet)
        {
            packet.Write(Mask);
            packet.Write(RaceID);
        }
    }
}
