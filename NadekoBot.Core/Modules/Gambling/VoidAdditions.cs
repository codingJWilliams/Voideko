using System;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NadekoBot.Core.Services;
using NLog;

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
            
            
            WebServer ws = new WebServer(SendResponse, "http://localhost:9000/api/");
            ws.Run();
            
            // Add listener for user joining
            client.UserJoined += (user) =>
            {
                // Get user from the DB if they exist, else create a new blank record for dem
                var duser = db.UnitOfWork.DiscordUsers.GetOrCreate(user);
                log.Info("Created / retrieved user " + user.Id.ToString() + " in/from DB");
                var amount = 25000 - duser.CurrencyAmount;
                if (amount > 0)
                {
                    log.Info("Awarded " + amount.ToString() + " to user");
                    currency.AddAsync(user, "Initial Currency", amount);
                }
                else
                {
                    log.Info("Nicked " + amount.ToString() + " from user");
                    currency.RemoveAsync(user, "Initial Currency", 0 - amount);
                }
                return Task.CompletedTask;
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
            var duser = _db.UnitOfWork.DiscordUsers.GetOrCreate(user);
            return duser.CurrencyAmount;
        }

        private static async Task SetBal(IUser user, long amount, string reason = "NadekoConnector SetBal", bool sendMessage = false)
        {
            long userBal = GetBal(user);
            long difference = amount - userBal;
            if (difference == 0) return;
            if (difference > 0)
            {
                Award(user, difference, reason, sendMessage);
            }
            else
            {
                Take(user, difference, reason, sendMessage);
            }            
        }
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

                    SetBal(user, amount, reason, sendMessage).RunSynchronously();
                    return "ok";
                }
                case "award":
                {
                    IUser user = _client.GetUser(ulong.Parse(request.QueryString.Get("id")));
                    var reason = request.QueryString.Get("reason") ?? "NadekoConnector SetBal";                
                    var sendMessage = request.QueryString.Get("sendMessage") != null && bool.Parse(request.QueryString.Get("sendMessage"));
                    var amount = long.Parse(request.QueryString.Get("amount"));

                    Award(user, amount, reason, sendMessage).RunSynchronously();
                    return "ok";
                }
                case "take":
                {
                    IUser user = _client.GetUser(ulong.Parse(request.QueryString.Get("id")));
                    var reason = request.QueryString.Get("reason") ?? "NadekoConnector SetBal";                
                    var sendMessage = request.QueryString.Get("sendMessage") != null && bool.Parse(request.QueryString.Get("sendMessage"));
                    var amount = long.Parse(request.QueryString.Get("amount"));

                    Take(user, amount, reason, sendMessage).RunSynchronously();
                    return "ok";
                }
                default:
                    return "error";
            }
        }
    }
}