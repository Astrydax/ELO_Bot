﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using ELO_Bot.Preconditions;
using ELO_Bot.PreConditions;

namespace ELO_Bot.Commands
{
    /// <summary>
    ///     ensure users are registered before using these commands
    ///     blocks blacklsted commands from being run.
    ///     these commands can only be run within a server.
    /// </summary>
    [CheckRegistered]
    [CheckBlacklist]
    [RequireContext(ContextType.Guild)]
    public class MatchMaking : InteractiveBase
    {
        /// <summary>
        ///     dusplays the current queue.
        /// </summary>
        /// <returns></returns>
        [Ratelimit(1, 10d, Measure.Seconds)]
        [Command("Queue")]
        [Alias("q")]
        [Summary("Queue")]
        [Remarks("Display the current queue")]
        public async Task Queue()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var embed = new EmbedBuilder();
            try
            {
                var lobby = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                //get the current queue

                if (lobby == null)
                {
                    await ReplyAsync("Current channel is not a lobby!");
                    return;
                }

                if (lobby.IsPickingTeams)
                {
                    //if teams are currently being picked, display teams in the queue rather than just the queue.
                    var t1List = "";
                    var t2List = "";
                    var users = "";

                    var cap1 = await ((IGuild) Context.Guild).GetUserAsync(lobby.T1Captain);
                    var cap2 = await ((IGuild) Context.Guild).GetUserAsync(lobby.T2Captain);

                    //create a list of users in team 1 2 and users left
                    foreach (var us in lobby.Team1)
                    {
                        var u = await ((IDiscordClient) Context.Client).GetUserAsync(us);
                        t1List += $"{u.Mention} ";
                    }
                    foreach (var us in lobby.Team2)
                    {
                        var u = await ((IDiscordClient) Context.Client).GetUserAsync(us);
                        t2List += $"{u.Mention} ";
                    }
                    foreach (var us in lobby.Users)
                    {
                        var u = await ((IDiscordClient) Context.Client).GetUserAsync(us);
                        users += $"{u.Mention} ";
                    }
                    embed.AddField("Lobby", $"[{lobby.Team1.Count}/{lobby.UserLimit / 2}]\n" +
                                            $"Team1: {t1List}\n" +
                                            $"Team2: {t2List}\n" +
                                            "\nCaptains: \n" +
                                            $"1: {cap1.Mention}\n" +
                                            $"2: {cap2.Mention}\n" +
                                            $"Players Left: {users}");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }
                if (lobby.Users.Count == 0)
                {
                    //empty lobby
                    embed.AddField($"{Context.Channel.Name} Queue **[0/{lobby.UserLimit}]** #{lobby.Games + 1}",
                        "Empty");
                }
                else
                {
                    //get all users in the current lobby and find their user registrations for the server.
                    var list = "";
                    foreach (var user in lobby.Users)
                    {
                        var subject = server.UserList.FirstOrDefault(x => x.UserId == user);
                        if (subject == null)
                        {
                            await ReplyAsync($"error with {user} profile");
                            return;
                        }
                        list += $"{subject.Username} - {subject.Points}\n";
                        //create a list of usernames and their points
                    }

                    embed.AddField(
                        $"{Context.Channel.Name} Queue **[{lobby.Users.Count}/{lobby.UserLimit}]** #{lobby.Games + 1}",
                        $"{list}");
                }
                embed.AddField("Join/Leave", "`=Join` To Join the queue\n" +
                                             "`=Leave` To Leave the queue\n" +
                                             "`=subfor <@user>` Replace a user");
            }
            catch
            {
                embed.AddField("Error", "The current channel is not a lobby, there is no queue here.");
            }

            await ReplyAsync("", false, embed.Build());
        }

        /// <summary>
        ///     displays information about the current lobby
        /// </summary>
        /// <returns></returns>
        [Ratelimit(1, 10d, Measure.Seconds)]
        [Command("Lobby")]
        [Summary("Lobby")]
        [Remarks("Gamemode Info")]
        public async Task Lobby()
        {
            //creating general info about the current lobby.
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var embed = new EmbedBuilder();
            try
            {
                var lobbyexists = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);

                if (lobbyexists == null)
                {
                    await ReplyAsync("Current channel is not a lobby!");
                    return;
                }

                if (lobbyexists.ChannelGametype == null)
                    lobbyexists.ChannelGametype = "Unknown";
                var PickString = "";
                switch (lobbyexists.PickMode)
                {
                    case Servers.Server.PickModes.CompleteRandom:
                        PickString = "Random";
                        break;
                    case Servers.Server.PickModes.Captains:
                        PickString = "Captains";
                        break;
                    case Servers.Server.PickModes.SortByScore:
                        PickString = "Score Sort";
                        break;
                }

                embed.AddField("Lobby Info", "**Player Limit:**\n" +
                                             $"{lobbyexists.UserLimit}\n" +
                                             "**Game Number:**\n" +
                                             $"{lobbyexists.Games + 1}\n" +
                                             "**Sort Mode:**\n" +
                                             $"{PickString}\n" +
                                             "**Gamemode Description:**\n" +
                                             $"{lobbyexists.ChannelGametype}");
            }
            catch
            {
                embed.AddField("Error", "The current channel is not a lobby, there is no queue here.");
            }

