using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PollBot {

    internal class Program {
        private static Config cfg;
        private static DB db;
        private static TelegramBotClient botClient;

        private static void Main(string[] _) {
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
            cfg = deserializer.Deserialize<Config>(input: System.IO.File.ReadAllText("config.yaml"));
            db = new DB(cfg.Database);
            botClient = new TelegramBotClient(token: cfg.TelegramToken);
            var me = botClient.GetMeAsync().Result;
            Console.WriteLine($"UserID {me.Id} NAME: {me.FirstName}.");
            botClient.StartReceiving(allowedUpdates: new UpdateType[] { UpdateType.Message, UpdateType.CallbackQuery });
            botClient.OnMessage += BotClient_OnMessage;
            botClient.OnCallbackQuery += BotClient_OnCallbackQuery;
            while (true) {
                Thread.Sleep(millisecondsTimeout: int.MaxValue);
            }
        }

        private static async void BotClient_OnMessage(object sender, MessageEventArgs e) {
            if (e.Message.Text != null) {
                if (cfg.DebugMode)
                    Console.WriteLine(ObjectDumper.Dump(e.Message));
                var text = e.Message.Text;
                var user = e.Message.From;
                if (text.StartsWith("/poll") || text.StartsWith("/multi_poll")) {
                    HandleCreate(chat_id: e.Message.Chat.Id, user: user, text: e.Message.Text, msg: e.Message.MessageId);
                } else if (text == "/help") {
                    await botClient.SendTextMessageAsync(e.Message.Chat.Id, cfg.translation.Help, replyToMessageId: e.Message.MessageId);
                } else if (text == "/stats") {
                    HandleStat(chat_id: e.Message.Chat.Id, e.Message.MessageId);
                }
            }
        }

        private static async void BotClient_OnCallbackQuery(object sender, CallbackQueryEventArgs e) {
            const string _approve = "approve ";
            var query = e.CallbackQuery;
            if (!cfg.Admins.Contains(query.From.Id)) {
                await botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.PermissionError);
                return;
            }
            if (query.Data.StartsWith(_approve)) {
                var hash = int.Parse(query.Data.Remove(0, _approve.Length));
                var origin = query.Message.ReplyToMessage;
                var text = origin.Text;
                var shash = text.GetHashCode();
                if (hash != shash) {
                    await Task.WhenAll(
                        botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.HashMisMatchError),
                        botClient.DeleteMessageAsync(cfg.MainChatId, query.Message.MessageId),
                        SendRequest(origin.MessageId, cfg.translation.HashMisMatchError, shash));
                    return;
                }
                VerifyMessage(text, out var firstline, out var opts);
                await SendPoll(origin.From, firstline, opts);
                if (cfg.DeleteOrigin)
                    await botClient.DeleteMessageAsync(cfg.MainChatId, origin.MessageId);
                await Task.WhenAll(
                    botClient.DeleteMessageAsync(cfg.MainChatId, query.Message.MessageId),
                    botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.Approved));
            } else {
                await Task.WhenAll(
                    botClient.SendTextMessageAsync(cfg.MainChatId, cfg.translation.RejectError),
                    botClient.DeleteMessageAsync(cfg.MainChatId, query.Message.MessageId),
                    botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.Rejected));
            }
        }

        private static async void HandleCreate(long chat_id, User user, string text, int msg) {
            var direct_send = cfg.Admins.Contains(user.Id) && cfg.DirectSend;
            if (chat_id != cfg.MainChatId && direct_send) {
                await botClient.SendTextMessageAsync(chat_id, cfg.translation.DisallowError);
                return;
            }
            if (!VerifyMessage(text, out var firstline, out var opts)) {
                await botClient.SendTextMessageAsync(chat_id, cfg.translation.FormatError);
                return;
            }
            if (direct_send) {
                await SendPoll(user, firstline, opts);
            } else {
                await SendRequest(msg, cfg.translation.Reply, text.GetHashCode());
            }
        }

        private static async Task SendRequest(int msg, string content, int hash) => await botClient.SendTextMessageAsync(cfg.MainChatId, content,
                disableWebPagePreview: true,
                replyToMessageId: msg,
                replyMarkup: new InlineKeyboardMarkup(
                    new InlineKeyboardButton[] {
                            InlineKeyboardButton.WithCallbackData(cfg.translation.Approve, $"approve {hash}"),
                            InlineKeyboardButton.WithCallbackData(cfg.translation.Reject, "reject") }));

        private static async Task SendPoll(User user, string firstline, IEnumerable<string> opts) {
            const string _poll = "/poll ";
            const string _mpoll = "/multi_poll ";
            var multi = false;
            string title;
            if (firstline.StartsWith(_poll)) {
                title = firstline.Remove(0, _poll.Length);
            } else if (firstline.StartsWith(_mpoll)) {
                title = firstline.Remove(0, _mpoll.Length);
                multi = true;
            } else {
                Console.WriteLine($"Unexcepted request: {firstline}");
                return;
            }
            var msg = await botClient.SendPollAsync(cfg.SendChatId, $"{title} by {user.FirstName}", opts, allowsMultipleAnswers: multi, isAnonymous: true);
            db.AddLog(user.Id, user.Username, user.FirstName, user.LastName, title, msg.MessageId);
        }

        private static bool VerifyMessage(string data, out string firstline, out IEnumerable<string> opts) {
            var sp = data.Split("\n");
            firstline = sp[0].Trim();
            if (sp.Length > 2 && firstline.Split(' ', 2).Length == 2) {
                opts = sp.Skip(1).Select(x => x.Trim());
                return true;
            }
            firstline = null;
            opts = null;
            return false;
        }

        private static async void HandleStat(long chat_id, int msg_id) {
            var log = cfg.translation.Stats + "\n";
            foreach (var entry in db.StatLog()) {
                log += $"{entry.Count} {entry.GetName()}\n";
            }
            await botClient.SendTextMessageAsync(chat_id, log, replyToMessageId: msg_id);
        }
    }
}