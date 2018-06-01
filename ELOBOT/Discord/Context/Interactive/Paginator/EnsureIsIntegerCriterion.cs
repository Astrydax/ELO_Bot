﻿using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using ELOBOT.Discord.Context.Interactive.Criteria;

namespace ELOBOT.Discord.Context.Interactive.Paginator
{
    internal class EnsureIsIntegerCriterion : ICriterion<SocketMessage>
    {
        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, SocketMessage parameter)
        {
            var ok = int.TryParse(parameter.Content, out _);
            return Task.FromResult(ok);
        }
    }
}