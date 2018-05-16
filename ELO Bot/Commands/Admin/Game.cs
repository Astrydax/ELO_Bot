﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using ELO_Bot.Preconditions;

namespace ELO_Bot.Commands.Admin
{
    /// <summary>
    ///     Checks the commands against the blacklist to ensure that a blacklisted command is not run
    /// </summary>
    [CheckBlacklist]
    [CheckModerator]
    public class Game : InteractiveBase
    {
        /// <summary>
        ///     adds a win and increaces the points for the specified users.
        /// </summary>
        /// <param name="userlist">a seperated list of users ie. @user1 @user2 @user3...</param>
        /// <returns></returns>
        [Command("Win", RunMode = RunMode.Async)]
        [Summary("Win <users>")]
        [Remarks("Add a win + win points for the specified users")]
        public async Task Win(params SocketGuildUser[] userlist)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var points = server.Winamount;
            if (!(server.Winamount > 0))
                throw new Exception("ERROR this server's win modifier has not been set up yet.");

            await WinLossPoints(server, userlist.Select(x => (IUser)x).ToList(), true, points);
        }

        /// <summary>
        ///     add a loss to the specified players and decrease their points accordingly
        /// </summary>
        /// <param name="userlist">a seperated list of users ie. @user1 @user2 @user3...</param>
        /// <returns></returns>
        [Command("Lose", RunMode = RunMode.Async)]
        [Summary("Lose <users>")]
        [Remarks("Add a loss to the specified users")]
        public async Task Lose(params SocketGuildUser[] userlist)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var points = server.Lossamount;

            if (!(server.Lossamount > 0))
                throw new Exception("ERROR this server's loss modifier has not been set up yet.");

