﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO_Bot.Preconditions;
using Newtonsoft.Json;

namespace ELO_Bot.Commands.Admin
{
    /// <summary>
    ///     ensure only admins can use the commands
    /// </summary>
    [RequireContext(ContextType.Guild)]
    [CheckBlacklist(true)]
    public class Setup : ModuleBase
    {
        private readonly CommandService _service;

        public Setup(CommandService service)
        {
            _service = service;
        }

        /// <summary>
        ///     Command to inisialise the server configuration (if it wasn't done initially)
        /// </summary>
        /// <returns></returns>
        [Command("Initialise")]
        [Summary("Initialise")]
        [Remarks("Run this command to add your server to the serverlist")]
        public async Task Initialise()
        {
            if (Servers.ServerList.All(x => x.ServerId != Context.Guild.Id))
            {
                var server = new Servers.Server
                {
                    ServerId = Context.Guild.Id,
                    UserList = new List<Servers.Server.User>()
                };

                Servers.ServerList.Add(server);
                await ReplyAsync("Server Initialised, users may now register");
                return;
            }

            await ReplyAsync("Server has already been initialised. Denied.");
        }

        /// <summary>
        ///     set the current channel for game announcements
        /// </summary>
        /// <returns></returns>
        [Command("SetAnnouncements")]
        [Summary("SetAnnouncements")]
        [Remarks("Set the current channel for game announcements")]
        public async Task SetAnnounce()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            server.AnnouncementsChannel = Context.Channel.Id;
            await ReplyAsync("GameAnnouncements will now be posted in this channel");
        }

