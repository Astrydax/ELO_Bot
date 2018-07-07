﻿namespace ELO.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using global::Discord;
    using global::Discord.Commands;

    using ELO.Discord.Context;
    using ELO.Discord.Extensions;
    using ELO.Discord.Preconditions;
    using ELO.Models;

    using global::Discord.WebSocket;

    using Raven.Client.Documents.Linq.Indexing;

    [CustomPermissions]
    [CheckLobby]
    [CheckRegistered]
    public class MatchMaking : Base
    {
        [Command("Join")]
        [Alias("j")]
        [Summary("Join the current lobby's queue")]
        public async Task JoinLobbyAsync()
        {
            if (!Context.Elo.Lobby.Game.QueuedPlayerIDs.Contains(Context.User.Id))
            {
                if (Context.Server.Settings.GameSettings.BlockMultiQueuing)
                {
                    if (Context.Server.Lobbies.Any(x => x.Game.QueuedPlayerIDs.Contains(Context.User.Id)) || Context.Server.Lobbies.Any(x => x.Game.Team1.Players.Contains(Context.User.Id)) || Context.Server.Lobbies.Any(x => x.Game.Team2.Players.Contains(Context.User.Id)))
                    {
                        throw new Exception("MultiQueuing is disabled by the server Admins");
                    }
                }

                if (Context.Elo.User.Banned.Banned)
                {
                    throw new Exception($"You are banned from matchmaking for another {(Context.Elo.User.Banned.ExpiryTime - DateTime.UtcNow).TotalMinutes}");
                }

                if (Context.Elo.Lobby.Game.IsPickingTeams)
                {
                    throw new Exception("Currently Picking teams. Please wait until this is completed");
                }

                var previous = Context.Server.Results.Where(x => x.LobbyID == Context.Elo.Lobby.ChannelID && (x.Team1.Contains(Context.User.Id) || x.Team2.Contains(Context.User.Id))).OrderByDescending(x => x.Time).FirstOrDefault();
                if (previous != null && previous.Time + Context.Server.Settings.GameSettings.ReQueueDelay > DateTime.UtcNow)
                {
                    if (previous.Result == GuildModel.GameResult._Result.Undecided)
                    {
                        throw new Exception($"You must wait another {(previous.Time + Context.Server.Settings.GameSettings.ReQueueDelay - DateTime.UtcNow).TotalMinutes} minutes before rejoining the queue");
                    }
                }

                Context.Elo.Lobby.Game.QueuedPlayerIDs.Add(Context.User.Id);
                Context.Server.Save();
                await SimpleEmbedAsync($"Success, Added {Context.User.Mention} to queue, [{Context.Elo.Lobby.Game.QueuedPlayerIDs.Count}/{Context.Elo.Lobby.UserLimit}]");
                if (Context.Elo.Lobby.UserLimit >= Context.Elo.Lobby.Game.QueuedPlayerIDs.Count)
                {
                    //Game is ready to be played
                    await FullGame.FullQueueAsync(Context);
                }
            }
        }

        [Command("Leave")]
        [Alias("l")]
        [Summary("Leave the current lobby's queue")]
        public async Task LeaveLobbyAsync()
        {
            if (Context.Elo.Lobby.Game.QueuedPlayerIDs.Contains(Context.User.Id))
            {
                if (Context.Elo.Lobby.Game.IsPickingTeams)
                {
                    throw new Exception("Currently Picking teams. Please wait until this is completed");
                }

                Context.Elo.Lobby.Game.QueuedPlayerIDs.Remove(Context.User.Id);
                await SimpleEmbedAsync($"Success, Removed {Context.User.Mention} from queue, [{Context.Elo.Lobby.Game.QueuedPlayerIDs.Count}/{Context.Elo.Lobby.UserLimit}]");
                Context.Server.Save();
            }
        }

        [Command("Queue")]
        [Alias("q")]
        [Summary("View the current lobby's queue")]
        public async Task QueueAsync()
        {
            var queuedPlayers = Context.Elo.Lobby.Game.QueuedPlayerIDs.Select(p => Context.Guild.GetUser(p)).Where(x => x != null).ToList();

            if (Context.Elo.Lobby.Game.QueuedPlayerIDs.Count != queuedPlayers.Count)
            {
                Context.Elo.Lobby.Game.QueuedPlayerIDs = queuedPlayers.Select(x => x.Id).ToList();
                Context.Server.Save();
            }

            if (!Context.Elo.Lobby.Game.IsPickingTeams)
            {
                await SimpleEmbedAsync($"**Player List [{Context.Elo.Lobby.Game.QueuedPlayerIDs.Count}/{Context.Elo.Lobby.UserLimit}]**\n" +
                                       $"{string.Join("\n", queuedPlayers.Select(x => x.Mention))}");
            }
            else
            {
                await SimpleEmbedAsync($"**Team1 Captain** {Context.Guild.GetUser(Context.Elo.Lobby.Game.Team1.Captain)?.Mention}\n" +
                                       $"**Team1:** {string.Join(", ", Context.Elo.Lobby.Game.Team1.Players.Select(x => Context.Guild.GetUser(x)?.Mention).ToList())}\n" +
                                       $"**Team2 Captain** {Context.Guild.GetUser(Context.Elo.Lobby.Game.Team2.Captain)?.Mention}\n" +
                                       $"**Team2:** {string.Join(", ", Context.Elo.Lobby.Game.Team2.Players.Select(x => Context.Guild.GetUser(x)?.Mention).ToList())}\n" +
                                       $"**Select Your Teams using `{Context.Prefix}pick <@user>`**\n" +
                                       $"**It is Captain {(Context.Elo.Lobby.Game.Team1.TurnToPick ? 1 : 2)}'s Turn to pick**\n" +
                                       "**Player Pool**\n" +
                                       $"{string.Join(" ", Context.Elo.Lobby.Game.QueuedPlayerIDs.Select(x => Context.Guild.GetUser(x)?.Mention))}");
            }
        }

        [Command("Lobby")]
        [Summary("View information about the current lobby")]
        public Task LobbyInfoAsync()
        {
            return SimpleEmbedAsync($"**{Context.Channel.Name}**\n" +
                                    $"`Players Per team:` {Context.Elo.Lobby.UserLimit / 2}\n" +
                                    $"`Total Players:` {Context.Elo.Lobby.UserLimit}\n" +
                                    $"`Sort Mode:` {(Context.Elo.Lobby.PickMode == GuildModel.Lobby._PickMode.Captains ? $"Captains => {Context.Elo.Lobby.CaptainSortMode}" : $"{Context.Elo.Lobby.PickMode}")}\n" +
                                    $"`Game Number:` {Context.Elo.Lobby.GamesPlayed + 1}\n" +
                                    $"`Host Pick mode:` {Context.Elo.Lobby.HostSelectionMode}\n" +
                                    $"`Channel:` {Context.Channel.Name}\n" +
                                    "Description:\n" +
                                    $"{Context.Elo.Lobby.Description}");
        }

        [Command("Replace")]
        [Summary("Replace a user in the current queue")]
        public Task ReplaceAsync(SocketGuildUser user)
        {
            if (Context.Elo.Lobby.Game.QueuedPlayerIDs.Contains(Context.User.Id))
            {
                throw new Exception("You cannot replace a user if you are in the queue yourself");
            }

            if (!Context.Elo.Lobby.Game.QueuedPlayerIDs.Contains(user.Id))
            {
                throw new Exception("User is not queued.");
            }

            Context.Elo.Lobby.Game.QueuedPlayerIDs.Remove(user.Id);
            return JoinLobbyAsync();
        }

        [Command("Pick")]
        [Alias("p")]
        [Summary("Pick a player for your team")]
        [Remarks("Must pick a player that is in the queue and isn't already on a team\nYou must be the captain of a team to run this command")]
        public async Task PickUserAsync(IGuildUser pickedUser)
        {
            if (!Context.Elo.Lobby.Game.IsPickingTeams)
            {
                throw new Exception("Lobby is not picking teams at the moment.");
            }

            if (Context.Elo.Lobby.Game.Team1.Captain != Context.User.Id && Context.Elo.Lobby.Game.Team2.Captain != Context.User.Id)
            {
                throw new Exception($"{Context.User.Mention} is not a captain");
            }

            if (!Context.Elo.Lobby.Game.QueuedPlayerIDs.Contains(pickedUser.Id))
            {
                throw new Exception($"{pickedUser.Mention} is not able to be picked");
            }

            int nextTeam;
            if (Context.Elo.Lobby.Game.Team1.TurnToPick)
            {
                if (Context.User.Id != Context.Elo.Lobby.Game.Team1.Captain)
                {
                    throw new Exception("It is not your turn to pick.");
                }

                Context.Elo.Lobby.Game.Team1.Players.Add(pickedUser.Id);
                nextTeam = 2;
                Context.Elo.Lobby.Game.Team2.TurnToPick = true;
                Context.Elo.Lobby.Game.Team1.TurnToPick = false;
            }
            else
            {
                if (Context.User.Id != Context.Elo.Lobby.Game.Team2.Captain)
                {
                    throw new Exception("It is not your turn to pick.");
                }

                Context.Elo.Lobby.Game.Team2.Players.Add(pickedUser.Id);
                nextTeam = 1;
                Context.Elo.Lobby.Game.Team2.TurnToPick = false;
                Context.Elo.Lobby.Game.Team1.TurnToPick = true;
            }

            Context.Elo.Lobby.Game.QueuedPlayerIDs.Remove(pickedUser.Id);

            if (Context.Elo.Lobby.Game.QueuedPlayerIDs.Count == 1)
            {
                var lastPlayer = Context.Elo.Lobby.Game.QueuedPlayerIDs.FirstOrDefault();
                if (Context.Elo.Lobby.Game.Team1.TurnToPick)
                {
                    Context.Elo.Lobby.Game.Team1.Players.Add(lastPlayer);
                }
                else
                {
                    Context.Elo.Lobby.Game.Team2.Players.Add(lastPlayer);
                }

                Context.Elo.Lobby.Game.QueuedPlayerIDs.Remove(lastPlayer);
            }

            if (Context.Elo.Lobby.Game.QueuedPlayerIDs.Count == 0)
            {
                Context.Elo.Lobby.GamesPlayed++;

                /*
                await ReplyAsync("**Game has Started**\n" +
                                 $"Team1: {string.Join(", ", Context.Elo.Lobby.Game.Team1.Players.Select(x => Context.Guild.GetUser(x)?.Mention).ToList())}\n" +
                                 $"Team2: {string.Join(", ", Context.Elo.Lobby.Game.Team2.Players.Select(x => Context.Guild.GetUser(x)?.Mention).ToList())}\n" +
                                 $"**Game #{Context.Elo.Lobby.GamesPlayed}**");
                                 */
                Context.Server.Results.Add(new GuildModel.GameResult
                {
                    Comments = new List<GuildModel.GameResult.Comment>(),
                    GameNumber = Context.Elo.Lobby.GamesPlayed,
                    LobbyID = Context.Elo.Lobby.ChannelID,
                    Result = GuildModel.GameResult._Result.Undecided,
                    Team1 = Context.Elo.Lobby.Game.Team1.Players,
                    Team2 = Context.Elo.Lobby.Game.Team2.Players,
                    Time = DateTime.UtcNow
                });
                await FullGame.AnnounceGameAsync(Context);
                Context.Elo.Lobby.Game = new GuildModel.Lobby.CurrentGame();
            }
            else
            {
                await SimpleEmbedAsync($"**Team1 Captain** {Context.Guild.GetUser(Context.Elo.Lobby.Game.Team1.Captain)?.Mention}\n" +
                                       $"**Team1:** {string.Join(", ", Context.Elo.Lobby.Game.Team1.Players.Select(x => Context.Guild.GetUser(x)?.Mention).ToList())}\n\n" +
                                       $"**Team2 Captain** {Context.Guild.GetUser(Context.Elo.Lobby.Game.Team2.Captain)?.Mention}\n" +
                                       $"**Team2:** {string.Join(", ", Context.Elo.Lobby.Game.Team2.Players.Select(x => Context.Guild.GetUser(x)?.Mention).ToList())}\n\n" +
                                       $"**Select Your Teams using `{Context.Prefix}pick <@user>`**\n" +
                                       $"**It is Captain {nextTeam}'s Turn to pick**\n\n" +
                                       "**Player Pool**\n" +
                                       $"{string.Join(" ", Context.Elo.Lobby.Game.QueuedPlayerIDs.Select(x => Context.Guild.GetUser(x)?.Mention))}");
            }

            Context.Server.Save();
        }

        [Command("GameResult")]
        [Summary("Vote for the result of a game")]
        public async Task GameResultAsync(ITextChannel channel, int gameNumber, GuildModel.GameResult._Result result)
        {
            var selectedGame = Context.Server.Results.FirstOrDefault(x => x.LobbyID == channel.Id && x.GameNumber == gameNumber);
            if (selectedGame == null)
            {
                throw new Exception("Game Unavailable. Incorrect Data.");
            }

            if (selectedGame.Result != GuildModel.GameResult._Result.Undecided)
            {
                throw new Exception("Game must be undecided to submit player chosen result.");
            }

            if (result == GuildModel.GameResult._Result.Undecided)
            {
                throw new Exception("You cannot set the result to undecided");
            }

            if (selectedGame.Team1.Contains(Context.User.Id))
            {
                if (selectedGame.Proposal.P1 == 0)
                {
                    selectedGame.Proposal.P1 = Context.User.Id;
                    selectedGame.Proposal.R1 = result;
                }
                else
                {
                    throw new Exception("A player has already submitted a result from this team.");
                }
            }
            else if (selectedGame.Team2.Contains(Context.User.Id))
            {
                if (selectedGame.Proposal.P2 == 0)
                {
                    selectedGame.Proposal.P2 = Context.User.Id;
                    selectedGame.Proposal.R2 = result;
                }
                else
                {
                    throw new Exception("A player has already submitted a result from this team.");
                }
            }
            else
            {
                throw new Exception("You must be on either team to submit the game result.");
            }

            Context.Server.Save();
            await SimpleEmbedAsync("Result Proposal\n" +
                                   $"Team1 Submission: {selectedGame.Proposal.R1.ToString()} Player: {Context.Guild.GetUser(selectedGame.Proposal.P1)?.Mention ?? "N/A"}\n" +
                                   $"Team2 Submission: {selectedGame.Proposal.R2.ToString()} Player: {Context.Guild.GetUser(selectedGame.Proposal.P2)?.Mention ?? "N/A"}");

            if (selectedGame.Proposal.R1 == GuildModel.GameResult._Result.Undecided || selectedGame.Proposal.R2 == GuildModel.GameResult._Result.Undecided)
            {
                return;
            }

            if (selectedGame.Proposal.R1 == selectedGame.Proposal.R2)
            {
                await GameManagement.GameResultAsync(Context, selectedGame, result);
            }
            else
            {
                throw new Exception("Mismatched Game Result Proposals. Please allow an admin to manually submit a result");
            }
        }

        [CheckLobby]
        [Command("ClearProposedResult")]
        [Summary("Clear the result of a proposal")]
        public Task ClearGResAsync(ITextChannel channel, int gameNumber)
        {
            var selectedGame = Context.Server.Results.FirstOrDefault(x => x.LobbyID == channel.Id && x.GameNumber == gameNumber);
            if (selectedGame == null)
            {
                throw new Exception("Game Unavailable. Incorrect Data.");
            }

            selectedGame.Proposal = new GuildModel.GameResult.ResultProposal();
            Context.Server.Save();
            return ReplyAsync("Reset.");
        }

        [CheckLobby]
        [Command("Maps")]
        [Summary("Show a list of all maps for the current lobby")]
        public Task MapsAsync()
        {
            return SimpleEmbedAsync($"{string.Join("\n", Context.Elo.Lobby.Maps)}");
        }

        [CheckLobby]
        [Command("Map")]
        [Summary("select a random map for the lobby")]
        public Task MapAsync()
        {
            if (Context.Elo.Lobby.Maps.Any())
            {
                var r = new Random();
                return SimpleEmbedAsync($"{Context.Elo.Lobby.Maps.OrderByDescending(m => r.Next()).FirstOrDefault()}");
            }

            return SimpleEmbedAsync("There are no maps set in this lobby");
        }
    }
}