            await WinLossPoints(server, userlist.Select(x => (IUser)x).ToList(), false, points);
        }

        /// <summary>
        ///     exact inverse of the game command
        ///     runs checks for the parameters, lobbyname & game number
        ///     creates lists of users in both teams and modifies scores accordingly
        /// </summary>
        /// <param name="lobbyname">name of the lobby the game was originally hosted in</param>
        /// <param name="gamenumber">game number for the specified lobby</param>
        /// <param name="team">winning team ie. Team2 or Team2</param>
        /// <returns></returns>
        [Command("UndoGame", RunMode = RunMode.Async)]
        [Summary("UndoGame <lobbyname> <gamenumber> <winningteam>")]
        [Remarks("Undo the results of a previous game")]
        public async Task UnWin(string lobbyname, int gamenumber, [Remainder] string team)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var channel = Context.Guild.TextChannels.FirstOrDefault(x => string.Equals(x.Name, lobbyname, StringComparison.CurrentCultureIgnoreCase));

            if (channel == null)
            {
                var queuechannels = "";
                foreach (var chan in server.Queue)
                {
                    var getqueuechannels = await ((IGuild) Context.Guild).GetChannelAsync(chan.ChannelId);
                    queuechannels += $"{getqueuechannels.Name}\n";
                }

                await ReplyAsync("**ERROR:** Please specify the channel in which queue was created\n" +
                                 "Here are a list:\n" +
                                 $"{queuechannels}");
                return;
            }

            var game = server.Gamelist.FirstOrDefault(x => x.LobbyId == channel.Id
                                                           && x.GameNumber == gamenumber);

            if (game == null)
            {
                await ReplyAsync("ERROR: Invalid Game number/channel");
                return;
            }

            if (game.Cancelled)
            {
                await ReplyAsync("ERROR: Game has been cancelled, use the cancel command again to undo this.");
                return;
            }

            var team1 = new List<IUser>();
            var team2 = new List<IUser>();
            foreach (var user in game.Team1)
                try
                {
                    team1.Add(await ((IGuild) Context.Guild).GetUserAsync(user));
                }
                catch
                {
                    await ReplyAsync(
                        $"{server.UserList.FirstOrDefault(x => x.UserId == user)?.Username} was unavailable");
                }


            foreach (var user in game.Team2)
                try
                {
                    team2.Add(await ((IGuild) Context.Guild).GetUserAsync(user));
                }
                catch
                {
                    await ReplyAsync(
                        $"{server.UserList.FirstOrDefault(x => x.UserId == user)?.Username} was unavailable");
                }


            switch (team.ToLower())
            {
                case "team1":
                case "team 1":
                case "1":
                    await UndoWinLossPoints(server, team1, true, server.Winamount,
                        $"{lobbyname} {game.GameNumber} {team}");
                    await UndoWinLossPoints(server, team2, false, server.Lossamount,
                        $"{lobbyname} {game.GameNumber} {team}");
                    game.Result = null;
                    break;
                case "team2":
                case "team 2":
                case "2":
                    await UndoWinLossPoints(server, team2, true, server.Winamount,
                        $"{lobbyname} {game.GameNumber} {team}");
                    await UndoWinLossPoints(server, team1, false, server.Lossamount,
                        $"{lobbyname} {game.GameNumber} {team}");
                    game.Result = null;
                    break;
                default:
                    await ReplyAsync(
                        "Please specify a team in the following format `=game <lobby> <number> team1` or `=game <lobby> <number> team2`");
                    break;
            }
        }


        /// <summary>
        ///     runs checks for the parameters, lobbyname & game number
        ///     creates lists of users in both teams and modifies scores accordingly
        /// </summary>
        /// <param name="lobbyname">name of the lobby the game was originally hosted in</param>
        /// <param name="gamenumber">game number for the specified lobby</param>
        /// <param name="team">winning team ie. Team2 or Team2</param>
        /// <returns></returns>
        [Command("Game", RunMode = RunMode.Async)]
        [Summary("Game <lobbyname> <gamenumber> <winningteam>")]
        [Remarks("Automatically update wins/losses for the selected team")]
        public async Task DoGame(string lobbyname, int gamenumber, [Remainder] string team)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var channel = Context.Guild.TextChannels.FirstOrDefault(x => string.Equals(x.Name, lobbyname, StringComparison.CurrentCultureIgnoreCase));

            if (channel == null)
            {
                var queuechannels = "";
                foreach (var chan in server.Queue)
                    try
                    {
                        var getqueuechannels = await ((IGuild) Context.Guild).GetChannelAsync(chan.ChannelId);
                        queuechannels += $"{getqueuechannels.Name}\n";
                    }
                    catch
                    {
                        //
                    }
                await ReplyAsync("**ERROR:** Please specify the channel in which queue was created\n" +
                                 "Here are a list:\n" +
                                 $"{queuechannels}");
                return;
            }

            var game = server.Gamelist.FirstOrDefault(x => x.LobbyId == channel.Id
                                                           && x.GameNumber == gamenumber);

            if (game == null)
            {
                await ReplyAsync("ERROR: Invalid Game number/channel");
                return;
            }

            if (game.Cancelled)
            {
                await ReplyAsync("ERROR: Game has been cancelled, use the cancel command again to undo this.");
                return;
            }

            if (game.Result != null)
            {
                switch (game.Result)
                {
                    case true:
                        await ReplyAsync("Team1 is already recorded as winning this game.");
                        break;
                    case false:
                        await ReplyAsync("Team2 is already recorded as winning this game.");
                        break;
                }

                await ReplyAsync("Reply with `YES` to continue scoring this game, reply with `NO` to cancel");
                var response = await NextMessageAsync(timeout: TimeSpan.FromSeconds(30));
                if (response.Content.ToLower().Contains("yes"))
                {
                    //
                }
                else
                {
                    await ReplyAsync("Command Aborted.");
                    return;
                }
            }

            var team1 = new List<IUser>();
            var team2 = new List<IUser>();
            foreach (var user in game.Team1)
                try
                {
                    team1.Add(await ((IGuild) Context.Guild).GetUserAsync(user));
                }
                catch
                {
                    await ReplyAsync(
                        $"{server.UserList.FirstOrDefault(x => x.UserId == user)?.Username} was unavailable");
                }


            foreach (var user in game.Team2)
                try
                {
                    team2.Add(await ((IGuild) Context.Guild).GetUserAsync(user));
                }
                catch
                {
                    await ReplyAsync(
                        $"{server.UserList.FirstOrDefault(x => x.UserId == user)?.Username} was unavailable");
                }

            switch (team.ToLower())
            {
                case "team1":
                    await WinLossPoints(server, team1, true, server.Winamount, $"{lobbyname} {game.GameNumber} {team}");
                    await WinLossPoints(server, team2, false, server.Lossamount,
                        $"{lobbyname} {game.GameNumber} {team}");
                    game.Result = true;
                    break;
                case "team2":
                    await WinLossPoints(server, team2, true, server.Winamount, $"{lobbyname} {game.GameNumber} {team}");
                    await WinLossPoints(server, team1, false, server.Lossamount,
                        $"{lobbyname} {game.GameNumber} {team}");
                    game.Result = false;
                    break;
                default:
                    await ReplyAsync(
                        "Please specify a team in the following format `=game <lobby> <number> team1` or `=game <lobby> <number> team2`");
                    break;
            }
        }

        [Command("AddKills", RunMode = RunMode.Async)]
        [Summary("AddKills <kills> <users>")]
        [Remarks("add points to the specified users")]
        public async Task AddKills(int kills, params SocketGuildUser[] userlist)
        {
            var embed = new EmbedBuilder();
            if (kills <= 0)
            {
                embed.AddField("ERROR", "This command is only for adding kills");
                embed.Color = Color.Red;
                await ReplyAsync("", false, embed.Build());
            }
            else
            {
                var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
                if (server.showkd == false)
                {
                    embed.AddField("ERROR", "K/D Mode is Disabled");
                    embed.Color = Color.Red;
                    await ReplyAsync("", false, embed.Build());
                    return;
                }
                foreach (var user in userlist)
                {
                    var success = false;
                    var userval = 0;
                    foreach (var subject in server.UserList)
                        if (subject.UserId == user.Id)
                        {
                            subject.kills = subject.kills + kills;
                            success = true;
                            userval = subject.kills;
                        }

                    if (!success)
                        embed.AddField($"{user.Username} ERROR", "Not Registered");
                    else
                        embed.AddField($"{user.Username} MODIFIED", $"Added: +{kills}\n" +
                                                                    $"Current Kills: {userval}");
                }
                embed.Color = Color.Green;
                await ReplyAsync("", false, embed.Build());
            }
        }

        [Command("AddDeaths", RunMode = RunMode.Async)]
        [Summary("AddDeaths <deaths> <users>")]
        [Remarks("add deaths to the specified users")]
        public async Task AddDeaths(int deaths, params SocketGuildUser[] userlist)
        {
            var embed = new EmbedBuilder();
            if (deaths <= 0)
            {
                embed.AddField("ERROR", "This command is only for adding deaths");
                embed.Color = Color.Red;
                await ReplyAsync("", false, embed.Build());
            }
            else
            {
                var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
                if (server.showkd == false)
                {
                    embed.AddField("ERROR", "K/D Mode is Disabled");
                    embed.Color = Color.Red;
                    await ReplyAsync("", false, embed.Build());
                    return;
                }
                foreach (var user in userlist)
                {
                    var success = false;
                    var userval = 0;
                    foreach (var subject in server.UserList)
                        if (subject.UserId == user.Id)
                        {
                            subject.deaths = subject.deaths + deaths;
                            success = true;
                            userval = subject.deaths;
                        }

                    if (!success)
                        embed.AddField($"{user.Username} ERROR", "Not Registered");
                    else
                        embed.AddField($"{user.Username} MODIFIED", $"Added: +{deaths}\n" +
                                                                    $"Current Deaths: {userval}");
                }
                embed.Color = Color.Green;
                await ReplyAsync("", false, embed.Build());
            }
        }

        [Command("RemoveKills", RunMode = RunMode.Async)]
        [Summary("RemoveKills <kills> <users>")]
        [Remarks("remove kills from the specified users")]
        public async Task DelKills(int kills, params SocketGuildUser[] userlist)
        {
            var embed = new EmbedBuilder();

            if (kills <= 0)
                kills = Math.Abs(kills);
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            if (server.showkd == false)
            {
                embed.AddField("ERROR", "K/D Mode is Disabled");
                embed.Color = Color.Red;
                await ReplyAsync("", false, embed.Build());
                return;
            }
            foreach (var user in userlist)
            {
                var success = false;
                var userval = 0;
                foreach (var subject in server.UserList)
                    if (subject.UserId == user.Id)
                    {
                        subject.kills = subject.kills - kills;
                        if (subject.kills < 0)
                            subject.kills = 0;
                        success = true;
                        userval = subject.kills;
                    }
                if (!success)
                    embed.AddField($"{user.Username} ERROR", "Not Registered");
                else
                    embed.AddField($"{user.Username} MODIFIED", $"Removed: -{kills}\n" +
                                                                $"Current Kills: {userval}");
            }
            embed.Color = Color.Green;
            await ReplyAsync("", false, embed.Build());
        }

        [Command("RemoveDeaths", RunMode = RunMode.Async)]
        [Summary("RemoveDeaths <deaths> <users>")]
        [Remarks("remove points from the specified users")]
        public async Task DelDeaths(int deaths, params SocketGuildUser[] userlist)
        {
            var embed = new EmbedBuilder();

            if (deaths <= 0)
                deaths = Math.Abs(deaths);
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            if (server.showkd == false)
            {
                embed.AddField("ERROR", "K/D Mode is Disabled");
                embed.Color = Color.Red;
                await ReplyAsync("", false, embed.Build());
                return;
            }
            foreach (var user in userlist)
            {
                var success = false;
                var userval = 0;
                foreach (var subject in server.UserList)
                    if (subject.UserId == user.Id)
                    {
                        subject.deaths = subject.deaths - deaths;
                        if (subject.deaths < 0)
                            subject.deaths = 0;
                        success = true;
                        userval = subject.deaths;
                    }
                if (!success)
                    embed.AddField($"{user.Username} ERROR", "Not Registered");
                else
                    embed.AddField($"{user.Username} MODIFIED", $"Removed: -{deaths}\n" +
                                                                $"Current Deaths: {userval}");
            }
            embed.Color = Color.Green;
            await ReplyAsync("", false, embed.Build());
        }

        [Command("Cancel", RunMode = RunMode.Async)]
        [Summary("Cancel <lobbyname> <gamenumber>")]
        [Remarks("Cancel a game so the score is not set.")]
        public async Task Cancel(string lobbyname, int gamenumber)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var channel = Context.Guild.TextChannels.FirstOrDefault(x => string.Equals(x.Name, lobbyname, StringComparison.CurrentCultureIgnoreCase));

            if (channel == null)
            {
                var queuechannels = "";
                foreach (var chan in server.Queue)
                    try
                    {
                        var getqueuechannels = await ((IGuild)Context.Guild).GetChannelAsync(chan.ChannelId);
                        queuechannels += $"{getqueuechannels.Name}\n";
                    }
                    catch
                    {
                        //
                    }
                await ReplyAsync("**ERROR:** Please specify the channel in which queue was created\n" +
                                 "Here are a list:\n" +
                                 $"{queuechannels}");
                return;
            }

            var game = server.Gamelist.FirstOrDefault(x => x.LobbyId == channel.Id
                                                           && x.GameNumber == gamenumber);

            if (game == null)
            {
                await ReplyAsync("ERROR: Invalid Game number/channel");
                return;
            }

            if (game.Result != null)
            {
                switch (game.Result)
                {
                    case true:
                        await ReplyAsync("Team1 is already recorded as winning this game.");
                        break;
                    case false:
                        await ReplyAsync("Team2 is already recorded as winning this game.");
                        break;
                }

                await ReplyAsync("You cannot cancel a game that has already been played");
            }
            else
            {
                game.Cancelled = !game.Cancelled;
                await ReplyAsync("Game has been cancelled");
            }
        }

        /// <summary>
        ///     For given server and users
        ///     if win = true
        ///     subtract one win and given points
        ///     if win = false
        ///     subtract one loss and add given points
        /// </summary>
        /// <param name="server"> current server's object</param>
        /// <param name="users"> users to be modified.</param>
        /// <param name="win"> check if the provided users won or lost</param>
        /// <param name="points">default points for the user's to be modified.</param>
        /// <param name="gametext">String of the game info</param>
        /// <returns></returns>
        public async Task UndoWinLossPoints(Servers.Server server, List<IUser> users, bool win, int points,
            string gametext = null)
        {
            var embed = new EmbedBuilder();
            foreach (var user in users)
            {
                var usr = server.UserList.FirstOrDefault(x => x.UserId == user.Id);

                if (usr == null || user.Id != usr.UserId) continue;
                {
                    points = win ? server.Winamount : server.Lossamount;

                    //checks against possible ranks for each user. 
                    //if the user has a rank that has a different point modifier to the server's one, then modify 
                    //their points according to their rank
                    //if there is no role then ignore this.
                    try
                    {
                        var toprole = server.Ranks.Where(x => x.Points <= usr.Points).Max(x => x.Points);

                        var top = server.Ranks.Where(x => x.Points == toprole);

                        try
                        {
                            var rank = top.First();
                            if (rank.WinModifier != 0 && win)
                                points = rank.WinModifier;
                            else if (rank.LossModifier != 0 && !win)
                                points = rank.LossModifier;
                        }
                        catch
                        {
                            //
                        }
                    }
                    catch
                    {
                        //
                    }


                    if (win)
                    {
                        usr.Points = usr.Points - points;
                        usr.Wins--;
                        embed.AddField($"{usr.Username} [-{points}]", $"Points: **{usr.Points}**\n" +
                                                                      $"W/L: **[{usr.Wins}/{usr.Losses}]**");
                        embed.Color = Color.Blue;
                    }
                    else
                    {
                        points = Math.Abs(points);
                        usr.Points = usr.Points + points;
                        usr.Losses--;
                        if (usr.Points < 0 && !server.AllowNegativeScore)
                            usr.Points = 0;
                        embed.AddField($"{usr.Username} [+{points}]", $"Points: **{usr.Points}**\n" +
                                                                      $"W/L: **[{usr.Wins}/{usr.Losses}]**");
                        embed.Color = Color.Blue;
                    }
                    try
                    {
                        await UserRename(server.UsernameSelection, user, usr.Username, usr.Points);
                    }
                    catch
                    {
                        //
                    }

                    await CheckRank(server, user, usr);
                }
            }
            embed.Title = gametext != null ? $"Game Undone ({gametext})" : "Game Undone";

            await ReplyAsync("", false, embed.Build());
        }

        /// <summary>
        ///     For given server and users
        ///     if win = true
        ///     add one win and add given points
        ///     if win = false
        ///     add one loss and subtract given points
        /// </summary>
        /// <param name="server"> current server's object</param>
        /// <param name="users"> users to be modified.</param>
        /// <param name="win"> check if the provided users won or lost</param>
        /// <param name="points">default points for the user's to be modified.</param>
        /// <param name="gametext">Game text information</param>
        /// <returns></returns>
        public async Task WinLossPoints(Servers.Server server, List<IUser> users, bool win, int points,
            string gametext = null)
        {
            var embed = new EmbedBuilder();
            foreach (var user in users)
            {
                var usr = server.UserList.FirstOrDefault(x => x.UserId == user.Id);


                if (usr == null || user.Id != usr.UserId) continue;
                {
                    //checks against possible ranks for each user. 
                    //if the user has a rank that has a different point modifier to the server's one, then modify 
                    //their points according to their rank
                    //if there is no role then ignore this.
                    points = win ? server.Winamount : server.Lossamount;
                    try
                    {
                        var toprole = server.Ranks.Where(x => x.Points <= usr.Points).OrderByDescending(x => x.Points);

                        try
                        {
                            var rank = toprole.First();
                            if (rank.WinModifier != 0 && win)
                                points = rank.WinModifier;
                            else if (rank.LossModifier != 0 && !win)
                                points = rank.LossModifier;
                        }
                        catch
                        {
                            //
                        }
                    }
                    catch
                    {
                        //
                    }


                    if (win)
                    {
                        usr.Points = usr.Points + points;
                        usr.Wins++;
                        embed.AddField($"{usr.Username} WON (+{points})", $"Points: **{usr.Points}**\n" +
                                                                          $"W/L: **[{usr.Wins}/{usr.Losses}]**");
                        embed.Color = Color.Green;
                    }
                    else
                    {
                        points = Math.Abs(points);
                        usr.Points = usr.Points - points;
                        usr.Losses++;
                        if (usr.Points < 0 && !server.AllowNegativeScore)
                            usr.Points = 0;
                        embed.AddField($"{usr.Username} LOST (-{points})", $"Points: **{usr.Points}**\n" +
                                                                           $"W/L: **[{usr.Wins}/{usr.Losses}]**");
                        embed.Color = Color.Red;
                    }
                    try
                    {
                        await UserRename(server.UsernameSelection, user, usr.Username, usr.Points);
                    }
                    catch
                    {
                        //
                    }
                    await CheckRank(server, user, usr);
                }
            }

            if (gametext != null)
                embed.Title = $"Game Decided: {gametext}";

            await ReplyAsync("", false, embed.Build());
        }

        public async Task CheckRank(Servers.Server server, IUser user, Servers.Server.User subject)
        {
            if (server.Ranks.Count == 0)
                return;

            foreach (var role in server.Ranks)
            {
                var u = user as IGuildUser;
                var r = Context.Guild.GetRole(role.RoleId);
                if (u != null && u.RoleIds.Contains(role.RoleId))
                    await u.RemoveRoleAsync(r);
            }
            try
            {
                var toprole = server.Ranks.Where(x => x.Points <= subject.Points).Max(x => x.Points);
                var top = server.Ranks.Where(x => x.Points == toprole);

                try
                {
                    var newrole = Context.Guild.GetRole(top.First().RoleId);
                    await ((IGuildUser) user).AddRoleAsync(newrole);
                }
                catch
                {
                    //role has been deleted
                }
            }
            catch
            {
                // No available roles
            }
        }

        public async Task UserRename(int usernameSelection, IUser user, string username, int userpoints)
        {
            //await UserRename(server.UsernameSelection, u, user.Username, user.Points);
            if (usernameSelection == 1)
            {
                await ((IGuildUser) user).ModifyAsync(x => { x.Nickname = $"{userpoints} ~ {username}"; });

                if (CommandHandler.VerifiedUsers != null)
                    if (CommandHandler.VerifiedUsers.Contains(user.Id))
                        await ((IGuildUser) user).ModifyAsync(x => { x.Nickname = $"👑{userpoints} ~ {username}"; });
            }
            else if (usernameSelection == 2)
            {
                await ((IGuildUser) user).ModifyAsync(x => { x.Nickname = $"[{userpoints}] {username}"; });

                if (CommandHandler.VerifiedUsers != null)
                    if (CommandHandler.VerifiedUsers.Contains(user.Id))
                        await ((IGuildUser) user).ModifyAsync(x => { x.Nickname = $"👑[{userpoints}] {username}"; });
            }
            else if (usernameSelection == 3)
            {
                await ((IGuildUser) user).ModifyAsync(x => { x.Nickname = $"{username}"; });

                if (CommandHandler.VerifiedUsers != null)
                    if (CommandHandler.VerifiedUsers.Contains(user.Id))
                        await ((IGuildUser) user).ModifyAsync(x => { x.Nickname = $"👑{username}"; });
            }
        }
    }
}