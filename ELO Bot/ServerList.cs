﻿using System;
using System.Collections.Generic;
using System.IO;
using Discord;
using Newtonsoft.Json;

namespace ELO_Bot
{
    public class ServerList
    {
        [JsonIgnore] public static string EloFile = Path.Combine(AppContext.BaseDirectory, "setup/serverlist.json");

        public List<Server> Serverlist { get; set; }

        public static Server Load(IGuild guild)
        {
            if (!File.Exists(EloFile))
                File.Create(EloFile).Dispose();
            var obj = JsonConvert.DeserializeObject<ServerList>(File.ReadAllText(EloFile));

            foreach (var server in obj.Serverlist)
                if (server.ServerId == guild.Id)
                    return server;

            var nullserver = new Server
            {
                ServerId = guild.Id,
                UserList = new List<Server.User>(),
                RegisterRole = 0,
                Registermessage = "Thankyou for Registering"
            };
            return nullserver;
        }

        public static ServerList LoadFull()
        {
            if (!File.Exists(EloFile))
                File.Create(EloFile).Dispose();
            var obj = JsonConvert.DeserializeObject<ServerList>(File.ReadAllText(EloFile));

            return obj;
        }

        public static void Saveserver(Server serverconfig)
        {
            var file = JsonConvert.DeserializeObject<ServerList>(File.ReadAllText(EloFile));
            foreach (var server in file.Serverlist)
                if (server.ServerId == serverconfig.ServerId)
                {
                    file.Serverlist.Remove(server);
                    break;
                }
            file.Serverlist.Add(serverconfig);
            var output = JsonConvert.SerializeObject(file, Formatting.Indented);
            File.WriteAllText(EloFile, output);
        }

        public class Server
        {
            public bool IsPremium { get; set; } = false;
            public string PremiumKey { get; set; } = "";

            public ulong ServerId { get; set; }
            public ulong RegisterRole { get; set; }
            public List<Ranking> Ranks { get; set; } = new List<Ranking>();
            public ulong AdminRole { get; set; }
            public string Registermessage { get; set; } = "Thankyou for Registering";
            public List<User> UserList { get; set; }

            public int Winamount { get; set; }
            public int Lossamount { get; set; }


            public List<Gamemode> Gamemodes { get; set; } = new List<Gamemode>();
            public List<string> Maps { get; set; } = new List<string>();
            public class Gamemode
            {
                public string Name { get; set; }
                public int PlayerLimit { get; set; }
            }

            public List<Q> Queue { get; set; }
            public class Q
            {
                public List<ulong> Users { get; set; } = new List<ulong>();
                public ulong ChannelId { get; set; }
            }


            public class Ranking
            {
                public ulong RoleId { get; set; }
                public int Points { get; set; }
            }


            public class User
            {
                public string Username { get; set; }
                public ulong UserId { get; set; }
                public int Points { get; set; }
                public int Wins { get; set; }
                public int Losses { get; set; }
            }
        }
    }
}