            await ReplyAsync("", false, embed.Build());
        }

        /// <summary>
        ///     team captain command
        ///     Select a player from the queue to join your team.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [Ratelimit(1, 5d, Measure.Seconds)]
        [Command("pick")]
        [Summary("pick <@user>")]
        [Alias("p")]
        [Remarks("Choose a player for your team")]
        public async Task Pick(IUser user)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var embed = new EmbedBuilder();

            Servers.Server.Q lobby;
            try
            {
                lobby = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await ReplyAsync("Current channel is not a lobby!");
                    return;
                }
            }
            catch
            {
                //ensure that the current channel is a lobby
                embed.AddField("ERROR", "Current Channel is not a lobby!");
                await ReplyAsync("", false, embed.Build());
                return;
            }

            if (lobby.UserLimit != lobby.Users.Count && !lobby.IsPickingTeams)
            {
                //if the lobby is not full or teams are not being picked.
                embed.AddField("ERROR", "Lobby is not full!!");
                await ReplyAsync("", false, embed.Build());
                return;
            }
            if (!lobby.Users.Contains(user.Id))
            {
                //make sure the user is actually in the lobby.
                embed.AddField("ERROR", "user is not in lobby/user has already been picked.");
                await ReplyAsync("", false, embed.Build());
                return;
            }


            if (Context.User.Id == lobby.T1Captain)
            {
                //check if team 1 players have been picked yet
                //is team2's player count is greater than or equal to team 1's user count (should always be equal however)
                if (lobby.Team1.Count == 0 || lobby.Team1 == null || lobby.Team2.Count >= lobby.Team1.Count)
                {
                    //make sure that users do not pick the other team's captain
                    if (user.Id == lobby.T2Captain)
                    {
                        embed.AddField("ERROR", "User is a captain!");
                        await ReplyAsync("", false, embed.Build());
                        return;
                    }

                    //for initialiseing teams make sure team2's captain is added to their team
                    //also make sure both captains are added to the correct teams.
                    if (lobby.Team1.Count == 0)
                    {
                        lobby.Team1.Add(Context.User.Id);
                        lobby.Users.Remove(Context.User.Id);
                        lobby.Team2.Add(lobby.T2Captain);
                        lobby.Users.Remove(lobby.T2Captain);
                    }


                    //add the specified user to the team
                    lobby.Team1.Add(user.Id);
                    lobby.Users.Remove(user.Id);

                    if (lobby.Users.Count == 1)
                    {
                        var u = lobby.Users.First();
                        lobby.Team2.Add(u);
                        lobby.Users.Remove(u);
                    }

                    var t1List = "";
                    var t2List = "";
                    var users = "";

                    var cap1 = await ((IGuild) Context.Guild).GetUserAsync(lobby.T2Captain);
                    var cap2 = await ((IGuild) Context.Guild).GetUserAsync(lobby.T1Captain);

                    //create a list of users in team1, team2 and users left.
                    foreach (var us in lobby.Team1)
                    {
                        var u = await ((IDiscordClient) Context.Client).GetUserAsync(us);
                        try
                        {
                            t1List += $"{u.Mention} ";
                        }
                        catch
                        {
                            t1List += $"{server.UserList.FirstOrDefault(x => x.UserId == us)?.Username} ";
                        }
                    }
                    foreach (var us in lobby.Team2)
                    {
                        var u = await ((IDiscordClient) Context.Client).GetUserAsync(us);
                        try
                        {
                            t2List += $"{u.Mention} ";
                        }
                        catch
                        {
                            t2List += $"{server.UserList.FirstOrDefault(x => x.UserId == us)?.Username} ";
                        }
                    }
                    foreach (var us in lobby.Users)
                    {
                        var u = await ((IDiscordClient) Context.Client).GetUserAsync(us);
                        try
                        {
                            users += $"{u.Mention} ";
                        }
                        catch
                        {
                            users += $"{server.UserList.FirstOrDefault(x => x.UserId == us)?.Username} ";
                        }
                    }
                    embed.AddField($"{((IGuildUser) user).Nickname} Added",
                        $"[{lobby.Team1.Count}/{lobby.UserLimit / 2}]\n" +
                        $"Team1: {t1List}\n" +
                        $"Team2: {t2List}\n" +
                        "\nCaptains: \n" +
                        $"1: {cap1.Mention}\n" +
                        $"2: {cap2.Mention}\n" +
                        $"Players Left: {users}");
                    await ReplyAsync("", false, embed.Build());
                    lobby.IsPickingTeams = true;


                    //if teams have both been filled finish the queue
                    if (lobby.Users.Count == 0 || lobby.Users == null)
                        await Teams(server, lobby.Team1, lobby.Team2);
                    return;
                }

                //make sure teams are picked in turns.
                if (lobby.Team1.Count > lobby.Team2.Count)
                {
                    embed.AddField("ERROR", "Team 2's turn to pick.");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }


                embed.AddField("ERROR", "FUCK tell Passive to fix something...");
                await ReplyAsync("", false, embed.Build());
            }
            else if (Context.User.Id == lobby.T2Captain)
            {
                //make sure team one has picked before starting team2
                if (lobby.Team2.Count > lobby.Team1.Count || lobby.Team1.Count == 0 || lobby.Team1 == null)
                {
                    embed.AddField("ERROR", "Team 1's turn to pick.");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }

                //add specified user to team 2
                if (lobby.Team1.Count > lobby.Team2.Count)
                {
                    lobby.Team2.Add(user.Id);
                    lobby.Users.Remove(user.Id);
                    var t1List = "";
                    var t2List = "";
                    var users = "";

                    var cap1 = await ((IGuild) Context.Guild).GetUserAsync(lobby.T1Captain);
                    var cap2 = await ((IGuild) Context.Guild).GetUserAsync(lobby.T2Captain);

                    t1List = string.Join(" ", lobby.Team1.Select(x => Context.Client.GetUser(x) == null ? $"N/A ({x})" : Context.Client.GetUser(x).Mention));
                    t2List = string.Join(" ", lobby.Team2.Select(x => Context.Client.GetUser(x) == null ? $"N/A ({x})" : Context.Client.GetUser(x).Mention));
                    users = string.Join(" ", lobby.Users.Select(x => Context.Client.GetUser(x) == null ? $"N/A ({x})" : Context.Client.GetUser(x).Mention));

                    embed.AddField($"{(user as IGuildUser)?.Nickname} Added",
                        $"[{lobby.Team2.Count}/{lobby.UserLimit / 2}]\n" +
                        $"Team1: {t1List}\n" +
                        $"Team2: {t2List}\n" +
                        "\nCaptains: \n" +
                        $"1: {cap1.Mention}\n" +
                        $"2: {cap2.Mention}\n" +
                        $"Players Left: {users}");


                    await ReplyAsync("", false, embed.Build());
                    lobby.IsPickingTeams = true;

                    if (lobby.Users.Count == 0 || lobby.Users == null)
                        await Teams(server, lobby.Team1, lobby.Team2);
                    return;
                }


                //idk this should never happen.
                embed.AddField("ERROR", "I dont think it's your turn to pick a player.....");
                await ReplyAsync("", false, embed.Build());
            }
            else
            {
                //make sure that only captains can choose players.
                embed.AddField("ERROR", "Not A Captain!");
                await ReplyAsync("", false, embed.Build());
            }
        }

        [Command("pair", RunMode = RunMode.Async)]
        [Summary("pair <@user>")]
        [Remarks("Pair with another player to ensure you both get into the same team")]
        public async Task Pair(IUser user)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var embed = new EmbedBuilder();
            Servers.Server.Q lobby;
            try
            {
                lobby = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    embed.AddField("ERROR", "Current Channel is not a lobby!");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }
            }
            catch
            {
                embed.AddField("ERROR", "Current Channel is not a lobby!");
                await ReplyAsync("", false, embed.Build());
                return;
            }


            if (lobby.NoPairs)
            {
                embed.AddField("ERROR", "Pairing is disabled in this lobby.");
                await ReplyAsync("", false, embed.Build());
                return;
            }

            if (user.Id == Context.User.Id)
            {
                embed.AddField("ERROR", "You cannot pair with yourself.");
                await ReplyAsync("", false, embed.Build());
                return;
            }


            if (server.UserList.Any(x => x.UserId == user.Id))
            {
                if (lobby.Pairs.Any(x =>
                    x.User1 == Context.User.Id || x.User2 == Context.User.Id || x.User1 == user.Id ||
                    x.User2 == user.Id))
                {
                    embed.AddField("ERROR", "User or User(s) are already paired with another player");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }


                await ReplyAsync(
                    $"{user.Mention}, please type `confirm pair` in your next message to confirm this pairing.");
                try
                {
                    var i = 0;
                    while (i < 10)
                    {
                        var next = await NextMessageAsync(false, timeout: TimeSpan.FromMinutes(1));
                        if (next.Content.ToLower().Contains("confirm pair") && next.Author.Id == user.Id)
                        {
                            lobby.Pairs.Add(new Servers.Server.Q.Buddy
                            {
                                User1 = Context.User.Id,
                                User2 = user.Id
                            });
                            embed.AddField("Success!",
                                "You will now be paired with this player for the next game you both join.");

                            await ReplyAsync("", false, embed.Build());
                            break;
                        }
                        i++;
                    }
                }
                catch
                {
                    //throw new Exception(e.ToString());
                }
            }
            else
            {
                embed.AddField("ERROR", "Specified user is not registered");
                await ReplyAsync("", false, embed.Build());
            }
        }

        [Command("pairs", RunMode = RunMode.Async)]
        [Summary("pairs")]
        [Remarks("list all pairs in this lobby")]
        public async Task Pairs()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var embed = new EmbedBuilder();
            Servers.Server.Q lobby;
            try
            {
                lobby = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    embed.AddField("ERROR", "Current Channel is not a lobby!");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }
            }
            catch
            {
                embed.AddField("ERROR", "Current Channel is not a lobby!");
                await ReplyAsync("", false, embed.Build());
                return;
            }


            if (lobby.NoPairs)
            {
                embed.AddField("ERROR", "Pairing is disabled in this lobby.");
                await ReplyAsync("", false, embed.Build());
                return;
            }

            if (lobby.Pairs.Count > 0)
            {
                var pages = new List<string>();
                var pairstring = "";
                foreach (var pair in lobby.Pairs)
                {
                    pairstring +=
                        $"{server.UserList.FirstOrDefault(x => x.UserId == pair.User1)?.Username} + {server.UserList.FirstOrDefault(x => x.UserId == pair.User2)?.Username}\n";

                    var numLines = pairstring.Split('\n').Length;
                    if (numLines > 20)
                    {
                        pages.Add(pairstring);
                        pairstring = "";
                    }
                }

                pages.Add(pairstring);

                var msg = new PaginatedMessage
                {
                    Pages = pages,
                    Title = $"{Context.Channel.Name} Pairs",
                    Color = Color.Green
                };

                await PagedReplyAsync(msg);
            }
            else
            {
                embed.AddField("ERROR", "There are no pairs created in this lobby yet.");
                await ReplyAsync("", false, embed.Build());
            }
        }

        [Command("leavepair", RunMode = RunMode.Async)]
        [Summary("leavepair")]
        [Remarks("Leave a pair")]
        public async Task LeavePair()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var embed = new EmbedBuilder();

            var lobby = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
            if (lobby == null)
            {
                embed.AddField("ERROR", "Current Channel is not a lobby!");
                await ReplyAsync("", false, embed.Build());
                return;
            }

            if (lobby.NoPairs)
            {
                embed.AddField("ERROR", "Pairing is disabled in this lobby.");
                await ReplyAsync("", false, embed.Build());
                return;
            }

            if (lobby.Pairs.Any(x => x.User1 == Context.User.Id || x.User2 == Context.User.Id))
            {
                lobby.Pairs.Remove(lobby.Pairs.First(x => x.User1 == Context.User.Id || x.User2 == Context.User.Id));
                embed.AddField("Success!", "Pair has been removed.");
                await ReplyAsync("", false, embed.Build());
            }
            else
            {
                embed.AddField("ERROR", "you are not paired with anyone yet!");
                await ReplyAsync("", false, embed.Build());
            }
        }


        /// <summary>
        ///     join the current queue
        ///     Command blocked if you are
        ///     1. Banned
        ///     2. Already in another queue and mutliqueueing is disabled.
        /// </summary>
        /// <returns></returns>
        [Ratelimit(1, 10d, Measure.Seconds)]
        [Command("Join", RunMode = RunMode.Async)]
        [Summary("Join")]
        [Alias("j")]
        [Remarks("Join the current queue")]
        public async Task Join()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var embed = new EmbedBuilder();
            Servers.Server.Q lobby;
            try
            {
                lobby = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    embed.AddField("ERROR", "Current Channel is not a lobby!");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }
            }
            catch
            {
                embed.AddField("ERROR", "Current Channel is not a lobby!");
                await ReplyAsync("", false, embed.Build());
                return;
            }

            if (server.Bans.Any(x => x.UserId == Context.User.Id))
            {
                var uban = server.Bans.FirstOrDefault(x => x.UserId == Context.User.Id);

                if (uban != null && DateTime.UtcNow >= uban.Time)
                {
                    server.Bans.Remove(uban);
                }
                else
                {
                    if (uban != null)
                        embed.AddField("ERROR",
                            $"You are currently banned from joining the queue for another {Math.Round((uban.Time - DateTime.UtcNow).TotalMinutes, 0)} minutes");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }
            }

            if (server.BlockMultiQueueing)
                if (server.Queue.Any(x => x.Users.Contains(Context.User.Id)))
                    throw new Exception("MultiQueueing is Disabled for this server");

            //users can only join the queue when teams are not being picked.
            if (lobby.IsPickingTeams)
            {
                embed.AddField("ERROR", "Teams are being picked, you cannot join the queue");
                var emb = await ReplyAsync("", false, embed.Build());
                await Task.Delay(500);
                await Context.Message.DeleteAsync();
                await emb.DeleteAsync();
                return;
            }

            //make sure that the users never reach the userlimit.
            if (lobby.Users.Count < lobby.UserLimit)
            {
                if (!lobby.Users.Contains(Context.User.Id))
                {
                    lobby.Users.Add(Context.User.Id);
                    embed.AddField("Success", $"Added to the queue **[{lobby.Users.Count}/{lobby.UserLimit}]**");
                    await ReplyAsync("", false, embed.Build());
                }
                else
                {
                    embed.AddField("ERROR", "Already in queue.");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }
                if (lobby.Users.Count >= lobby.UserLimit)
                {
                    //if lobby is full increment the game count by 1.
                    lobby.Games++;
                    await FullQueue(server);
                }
            }
        }

        /// <summary>
        ///     replaces a user in the current queue (for use if they are afk etc.)
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [Ratelimit(1, 10d, Measure.Seconds)]
        [Command("subfor", RunMode = RunMode.Async)]
        [Summary("subfor <@user>")]
        [Remarks("replace the given user in the queue")]
        public async Task Sub(IUser user)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var embed = new EmbedBuilder();
            var queue = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
            //get the current lobbies queue.
            if (queue != null)
            {
                if (server.Bans.Any(x => x.UserId == Context.User.Id))
                {
                    var uban = server.Bans.FirstOrDefault(x => x.UserId == Context.User.Id);
                    if (uban != null && DateTime.UtcNow >= uban.Time)
                    {
                        server.Bans.Remove(uban);
                    }
                    else
                    {
                        if (uban != null)
                            embed.AddField("ERROR",
                                $"You are currently banned from joining the queue for another {Math.Round((uban.Time - DateTime.UtcNow).TotalMinutes, 0)} minutes");
                        await ReplyAsync("", false, embed.Build());
                        return;
                    }
                }

                if (queue.Users.Contains(Context.User.Id))
                {
                    embed.AddField("ERROR", "You are already queued");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }

                if (queue.Users.Contains(user.Id))
                {
                    queue.Users.Remove(user.Id);
                    queue.Users.Add(Context.User.Id);
                    embed.AddField("Success", $"{user.Mention} has been replaced by {Context.User.Mention}\n" +
                                              $"**[{queue.Users.Count}/{queue.UserLimit}]**");
                }
                else
                {
                    embed.AddField("ERROR", $"{user.Mention} is not queued\n" +
                                            $"**[{queue.Users.Count}/{queue.UserLimit}]**");
                }


                await ReplyAsync("", false, embed.Build());
            }
            else
            {
                await ReplyAsync("Error: No queue? or something... ask passive idk");
            }
        }

        /// <summary>
        ///     replace a user in the most recent game for the current lobby.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [Ratelimit(1, 10d, Measure.Seconds)]
        [Command("replace", RunMode = RunMode.Async)]
        [Summary("replace <@user>")]
        [Remarks("replace the specified user in the previously chosen game")]
        public async Task Replace(IUser user)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var queue = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);

            if (queue == null)
            {
                await ReplyAsync("ERROR: Current Channel is not a lobby!");
                return;
            }

            var oldgame =
                server.Gamelist.FirstOrDefault(x => x.LobbyId == Context.Channel.Id && x.GameNumber == queue.Games);
            if (oldgame != null)
            {
                //check thay you are not already in the old game.
                if (oldgame.Team1.Contains(Context.User.Id) || oldgame.Team2.Contains(Context.User.Id))
                {
                    await ReplyAsync(
                        "You are already in a team for the previous game. Only users that were'nt in this game can replace others.");
                    return;
                }


                if (oldgame.Team1.Contains(user.Id))
                {
                    //remove specified user and replace with new user.
                    oldgame.Team1.Remove(user.Id);
                    oldgame.Team1.Add(Context.User.Id);

                    await ReplyAsync(
                        $"Game #{oldgame.GameNumber} Team 1: {user.Mention} has been replaced by {Context.User.Mention}");
                }
                if (oldgame.Team2.Contains(user.Id))
                {
                    oldgame.Team2.Remove(user.Id);
                    oldgame.Team2.Add(Context.User.Id);

                    await ReplyAsync(
                        $"Game #{oldgame.GameNumber} Team 2: {user.Mention} has been replaced by {Context.User.Mention}");
                }

                var t1Mention = new List<IUser>();
                var t2Mention = new List<IUser>();

                foreach (var u in oldgame.Team1)
                {
                    var use = await ((IGuild) Context.Guild).GetUserAsync(u);
                    t1Mention.Add(use);
                }
                foreach (var u in oldgame.Team2)
                {
                    var use = await ((IGuild) Context.Guild).GetUserAsync(u);
                    t2Mention.Add(use);
                }
                var announcement = "**__Game Has Been Updated__**\n" +
                                   "**Lobby:** \n" +
                                   $"{Context.Channel.Name} - Match #{oldgame.GameNumber}\n" +
                                   $"**Team 1:** [{string.Join(" ", t1Mention.Select(x => x.Mention).ToList())}]\n" +
                                   $"**Team 2**: [{string.Join(" ", t2Mention.Select(x => x.Mention).ToList())}]\n" +
                                   $"When the game finishes, type `=game {Context.Channel.Name} {oldgame.GameNumber} <team1 or team2>`\n" +
                                   "This will modify each team's points respectively.";

                try
                {
                    var channel = await ((IGuild) Context.Guild).GetChannelAsync(server.AnnouncementsChannel);
                    await ((IMessageChannel) channel).SendMessageAsync(announcement);
                }
                catch
                {
                    await ReplyAsync(announcement);
                }
            }
            else
            {
                await ReplyAsync("Error: No queue? or something... ask passive idk");
            }
        }

        /// <summary>
        ///     leave the current queue
        /// </summary>
        /// <returns></returns>
        [Ratelimit(1, 10d, Measure.Seconds)]
        [Command("Leave", RunMode = RunMode.Async)]
        [Alias("l")]
        [Summary("Leave")]
        [Remarks("Leave the current queue")]
        public async Task Leave()
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            var embed = new EmbedBuilder();
            try
            {
                var queue = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);

                if (queue == null)
                {
                    await ReplyAsync("ERROR: Current Channel is not a lobby!");
                    return;
                }

                if (queue.IsPickingTeams)
                {
                    embed.AddField("ERROR",
                        "Teams are being picked, you cannot leave the queue. You may only be subbed.");
                    await ReplyAsync("", false, embed.Build());
                    return;
                }


                queue.Users.Remove(Context.User.Id);
                embed.AddField("Success", "You have been removed from the queue.\n" +
                                          $"**[{queue.Users.Count}/{queue.UserLimit}]**");

                await ReplyAsync("", false, embed.Build());
            }
            catch
            {
                await ReplyAsync("Not Queued?");
            }
        }

        /// <summary>
        ///     select a random map from the maps list
        /// </summary>
        /// <returns></returns>
        [Command("Map")]
        [Summary("Map")]
        [Remarks("select a random map")]
        public async Task Map()
        {
            var embed = new EmbedBuilder();
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            //load the current server
            var lobby = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);

            if (lobby == null)
            {
                await ReplyAsync("ERROR: Current Channel is not a lobby!");
                return;
            }

            if (lobby.Maps.Count == 0 || lobby.Maps == null)
            {
                await ReplyAsync("There are no maps setup for this lobby");
                return;
            }


            var r = new Random().Next(0, lobby.Maps.Count);
            embed.AddField("Random Map", $"{lobby.Maps[r]}");

            await ReplyAsync("", false, embed.Build());
        }

        /// <summary>
        ///     list all maps for the current lobby
        /// </summary>
        /// <returns></returns>
        [Ratelimit(1, 10d, Measure.Seconds)]
        [Command("Maps")]
        [Summary("Maps")]
        [Remarks("List Maps")]
        public async Task Maps()
        {
            var embed = new EmbedBuilder();
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            //load the current server
            var lobby = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
            //try to output the current lobby
            if (lobby == null)
            {
                await ReplyAsync("ERROR: Current Channel is not a lobby!");
                return;
            }

            foreach (var map in lobby.Maps)
                embed.Description += $"{map}\n";
            //adds each map in the list to the embed

            await ReplyAsync("", false, embed.Build());
        }

        [Command("showgame", RunMode = RunMode.Async)]
        [Summary("showgame <lobby> <match no.>")]
        [Remarks("Show information about a previous game")]
        public async Task Showgame(string lobbyname, int matchnumber)
        {
            try
            {
                var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
                var lobby = Context.Guild.TextChannels.FirstOrDefault(x =>
                    x.Name.ToLower() == lobbyname.ToLower());
                if (lobby == null)
                    throw new Exception("Invalid Lobbyname");

                var game = server.Gamelist.FirstOrDefault(x => x.LobbyId == lobby.Id && x.GameNumber == matchnumber);
                if (game == null)
                    throw new Exception("Invalid Gamenumber");

                var embed = new EmbedBuilder();
                var gstring = "";

                if (game.Cancelled)
                {
                    gstring = "Cancelled";
                }
                else
                {
                     if (game.Result == null)
                        gstring = "Undecided";
                    else if (game.Result is true)
                        gstring = "Team1";
                    else if (game.Result is false)
                        gstring = "Team2";                   
                }



                var team1 = server.UserList.Where(x => game.Team1.Contains(x.UserId)).Select(x => x.Username);
                var team2 = server.UserList.Where(x => game.Team2.Contains(x.UserId)).Select(x => x.Username);


                embed.Title = $"=game {lobbyname} {matchnumber}";
                embed.AddField("Game Result", $"{gstring}\n\n");
                embed.AddField("Team1", $"{string.Join("\n", team1)}");
                embed.AddField("Team2", $"{string.Join("\n", team2)}");
                embed.Color = Color.Purple;

                await ReplyAsync("", false, embed.Build());
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
        }

        /// <summary>
        ///     Announce the currrent game.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="team1"></param>
        /// <param name="team2"></param>
        /// <returns></returns>
        public async Task Teams(Servers.Server server, List<ulong> team1, List<ulong> team2)
        {
            var team1Userlist = new List<IUser>();
            var t1 = "";
            foreach (var user in team1)
            {
                var u = await ((IGuild) Context.Guild).GetUserAsync(user);
                team1Userlist.Add(u);
                t1 += $"{u.Mention} ";
            }

            var team2Userlist = new List<IUser>();
            var t2 = "";
            foreach (var user in team2)
            {
                var u = await ((IGuild) Context.Guild).GetUserAsync(user);
                team2Userlist.Add(u);
                t2 += $"{u.Mention} ";
            }

            var currentqueue = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);

            if (currentqueue == null)
            {
                await ReplyAsync("ERROR: Current Channel is not a lobby!");
                return;
            }

            var host = await ((IGuild) Context.Guild).GetUserAsync(currentqueue.T1Captain);


            if (currentqueue.Maps.Count > 0 && currentqueue.Maps != null)
            {
                var rnd = new Random().Next(0, currentqueue.Maps.Count);
                await ReplyAsync("**GAME ON**\n" +
                                 $"Team1: {t1}\n" +
                                 $"Team2: {t2}\n\n" +
                                 $"Random Map: {currentqueue.Maps[rnd]}");
            }
            else
            {
                await ReplyAsync("**GAME ON**\n" +
                                 $"Team1: {t1}\n" +
                                 $"Team2: {t2}");
            }


            currentqueue.Users = new List<ulong>();
            currentqueue.Team1 = new List<ulong>();
            currentqueue.Team2 = new List<ulong>();
            currentqueue.T1Captain = 0;
            currentqueue.T2Captain = 0;
            currentqueue.IsPickingTeams = false;

            var newgame = new Servers.Server.PreviouMatches
            {
                GameNumber = currentqueue.Games,
                LobbyId = Context.Channel.Id,
                Team1 = team1,
                Team2 = team2
            };
            server.Gamelist.Add(newgame);

            await Announce(currentqueue, host, currentqueue.ChannelGametype, team1Userlist, team2Userlist);
        }

        public static (IEnumerable<T> first, IEnumerable<T> second) Split<T>(IEnumerable<T> source, Func<T, int> selector)
        {
            var e = source.Select(item => (Item: item, Value: selector(item)))
                .OrderByDescending(item => item.Value);
            var a = new List<T>();
            var b = new List<T>();
            foreach (var (item, _) in e)
            {
                if (a.Sum(selector) < b.Sum(selector))
                    a.Add(item);
                else
                    b.Add(item);
            }
            return (a, b);
        }

        /// <summary>
        ///     if a queue is full, check if captains and go into picking mode
        ///     otherwise, auto assign teams.
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        public async Task FullQueue(Servers.Server server)
        {
            var embed = new EmbedBuilder();
            var currentqueue = server.Queue.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);

            if (currentqueue == null)
            {
                await ReplyAsync("ERROR: Current Channel is not a lobby!");
                return;
            }

            // list of user profiles based on those in the current queue.
            var userlist = currentqueue.Users.Select(user => server.UserList.FirstOrDefault(x => x.UserId == user))
                .ToList();

            //order list by User Points
            if (currentqueue.PickMode == Servers.Server.PickModes.Captains)
            {
                //randomly select the captains on each team.
                var rnd = new Random();

                IUser cap1;
                IUser cap2;
                var capranks = new List<ulong>();
                foreach (var user in currentqueue.Users)
                {
                    var u = userlist.FirstOrDefault(x => x.UserId == user);
                    if (u != null && u.Points > 5)
                        capranks.Add(user);
                }

                if (capranks.Count >= 2)
                {
                    var cap = Enumerable.Range(0, capranks.Count).OrderBy(x => rnd.Next()).Take(2)
                        .ToList();
                    cap1 = await ((IGuild) Context.Guild).GetUserAsync(capranks[cap[0]]);
                    cap2 = await ((IGuild) Context.Guild).GetUserAsync(capranks[cap[1]]);
                }
                else
                {
                    var captains = Enumerable.Range(0, currentqueue.Users.Count).OrderBy(x => rnd.Next()).Take(2)
                        .ToList();
                    cap1 = await ((IGuild) Context.Guild).GetUserAsync(currentqueue.Users[captains[0]]);
                    cap2 = await ((IGuild) Context.Guild).GetUserAsync(currentqueue.Users[captains[1]]);
                }


                var players = "";
                foreach (var user in currentqueue.Users)
                {
                    var u = await ((IGuild) Context.Guild).GetUserAsync(user);
                    if (u != cap1 && u != cap2)
                        players += $"{u.Mention} ";
                }

                await ReplyAsync($"**Team 1 Captain:** {cap1.Mention}\n" +
                                 $"**Team 2 Captain:** {cap2.Mention}\n" +
                                 "**Select Your Teams using `=pick <@user>`**\n" +
                                 "**Captain 1 Always Picks First**\n" +
                                 "**Players:**\n" +
                                 $"{players}");
                //make sure that all players are mentioned to notify them that a game has begun

                currentqueue.T1Captain = cap1.Id;
                currentqueue.T2Captain = cap2.Id;
                currentqueue.Team1 = new List<ulong>();
                currentqueue.Team2 = new List<ulong>();
                currentqueue.IsPickingTeams = true;
                return;
            }

            var sortedlist = userlist.OrderByDescending(x => x.Points).ToList();
                var team1 = new List<Servers.Server.User>();
                var team2 = new List<Servers.Server.User>();
            if (currentqueue.PickMode == Servers.Server.PickModes.SortByScore)
            {
                //automatically select teams evenly based on points
                if (currentqueue.Pairs.Any(
                        x => currentqueue.Users.Contains(x.User1) && currentqueue.Users.Contains(x.User2)) &&
                    !currentqueue.NoPairs)
                {
                    var validpairs = currentqueue.Pairs.Where(x =>
                        currentqueue.Users.Contains(x.User1) && currentqueue.Users.Contains(x.User2));
                    foreach (var pair in validpairs)
                        if (team1.Count > team2.Count)
                        {
                            team2.Add(server.UserList.First(x => x.UserId == pair.User1));
                            team2.Add(server.UserList.First(x => x.UserId == pair.User2));
                        }
                        else
                        {
                            team1.Add(server.UserList.First(x => x.UserId == pair.User1));
                            team1.Add(server.UserList.First(x => x.UserId == pair.User2));
                        }
                    foreach (var user in currentqueue.Users)
                        if (team1.Any(x => x.UserId == user) || team2.Any(x => x.UserId == user))
                        {
                            //
                        }
                        else
                        {
                            if (team1.Count > team2.Count)
                                team2.Add(server.UserList.First(x => x.UserId == user));
                            else
                                team1.Add(server.UserList.First(x => x.UserId == user));
                        }

                    if (team1.Count > team2.Count)
                    {
                        team2.Add(team1.Last());
                        team1.Remove(team1.Last());
                    }
                    else if (team2.Count > team1.Count)
                    {
                        team1.Add(team2.Last());
                        team2.Remove(team2.Last());
                    }
                }
                else
                {
                    if (sortedlist.Count == 10)
                    {
                        team1.Add(sortedlist[0]);
                        team1.Add(sortedlist[3]);
                        team1.Add(sortedlist[5]);
                        team1.Add(sortedlist[7]);
                        team1.Add(sortedlist[9]);

                        team2.Add(sortedlist[1]);
                        team2.Add(sortedlist[2]);
                        team2.Add(sortedlist[4]);
                        team2.Add(sortedlist[6]);
                        team2.Add(sortedlist[8]);
                    }
                    else
                    {
                        foreach (var user in sortedlist)
                            if (team1.Count > team2.Count)
                                team2.Add(user);
                            else
                                team1.Add(user);
                    }
                }
            }

            if (currentqueue.PickMode == Servers.Server.PickModes.CompleteRandom)
            {
                sortedlist = userlist.OrderBy(x => new Random().Next()).Reverse().ToList();
                foreach (var user in sortedlist)
                    if (team1.Count > team2.Count)
                        team2.Add(user);
                    else
                        team1.Add(user);
            }



            //creating the info for each team
            var t1Desc = "";
            var t1Sum = team1.Sum(x => x.Points);
            var t1Users = new List<IUser>();
            foreach (var user in team1)
            {
                t1Desc += $"{user.Username} - {user.Points}\n";
                t1Users.Add(((IGuild) Context.Guild).GetUserAsync(user.UserId).Result);
            }

            var t2Desc = "";
            var t2Sum = team2.Sum(x => x.Points);
            var t2Users = new List<IUser>();
            foreach (var user in team2)
            {
                t2Desc += $"{user.Username} - {user.Points}\n";
                t2Users.Add(((IGuild) Context.Guild).GetUserAsync(user.UserId).Result);
            }

            var random = new Random().Next(0, sortedlist.Count);
            var gamehost = await ((IGuild) Context.Guild).GetUserAsync(sortedlist[random].UserId);

            embed.Title = $"{Context.Channel.Name} Match #{currentqueue.Games}";
            embed.AddField($"Random Host", $"{gamehost.Mention}");
            embed.AddField($"Team 1 - {t1Sum}", $"{t1Desc}");
            embed.AddField($"Team 2 - {t2Sum}", $"{t2Desc}");
            embed.WithFooter(x =>
            {
                x.Text = $"{DateTime.UtcNow} || Game: {Context.Channel.Name} {currentqueue.Games}";
            });


            if (currentqueue.ChannelGametype == null)
                currentqueue.ChannelGametype = "Unknown";
            embed.AddField("Match Info", $"{currentqueue.ChannelGametype}");


            string randmap;
            try
            {
                var r = new Random().Next(0, currentqueue.Maps.Count);
                randmap = currentqueue.Maps[r];
                embed.AddField("Random Map", $"{randmap}");
            }
            catch
            {
                randmap = null;
            }


            await ReplyAsync("", false, embed.Build());
            currentqueue.Users = new List<ulong>();
            var newgame = new Servers.Server.PreviouMatches
            {
                GameNumber = currentqueue.Games,
                LobbyId = Context.Channel.Id,
                Team1 = t1Users.Select(x => x.Id).ToList(),
                Team2 = t2Users.Select(x => x.Id).ToList()
            };
            server.Gamelist.Add(newgame);


            await Announce(currentqueue, gamehost, currentqueue.ChannelGametype, t1Users, t2Users, randmap);
        }

        /// <summary>
        ///     announce the current game!
        /// </summary>
        /// <param name="lobby"></param>
        /// <param name="gamehost"></param>
        /// <param name="matchdescription"></param>
        /// <param name="team1"></param>
        /// <param name="team2"></param>
        /// <param name="randommap"></param>
        /// <returns></returns>
        public async Task Announce(Servers.Server.Q lobby, IGuildUser gamehost, string matchdescription,
            List<IUser> team1,
            List<IUser> team2, string randommap = null)
        {
            var server = Servers.ServerList.First(x => x.ServerId == Context.Guild.Id);
            IMessageChannel channel;
            try
            {
                channel =
                    await ((IGuild) Context.Guild).GetChannelAsync(server.AnnouncementsChannel) as IMessageChannel;
            }
            catch
            {
                channel = Context.Channel;
            }
            var lobbychannel = Context.Guild.GetChannel(lobby.ChannelId);

            var cap1 = $"[{lobby.T1Captain}]";
            var cap2 = $"[{lobby.T2Captain}]";

            try
            {
                var c1 = await ((IGuild) Context.Guild).GetUserAsync(lobby.T1Captain);
                cap1 = $"[{c1.Mention}]";
            }
            catch
            {
                //
            }

            try
            {
                var c2 = await ((IGuild) Context.Guild).GetUserAsync(lobby.T2Captain);
                cap2 = $"[{c2.Mention}]";
            }
            catch
            {
                //
            }


            var embed = new EmbedBuilder
            {
                Title = "Game Has Started",
                Url = $"{Config.Load().DiscordInvite}"
            };
            embed.AddField("Info", "**Lobby:** \n" +
                                   $"{lobbychannel.Name} - Match #{lobby.Games}\n" +
                                   "**Selected Host:**\n" +
                                   $"{gamehost.Mention}");
            if (cap1 == "[0]" || cap2 == "[0]")
            {
                embed.AddField("Team1", $"{string.Join(" ", team1.Select(x => x.Mention))}");
                embed.AddField("Team2", $"{string.Join(" ", team2.Select(x => x.Mention))}");
            }
            else
            {
                embed.AddField("Team1", $"{cap1}\n" +
                                        $"{string.Join(" ", team1.Select(x => x.Mention))}");
                embed.AddField("Team2", $"{cap2}\n" +
                                        $"{string.Join(" ", team2.Select(x => x.Mention))}");
            }


            embed.WithFooter(x => { x.Text = $"{DateTime.UtcNow} || Game: {lobbychannel.Name} {lobby.Games}"; });

            if (randommap != null)
                try
                {
                    embed.AddField("Random Map", $"{randommap}");
                }
                catch
                {
                    //
                }

            embed.Color = Color.Blue;


            var announcement = "**__Game Has Started__**\n" +
                               "**Lobby:** \n" +
                               $"{lobbychannel.Name} - Match #{lobby.Games}\n" +
                               "**Selected Host:** \n" +
                               $"{gamehost.Mention}\n" +
                               "**Match Settings:**\n" +
                               $"{matchdescription}\n" +
                               $"**Team 1:** [{string.Join(" ", team1.Select(x => x.Mention))}]\n" +
                               $"**Team 2**: [{string.Join(" ", team2.Select(x => x.Mention))}]\n" +
                               $"When the game finishes, type `=game {lobbychannel.Name} {lobby.Games} <team1 or team2>`\n" +
                               "This will modify each team's points respectively.";

            foreach (var user in team1)
                try
                {
                    await user.SendMessageAsync("", false, embed.Build());
                }
                catch
                {
                    //
                }

            foreach (var user in team2)
                try
                {
                    await user.SendMessageAsync("", false, embed.Build());
                }
                catch
                {
                    //
                }

            try
            {
                if (channel != null)
                    try
                    {
                        await channel.SendMessageAsync(
                            $"{string.Join(" ", team1.Select(x => x.Mention))} {string.Join(" ", team2.Select(x => x.Mention))}",
                            false, embed.Build());
                    }
                    catch
                    {
                        await channel.SendMessageAsync(announcement);
                    }
            }
            catch
            {
                try
                {
                    await Context.Channel.SendMessageAsync(
                        $"{string.Join(" ", team1.Select(x => x.Mention))} {string.Join(" ", team2.Select(x => x.Mention))}",
                        false, embed.Build());
                }
                catch
                {
                    await Context.Channel.SendMessageAsync(announcement);
                }
            }
        }
    }
}