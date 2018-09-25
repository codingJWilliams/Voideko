using System;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Impl;
using NLog;
using Octokit;

namespace NadekoBot.Modules.Gambling
{
    public class VoidAdditions
    {
        private static ICurrencyService _cs;
        private static ILogger _log;
        private static DiscordSocketClient _client;
        private static DbService _db;
        
        public VoidAdditions(ICurrencyService currency, DiscordSocketClient client, DbService db, ILogger log)
        {
            _cs = currency;
            _log = log;
            _client = client;
            _db = db;
            
            var ws = new WebServer(SendResponse, "http://localhost:9000/api/");
            ws.Run();
            
            // Add listener for user joining
            client.UserJoined += async (user) =>
            {
                // Ensure they from correct guild
                if (user.Guild.Id.ToString() != NadekoBot.CurrencyOnJoinGuild) return;

                // Set their bal to correct value
                await SetBal(user, NadekoBot.CurrencyOnJoin, "Initial Currency");
                
                log.Info("Processed new user: " + user.Id);
            };
        }
        
        private static async Task Award(IUser user, long amount, string reason = "NadekoConnector Award", 
            bool sendMessage = false)
        {
            await _cs.AddAsync(user, reason, amount, sendMessage);
        }

        private static async Task Take(IUser user, long amount, string reason = "NadekoConnector Take",
            bool sendMessage = false)
        {
            await _cs.RemoveAsync(user, reason, amount, sendMessage);
        }

        private static long GetBal(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                var duser = uow.DiscordUsers.GetOrCreate(user);
                uow.Complete();
                
                return duser.CurrencyAmount;
            }
        }

        private static async Task SetBal(IUser user, long amount, string reason = "NadekoConnector SetBal", bool sendMessage = false)
        {
            var userBal = GetBal(user);
            var difference = amount - userBal;
            // If they already have the intended amount, don't bother
            if (difference == 0) return;
            
            // If the difference is greater than 0 we have to Award, else we have to Take
            if (difference > 0)
            {
                await Award(user, difference, reason, sendMessage);
            }
            else
            {
                await Take(user, difference, reason, sendMessage);
            }            
        }
        
        // HTTP request handler
        private static string SendResponse(HttpListenerRequest request)
        {
            var method = request.QueryString.Get("method");
            switch (method)
            {
                case "getbal":
                {
                    IUser user = _client.GetUser(ulong.Parse(request.QueryString.Get("id")));
                    var bal = GetBal(user);
                    return bal.ToString();
                }
                case "setbal":
                {
                    IUser user = _client.GetUser(ulong.Parse(request.QueryString.Get("id")));
                    var reason = request.QueryString.Get("reason") ?? "NadekoConnector SetBal";                
                    var sendMessage = request.QueryString.Get("sendMessage") != null && bool.Parse(request.QueryString.Get("sendMessage"));
                    var amount = long.Parse(request.QueryString.Get("amount"));

                    SetBal(user, amount, reason, sendMessage);
                    return "ok";
                }
                case "award":
                {
                    IUser user = _client.GetUser(ulong.Parse(request.QueryString.Get("id")));
                    var reason = request.QueryString.Get("reason") ?? "NadekoConnector SetBal";                
                    var sendMessage = request.QueryString.Get("sendMessage") != null && bool.Parse(request.QueryString.Get("sendMessage"));
                    var amount = long.Parse(request.QueryString.Get("amount"));

                    Award(user, amount, reason, sendMessage);
                    return "ok";
                }
                case "take":
                {
                    IUser user = _client.GetUser(ulong.Parse(request.QueryString.Get("id")));
                    var reason = request.QueryString.Get("reason") ?? "NadekoConnector SetBal";                
                    var sendMessage = request.QueryString.Get("sendMessage") != null && bool.Parse(request.QueryString.Get("sendMessage"));
                    var amount = long.Parse(request.QueryString.Get("amount"));

                    Take(user, amount, reason, sendMessage);
                    return "ok";
                }
                default:
                    return "error";
            }
        }
    }
}