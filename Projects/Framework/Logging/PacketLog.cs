﻿// Copyright (c) Arctium Emulation.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Framework.Logging.IO;

namespace Framework.Logging
{
    public class PacketLog
    {
        public static bool Initialized;
        static LogWriter logger;

        public static void Initialize(string directory, string file)
        {
            logger = new LogWriter(directory, file);

            Initialized = true;
        }

        public static async void Write<T>(ushort value, byte[] data, EndPoint remote)
        {
            Func<Task> write = async delegate
            {
                var sb = new StringBuilder();
                var endPoint = remote as IPEndPoint;
                var clientInfo = endPoint.Address + ":" + endPoint.Port;

                sb.AppendLine($"Client: {clientInfo}");
                sb.AppendLine($"Time: {DateTime.Now}");
                sb.AppendLine($"Type: {typeof(T).Name}");

                if (Enum.IsDefined(typeof(T), value))
                    sb.AppendLine(string.Format("Name: {0}", Enum.GetName(typeof(T), value)));

                sb.AppendLine($"Value: 0x{value:X} ({value})");
                sb.AppendLine($"Length: {data.Length}");

                sb.AppendLine("|----------------------------------------------------------------|");
                sb.AppendLine("| 00  01  02  03  04  05  06  07  08  09  0A  0B  0C  0D  0E  0F |");
                sb.AppendLine("|----------------------------------------------------------------|");
                sb.Append("|");

                var count = 0;
                var ctr = 0;

                for (var i = 0; i < data.Length; i++)
                {
                    sb.Append($" {data[i] :X2} ");

                    if (count == 0xF)
                    {
                        sb.Append("|");
                        sb.Append("  " + Encoding.UTF8.GetString(data, ctr * 0x10, 0x10).Replace("\n", "\\n").Replace("\r", "\\r"));
                        sb.AppendLine();
                        sb.Append("|");

                        count = 0;

                        ++ctr;
                    }
                    else if (i == data.Length - 1)
                    {
                        for (var j = 0; j != (60 - (count * 4)); j++)
                            sb.Append(" ");

                        sb.Append("|");

                        sb.Append("  " + Encoding.UTF8.GetString(data, ctr * 0x10, count + 1).Replace("\n", "\\n").Replace("\r", "\\r"));
                    }
                    else
                        count++;
                };

                sb.AppendLine("");
                sb.AppendLine("|----------------------------------------------------------------|");
                sb.AppendLine("");

                await logger.Write(sb.ToString());
            };

            await write();
        }
    }
}