        [Command("ToggleKD")]
        [Summary("ToggleKD")]
        [Remarks("Toggle whether or not to use K/D in the server")]
        public async Task ToggleKD()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            server.showkd = !server.showkd;
            await ReplyAsync($"ShowKD = {server.showkd}");
        }

        [Command("ToggleNegativeScore")]
        [Summary("ToggleNegativeScore")]
        [Remarks("Toggle whether or not to allow negative scores")]
        public async Task ToggleNegative()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            server.AllowNegativeScore = !server.AllowNegativeScore;
            await ReplyAsync($"Allow Negative = {server.AllowNegativeScore}");
        }

        [Command("SetNicknameFormat")]
        [Summary("SetNicknameFormat [ID]")]
        [Remarks("Set how scores are displayed in player nicknames")]
        public async Task SetNickname(int choice = 0)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            if (choice == 0)
            {
                var desc = new EmbedBuilder
                {
                    Description =
                        $"Type {Config.Load().Prefix} SetNickname [Option Number] to choose a layout for nicknames\n" +
                        $"1. 0 ~ Name\n" +
                        $"2. [0] Name\n" +
                        $"3. Name"
                };


                await ReplyAsync("", false, desc.Build());
            }
            else if (choice == 1 || choice == 2 || choice == 3)
            {
                server.UsernameSelection = choice;
                var desc = new EmbedBuilder
                {
                    Color = Color.Green,
                    Description = $"Username option has been set to #{choice}\n" +
                                  $"NOTE: Names are updated as scores are updated to reduce lag. As a result, inactive users may not have their usernames updated."
                };


                await ReplyAsync("", false, desc.Build());
            }
            else
            {
                throw new Exception("Invalid option specified");
            }
        }

        /// <summary>
        ///     set the role that users are given upon registering.
        /// </summary>
        /// <param name="role">role to give users. ie. @role</param>
        /// <returns></returns>
        [Command("SetRegisterRole")]
        [Summary("SetRegisterRole <@role>")]
        [Remarks("Sets the role users will join when registering")]
        public async Task SetReg(IRole role = null)
        {
            var embed = new EmbedBuilder();

            if (role == null)
            {
                embed.AddField("ERROR", "Please specify a role for users to be added to upon registering");
                embed.WithColor(Color.Red);
                await ReplyAsync("", false, embed.Build());
                return;
            }

            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            server.RegisterRole = role.Id;
            embed.AddField("Complete!", $"Upon registering, users will now be added to the role: {role.Name}");
            embed.WithColor(Color.Blue);
            await ReplyAsync("", false, embed.Build());
        }

        [Command("SetRegisterPoints")]
        [Summary("SetRegisterPoints <points>")]
        [Remarks("Sets theamount of points users are given upon registering")]
        public async Task SetRegisterPoints(int i = 0)
        {
            var embed = new EmbedBuilder();


            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            server.registerpoints = i;
            embed.AddField("Complete!", $"Upon registering, users will now be be given a default of {i} points");
            embed.WithColor(Color.Blue);
            await ReplyAsync("", false, embed.Build());
        }

        /// <summary>
        ///     toggle whether users are removed from queues when going idle/offline
        /// </summary>
        /// <returns></returns>
        [Command("ToggleAutoRemove")]
        [Summary("ToggleAutoRemove")]
        [Remarks("Set if users are removed from the queue when going offline")]
        public async Task IdleRemove()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            server.Autoremove = !server.Autoremove;
            if (server.Autoremove)
                await ReplyAsync("Users will be removed from queues if they go offline");
            else
                await ReplyAsync("Users will not be removed from queues if they go offline");
        }

        [Command("ToggleAutoDelete")]
        [Summary("ToggleAutoDelete")]
        [Remarks("Set if user profiles are deleted when they leave the server")]
        public async Task LeaveDelete()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            server.DeleteProfileOnLeave = !server.DeleteProfileOnLeave;
            if (server.DeleteProfileOnLeave)
                await ReplyAsync("User profiles will be deleted if they leave the server");
            else
                await ReplyAsync("User profiles will not be deleted if they leave the server");
        }

        /// <summary>
        ///     toggle whether to block users from joining more than one queue at a time.
        /// </summary>
        /// <returns></returns>
        [Command("ToggleMultiQueue")]
        [Summary("ToggleMultiQueue")]
        [Remarks("Toggle wether users are able to join more than one queue at a time")]
        public async Task MultiQueue()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            server.BlockMultiQueueing = !server.BlockMultiQueueing;
            if (server.BlockMultiQueueing)
                await ReplyAsync("Users will only be allowed in one queue at any one time.");
            else
                await ReplyAsync("Users are now allowed in any number of queues they want.");
        }

        /// <summary>
        ///     command for upgrading a server to the premium version of ELO Bot. Ie, have 20+ users registered
        /// </summary>
        /// <param name="key">key to input ie. 1234-5678-1234-5678</param>
        /// <returns></returns>
        [Command("Premium")]
        [Summary("Premium <key>")]
        [Remarks("Upgrade the server to premium and increase the userlimit to unlimited")]
        public async Task PremiumCommand(string key = null)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var embed = new EmbedBuilder();

            if (key == null)
            {
                embed.AddField("Premium",
                    "Premium allows servers to use the bot with more than 20 users to purchase it, check here: https://rocketr.net/buy/0e79a25902f5");
                embed.Color = Color.Blue;
                await ReplyAsync("", false, embed.Build());
                return;
            }


            if (CommandHandler.Keys.Contains(key))
            {
                if (server.Expiry > DateTime.UtcNow && server.IsPremium)
                {
                    embed.AddField("ERROR",
                        $"This server is already premium (expires {server.Expiry}), to avoid wasting your key please wait until it expires");
                    embed.Color = Color.Red;
                    await ReplyAsync("", false, embed.Build());
                    return;
                }

                CommandHandler.Keys.Remove(key);
                var obj = JsonConvert.SerializeObject(CommandHandler.Keys, Formatting.Indented);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "setup/keys.json"), obj);

                server.IsPremium = true;
                server.PremiumKey = key;
                server.Expiry = DateTime.UtcNow + TimeSpan.FromDays(31);
                embed.AddField("SUCCESS",
                    $"This server has been upgraded to premium for one month (expires {server.Expiry}), userlimits for registrations is now unrestricted.");
                embed.Color = Color.Green;
                await ReplyAsync("", false, embed.Build());
            }
            else
            {
                embed.AddField("ERROR INVALID KEY",
                    "Premium allows servers to use the bot with more than 20 users to purchase it, check here: https://rocketr.net/buy/0e79a25902f5");
                embed.Color = Color.Red;
                await ReplyAsync("", false, embed.Build());
            }
        }

        /// <summary>
        ///     set the rolethat is given bot administrator permissions
        /// </summary>
        /// <param name="adminrole"></param>
        /// <returns></returns>
        [Command("SetAdmin")]
        [Summary("SetAdmin <@role>")]
        [Remarks("sets the configurable admin role")]
        public async Task SetAdmin(IRole adminrole)
        {
            var embed = new EmbedBuilder();

            var s1 = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);

            s1.AdminRole = adminrole.Id;
            embed.AddField("Complete!", $"People with the role {adminrole.Mention} can now use admin commands");
            embed.WithColor(Color.Blue);
            await ReplyAsync("", false, embed.Build());
        }

        /// <summary>
        ///     set the role that is given moderator permissions. ie. Are able to use the `game` commands
        /// </summary>
        /// <param name="modRole"></param>
        /// <returns></returns>
        [Command("SetMod")]
        [Summary("SetMod <@role>")]
        [Remarks("Sets the moderator role (point updating access)")]
        public async Task SetMod(IRole modRole)
        {
            var embed = new EmbedBuilder();

            var s1 = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);

            s1.ModRole = modRole.Id;
            embed.AddField("Complete!",
                $"People with the role {modRole.Mention} can now use the following commands:\n" +
                "```\n" +
                "=win <@user1> <@user2>...\n" +
                "=lose <@user1> <@user2>...\n" +
                "=game <lobby> <match-no.> <team1/team2>\n" +
                "```");
            embed.WithColor(Color.Blue);
            await ReplyAsync("", false, embed.Build());
        }

        /// <summary>
        ///     set the welcome message given to users when they register
        /// </summary>
        /// <param name="message">message to be displayed</param>
        /// <returns></returns>
        [Command("SetRegisterMessage")]
        [Summary("SetRegistermessage <message>")]
        [Remarks("sets the configurable registration message")]
        public async Task SetWelcome([Remainder] string message = null)
        {
            var embed = new EmbedBuilder();

            var s1 = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);

            if (message == null)
            {
                embed.AddField("ERROR", "Please specify a welcome message for users");
                embed.WithColor(Color.Red);
                await ReplyAsync("", false, embed.Build());
                return;
            }

            s1.Registermessage = message;
            embed.AddField("Complete!", "Registration Message will now include the following:\n" +
                                        $"{message}");
            embed.WithColor(Color.Blue);
            await ReplyAsync("", false, embed.Build());
        }

        /// <summary>
        ///     sets the points removed from a user when they lose.
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        [Command("ModifyLoss")]
        [Summary("ModifyLoss <points>")]
        [Remarks("Sets the servers Loss amount")]
        public async Task Lose(int points)
        {
            var embed = new EmbedBuilder();
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            if (points == 0)
            {
                embed.AddField("ERROR", "Please supply a number that isnt 0");
                embed.Color = Color.Red;
                await ReplyAsync("", false, embed.Build());
                return;
            }
            if (points <= 0)
                points = Math.Abs(points);
            server.Lossamount = points;
            embed.AddField("Success", $"Upon losing, users will now lose {points} points");
            embed.Color = Color.Green;
            await ReplyAsync("", false, embed.Build());
        }

        /// <summary>
        ///     set the points given to a user when they win
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        [Command("ModifyWin")]
        [Summary("ModifyWin <points>")]
        [Remarks("Sets the servers Win amount")]
        public async Task Win(int points)
        {
            var embed = new EmbedBuilder();
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            if (points == 0)
            {
                embed.AddField("ERROR", "Please supply a number that isnt 0");
                embed.Color = Color.Red;
                await ReplyAsync("", false, embed.Build());
                return;
            }
            if (points <= 0)
                points = Math.Abs(points);
            server.Winamount = points;
            embed.AddField("Success", $"Upon Winning, users will now gain {points} points");
            embed.Color = Color.Green;
            await ReplyAsync("", false, embed.Build());
        }

        
        /// <summary>
        ///     server owner only command, resets all user scores on the scoreboard.
        /// </summary>
        /// <returns></returns>
        [Command("ScoreboardReset", RunMode = RunMode.Async)]
        [Summary("ScoreboardReset")]
        [Remarks("Reset Points, Wins and Losses for all users in the server")]
        [ServerOwner]
        public async Task Reset()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            await ReplyAsync("Working...");

            var reset = server.UserList.ToList();
            foreach (var user in reset)
            {
                if (user.Points != server.registerpoints || user.Wins != 0 || user.Losses != 0)
                {
                    user.Points = server.registerpoints;
                    user.Wins = 0;
                    user.Losses = 0;
                }
            }
            server.UserList = reset;

            await ReplyAsync("Leaderboard Reset Complete!\n" +
                             "NOTE: Names and ranks will be reset over the next few minutes.\n" +
                             $"EST time = {(double)reset.Count * 6/60/60} hours");
            var i = 0;
            var completion = await ReplyAsync($"{i}/{reset.Count} completed");
            var botposition = ((SocketGuild) Context.Guild).Users.First(x => x.Id == Context.Client.CurrentUser.Id)
                .Roles
                .OrderByDescending(x => x.Position).First().Position;
            foreach (var user in reset)
            {
                try
                {
                    i++;
                    var us = await Context.Guild.GetUserAsync(user.UserId);

                    var nick = us.Nickname ?? "";
                    if (!nick.Contains(Globals.GetNamePrefix(server, user.UserId, true)) &&
                        Context.Guild.OwnerId != us.Id &&
                        ((SocketGuildUser) us).Roles.OrderByDescending(x => x.Position).First().Position < botposition)
                    {
                        try
                        {
                            await us.ModifyAsync(x =>
                            {
                                x.Nickname = Globals.GetNamePrefix(server, user.UserId) + $" {user.Username}";
                            });
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }

                    }

                    await Task.Delay(2500);

                    await us.RemoveRolesAsync(server.Ranks.Select(x => Context.Guild.GetRole(x.RoleId)));
                    await Task.Delay(1000);
                    if (server.Ranks.Count(x => x.Points < user.Points) > 0)
                    {
                        var rank = server.Ranks.Where(x => x.Points < user.Points).OrderByDescending(x => x.Points).First();
                        await us.AddRoleAsync(Context.Guild.GetRole(rank.RoleId));
                    }
                    var i1 = i;
                    await completion.ModifyAsync(x => x.Content = $"{i1}/{reset.Count} completed");

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            await ReplyAsync("Reset complete.");
        }
        

        /// <summary>
        ///     list all commands unavailable to regilar users
        ///     these commands have been 'blacklisted' and are not able to be used by regular users.
        /// </summary>
        /// <returns></returns>
        [Command("Blacklist")]
        [Remarks("List blacklisted commands")]
        [Summary("Blacklist")]
        public async Task Blacklist()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);

            if (server.moduleConfig.DisabledTypes.Count == 0)
            {
                await ReplyAsync("There are no blacklisted commands in this server");
                return;
            }

            var embed = new EmbedBuilder();
            embed.AddField("Blacklist", $"{string.Join("\n", server.moduleConfig.DisabledTypes.Select(x => $"{(x.IsCommand ? "C: " : "M: ")}{x.Name}\nAdmin: {x.Setting.AdminAllowed}\nMod: {x.Setting.ModAllowed}\nRegistered: {x.Setting.RegisteredAllowed}"))}");

            await ReplyAsync("", false, embed);
        }

        /// <summary>
        ///     add a command to the blacklist.
        /// </summary>
        /// <param name="selection">a compination of ints and commas indicating the selections</param>
        /// <param name="name">command or module name.</param>
        /// <returns></returns>
        [Command("BlacklistAdd")]
        [Remarks("Blacklist a command from all regular users.")]
        [Summary("BlacklistAdd <command-name>")]
        public async Task BlacklistAdd(string selection, string name)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);

            var modulematch = _service.Modules.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase));
            var cmdmatch = _service.Commands.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase) || x.Aliases.Any(a => string.Equals(a, name, StringComparison.CurrentCultureIgnoreCase)));

            var intselections = selection.Split(',');

            var bsettings = new Servers.Server.ModuleConfig.SetupConf
            {
                AdminAllowed = false,
                ModAllowed = false,
                RegisteredAllowed = false,
                UnRegisteredAllowed = false
            };

            EmbedBuilder embed;
            if (int.TryParse(intselections[0], out var zerocheck))
            {

                    foreach (var s in intselections)
                        if (int.TryParse(s, out var sint))
                        {
                            if (sint < 1 || sint > 4)
                            {
                                await ReplyAsync($"Invalid Input {s}\n" +
                                                 "only 1-4 are accepted.");
                                return;
                            }

                            switch (sint)
                            {
                                case 1:
                                    bsettings.AdminAllowed = true;
                                    break;
                                case 2:
                                    bsettings.ModAllowed = true;
                                    break;
                                case 3:
                                    bsettings.RegisteredAllowed = true;
                                    break;
                                case 4:
                                    bsettings.UnRegisteredAllowed = true;
                                    break;
                            }
                        }
                        else
                        {
                            await ReplyAsync($"Invalid Input {s}");
                            return;
                        }

                    embed = new EmbedBuilder
                    {
                        Description = $"{name}\n" +
                                      $"Admin Allowed: {bsettings.AdminAllowed}\n" +
                                      $"Mod Allowed: {bsettings.ModAllowed}\n" +
                                      $"Registered Allowed: {bsettings.RegisteredAllowed}\n" +
                                      $"Unregistered Allowed: {bsettings.UnRegisteredAllowed}"
                    };
            }
            else
            {
                await ReplyAsync("Input Error!");
                return;
            }


            if (cmdmatch != null || modulematch != null)
            {
                if (modulematch != null)
                {
                    if (server.moduleConfig.DisabledTypes.Any(x => string.Equals(x.Name, modulematch.Name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        await ReplyAsync("Command already blacklisted.");
                        return;
                    }

                    server.moduleConfig.DisabledTypes.Add(new Servers.Server.ModuleConfig.DisabledType
                    {
                        IsCommand = false,
                        Name = modulematch.Name,
                        Setting = bsettings
                    });
                }

                if (cmdmatch != null)
                {
                    if (server.moduleConfig.DisabledTypes.Any(x => string.Equals(x.Name, cmdmatch.Name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        await ReplyAsync("Command already blacklisted.");
                        return;
                    }

                    server.moduleConfig.DisabledTypes.Add(new Servers.Server.ModuleConfig.DisabledType
                    {
                        IsCommand = true,
                        Name = cmdmatch.Name,
                        Setting = bsettings
                    });
                }
                await ReplyAsync("", false, embed.Build());
                return;
            }

            await ReplyAsync("No Matching Command.");
        }

        /// <summary>
        ///     remove a command from the blacklist
        /// </summary>
        /// <param name="cmdname">command name</param>
        /// <returns></returns>
        [Command("BlacklistDel")]
        [Remarks("Remove a blacklisted command")]
        [Summary("BlacklistDel <command-name>")]
        public async Task BlacklistDel(string cmdname)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);

            var toremove = server.moduleConfig.DisabledTypes.FirstOrDefault(x => string.Equals(x.Name, cmdname, StringComparison.CurrentCultureIgnoreCase));
            if (toremove == null)
            {
                await ReplyAsync("Error, Not Blacklisted");
            }
            else
            {
                server.moduleConfig.DisabledTypes.Remove(toremove);
                await ReplyAsync("Success, Blacklisted item removed");
            }
        }

        [Command("BlacklistInfo")]
        [Remarks("Info on how to use the blacklist command")]
        [Summary("BlacklistInfo")]
        public async Task BlacklistInfo()
        {
            await ReplyAsync("", false, new EmbedBuilder
            {
                Description =
                    "You can select roles to bypass the blacklist\n" +
                    "__Key__\n" +
                    "`1` - Allow Admin\n" +
                    "`2` - Allow Moderator\n" +
                    "`3` - Allow RegisteredUser\n" +
                    "`4` - Allow UnregisteredUser\n\n" +
                    "Usage\n" +
                    $"`{Config.Load().Prefix} 1 Leave` - this allows the admin role to use the leave command but no others\n" +
                    "You can use commas to use multiple Settings on the same item.\n" +
                    $"`{Config.Load().Prefix} 1,2,3 Join` - this allows admins, moderators and registered users to use the join command"
            }.Build());
        }

        /// <summary>
        ///     server information
        /// </summary>
        /// <returns></returns>
        [Command("Server")]
        [Remarks("Stats and info about the bot & current server")]
        [Summary("Server")]
        [CheckRegistered]
        public async Task Stats()
        {
            var embed = new EmbedBuilder();
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);

            var admin = Context.Guild.GetRole(server.AdminRole);
            embed.AddField("Admin Role", $"{admin?.Name ?? "N/A"}");
            var mod = Context.Guild.GetRole(server.ModRole);
            embed.AddField("Mod Role", $"{mod?.Name ?? "N/A"}");
            var ann = await Context.Guild.GetChannelAsync(server.AnnouncementsChannel);
            embed.AddField("Announcements Channel", $"{ann?.Name ?? "N/A"}");
            embed.AddField("Is Premium?", $"Premium: {server.IsPremium}\n" +
                                            $"Expires: {server.Expiry}");
            embed.AddField("Points Per Win/Loss", $"{server.Winamount}/{server.Lossamount}");
            embed.AddField("Counts", $"Lobbies: {server.Queue?.Count}\n" +
                                        $"Ranks: {server.Ranks?.Count}\n" +
                                        $"Registered Users: {server.UserList?.Count}");
            embed.AddField("Registration Message", $"{server.Registermessage}");
            var rrole = Context.Guild.GetRole(server.RegisterRole);
            embed.AddField("Registration Role", $"{rrole?.Name ?? "N/A"}");
            embed.AddField("Auto Remove from queue's if offline", $"{server.Autoremove}");
            embed.AddField("Auto Delete Profiles if user leaves server", $"{server.DeleteProfileOnLeave}");
            await ReplyAsync("", false, embed.Build());
        }
    }
}