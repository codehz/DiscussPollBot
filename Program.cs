using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        private static string botname;

        private static readonly Regex DIVIDER_REGEX = new Regex(@"^-{3,}$", RegexOptions.Compiled);

        private const int MAX_QUESTION_LENGTH = 255;
        private const int MAX_OPTION_LENGTH = 100;
        private const int MAX_OPTIONS = 10;
        private const int MIN_OPTIONS = 2;

        private static void Main(string[] _) {
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
            cfg = deserializer.Deserialize<Config>(input: System.IO.File.ReadAllText("config.yaml"));
            db = new DB(cfg.Database);
            botClient = new TelegramBotClient(token: cfg.TelegramToken);
            var me = botClient.GetMeAsync().Result;
            botname = me.Username;
            Console.WriteLine($"UserID {me.Id} NAME: {me.Username}.");
            cfg.Admins = botClient.GetChatAdministratorsAsync(cfg.MainChatId).Result.Select(x => x.User.Id);
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
                if (text.StartsWith("/poll") || text.StartsWith("/mpoll")) {
                    HandleCreate(chat_id: e.Message.Chat.Id, message: e.Message, text: e.Message.Text, msg: e.Message.MessageId);
                } else if (text == "/help" || text == $"/help@{botname}") {
                    await botClient.SendTextMessageAsync(e.Message.Chat.Id, cfg.translation.Help, replyToMessageId: e.Message.MessageId);
                } else if (text == "/stats" || text == $"/stats@{botname}") {
                    HandleStat(chat_id: e.Message.Chat.Id, e.Message.MessageId);
                } else if (text == "/refresh_admin" || text == $"/refresh_admin@{botname}") {
                    cfg.Admins = (await botClient.GetChatAdministratorsAsync(cfg.MainChatId)).Select(x => x.User.Id);
                } else if (text == "/dup" || text == $"/dup@{botname}") {
                    HandleDuplicate(msg: e.Message);
                }
            }
        }

        private static async void HandleDuplicate(Message msg) {
            if ((ChatId) msg.Chat.Id != cfg.MainChatId) {
                await botClient.SendTextMessageAsync(msg.Chat.Id, cfg.translation.DisallowError);
                return;
            }
            if (cfg.DeleteOrigin)
                await botClient.DeleteMessageAsync(cfg.MainChatId, msg.MessageId);
            var rep = msg.ReplyToMessage;
            if (rep == null) {
                await botClient.SendTextMessageAsync(cfg.MainChatId, cfg.translation.NoReplyError);
                return;
            }
            if (rep.Poll == null) {
                await botClient.SendTextMessageAsync(cfg.MainChatId, cfg.translation.NotPollError);
                return;
            }
            if (!rep.Poll.IsClosed) {
                await botClient.SendTextMessageAsync(cfg.MainChatId, cfg.translation.NotClosedError);
                return;
            }
            if (rep.Poll.Type == "quiz") {
                await botClient.SendTextMessageAsync(cfg.MainChatId, cfg.translation.NotQuizError);
                return;
            }
            await botClient.SendTextMessageAsync(cfg.MainChatId, cfg.translation.Duplicate,
                parseMode: ParseMode.Html,
                disableWebPagePreview: true,
                disableNotification: true,
                replyToMessageId: rep.MessageId,
                replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton[] {
                    InlineKeyboardButton.WithCallbackData(cfg.translation.Approve, "duplicate"),
                    InlineKeyboardButton.WithCallbackData(cfg.translation.Reject, "reject") }));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        private static async void BotClient_OnCallbackQuery(object sender, CallbackQueryEventArgs e) {
            const string _approve = "approve ";
            const string _duplicate = "duplicate";
            var query = e.CallbackQuery;
            try {
                if (query.Data.StartsWith(_approve)) { // Update / approve
                    var hash = int.Parse(query.Data.Remove(0, _approve.Length));
                    var origin = query.Message.ReplyToMessage;
                    var text = origin.Text;
                    var shash = text.GetHashCode();

                    // Check permission
                    if (hash != shash) { // edited, update
                        // check admin / author permission
                        if (!cfg.Admins.Contains(query.From.Id) && query.From.Id != query.Message.From.Id) {
                            await botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.PermissionWithAuthorError);
                            return;
                        }
                    } else { // not edited, approve
                        // check admin permission
                        if (!cfg.Admins.Contains(query.From.Id)) {
                            await botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.PermissionError);
                            return;
                        }
                    }
                    var error = VerifyMessage(text, origin, out var firstline, out var opts, out var multi);
                    if (error != null) {
                        await botClient.DeleteMessageAsync(cfg.MainChatId, query.Message.MessageId);
                        await botClient.SendTextMessageAsync(origin.Chat.Id, error);
                        return;
                    }
                    if (hash != shash) { // message edited
                        await Task.WhenAll(
                            botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.HashMisMatchError),
                            botClient.DeleteMessageAsync(cfg.MainChatId, query.Message.MessageId),
                            SendRequest(firstline, opts, origin.MessageId, shash, multi));
                        return;
                    }
                    await SendPoll(origin.From, firstline, opts, multi);
                    await Task.WhenAll(
                        botClient.DeleteMessageAsync(cfg.MainChatId, query.Message.MessageId),
                        botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.Approved));
                    if (cfg.DeleteOrigin)
                        await botClient.DeleteMessageAsync(cfg.MainChatId, origin.MessageId);
                } else if (query.Data == _duplicate) { // Duplicate
                    // check admin permission
                    if (!cfg.Admins.Contains(query.From.Id)) {
                        await botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.PermissionError);
                        return;
                    }

                    var origin = query.Message.ReplyToMessage;
                    await DuplicatePoll(origin);
                    await Task.WhenAll(
                        botClient.DeleteMessageAsync(cfg.MainChatId, query.Message.MessageId),
                        botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.Approved));
                    if (cfg.DeleteOrigin)
                        await botClient.DeleteMessageAsync(cfg.MainChatId, origin.MessageId);
                } else { // Reject
                    // check admin / author permission
                    if (!cfg.Admins.Contains(query.From.Id) && query.From.Id != query.Message.From.Id) {
                        await botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.PermissionWithAuthorError);
                        return;
                    }

                    // Reject
                    await Task.WhenAll(
                        botClient.SendTextMessageAsync(cfg.MainChatId, cfg.translation.RejectError, replyToMessageId: query.Message.ReplyToMessage.MessageId),
                        botClient.DeleteMessageAsync(cfg.MainChatId, query.Message.MessageId),
                        botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.Rejected));
                }
            } catch (Exception ex) {
                Console.WriteLine(ex);
                try {
                    await botClient.DeleteMessageAsync(query.Message.Chat.Id, query.Message.MessageId);
                } catch { }
                try {
                    await botClient.AnswerCallbackQueryAsync(query.Id, cfg.translation.ExceptionError);
                    await botClient.SendTextMessageAsync(cfg.MainChatId, cfg.translation.ExceptionError);
                } catch { }
            }
        }

        private static async void HandleCreate(ChatId chat_id, Message message, string text, int msg) {
            try {
                var direct_send = cfg.Admins.Contains(message.From.Id) && cfg.DirectSend;
                if (chat_id != cfg.MainChatId && !direct_send) {
                    await botClient.SendTextMessageAsync(chat_id, cfg.translation.DisallowError);
                    return;
                }
                string error = VerifyMessage(text, message, out var firstline, out var opts, out var multi);
                if (error != null) {
                    await botClient.SendTextMessageAsync(chat_id, error);
                    return;
                }
                if (direct_send) {
                    await SendPoll(message.From, firstline, opts, multi);
                } else {
                    await SendRequest(firstline, opts, msg, text.GetHashCode(), multi);
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.StackTrace);
                await botClient.SendTextMessageAsync(chat_id, cfg.translation.ExceptionError);
                return;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0018:Inline variable declaration", Justification = "<Pending>")]
        private static async Task SendRequest(string question, IEnumerable<string> opts, int msg, int hash, bool multi) {
            await botClient.SendPollAsync(cfg.MainChatId, question, opts,
                allowsMultipleAnswers: multi,
                isAnonymous: true,
                replyToMessageId: msg,
                isClosed: true,
                replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton[] {
                    InlineKeyboardButton.WithCallbackData(cfg.translation.Approve, $"approve {hash}"),
                    InlineKeyboardButton.WithCallbackData(cfg.translation.Reject, "reject") }));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0018:Inline variable declaration", Justification = "<Pending>")]
        private static async Task SendPoll(User user, string question, IEnumerable<string> opts, bool multi) {
            var msg = await botClient.SendPollAsync(cfg.SendChatId, question, opts, allowsMultipleAnswers: multi, isAnonymous: true);
            db.AddLog(user.Id, user.Username, user.FirstName, user.LastName, question, msg.MessageId);
        }

        private static async Task DuplicatePoll(Message origin) {
            var poll = origin.Poll;
            var user = origin.From;
            string authorSuffix = BuildAuthorSuffix(origin);
            var msg = await botClient.SendPollAsync(cfg.MainChatId, $"{poll.Question} {authorSuffix}",
                options: poll.Options.Select(op => op.Text),
                allowsMultipleAnswers: poll.AllowsMultipleAnswers,
                isAnonymous: true);
            db.AddLog(user.Id, user.Username, user.FirstName, user.LastName, poll.Question, msg.MessageId);
        }

        private static string VerifyMessage(string data, Message message, out string firstline, out IEnumerable<string> opts, out Boolean multi) {
            // Default values;
            firstline = null;
            opts = null;
            multi = false;

            // Poll types
            if (firstline.TryRemovePrefix("/poll ", out data) || firstline.TryRemovePrefix($"/poll@{botname} ", out data)) {
                multi = false;
            } else if (firstline.TryRemovePrefix("/mpoll ", out data) || firstline.TryRemovePrefix($"/mpoll@{botname} ", out data)) {
                multi = true;
            } else {
                Console.WriteLine($"Unexcepted request: {firstline}");
                return cfg.translation.Help;
            }

            // data preparation
            var lines = data.Split("\n").ToList();
            string question = null;
            IEnumerable<string> options = null;
            string authorSuffix = " " + BuildAuthorSuffix(message);
            int authorSuffixLength = authorSuffix.UTF16Length();
            int i = 0;
            while (i < lines.Count) {
                if (DIVIDER_REGEX.Match(lines[i]).Success) {
                    question = string.Join("\n", lines.Take(i)).Trim();
                    options = lines.TakeLast(lines.Count - i - 1).Select(x => x.Trim());
                    break;
                }
                i++;
            }
            if (question == null) {
                question = lines[0].Trim();
                options = lines.TakeLast(lines.Count - 1).Select(x => x.Trim());
            }
            if (question.UTF16Length() > MAX_QUESTION_LENGTH - authorSuffixLength) {
                return String.Format(
                        cfg.translation.QuestionTooLongError,
                        MAX_QUESTION_LENGTH - authorSuffixLength, question.UTF16Length()
                    );
            }
            question += authorSuffix;
            if (options.Count() < MIN_OPTIONS || options.Count() > MAX_OPTIONS) {
                return String.Format(
                        cfg.translation.WrongOptionSizeError,
                        MIN_OPTIONS, MAX_OPTIONS, options.Count()
                    );
            }
            foreach (string option in options) {
                if (option.UTF16Length() > MAX_OPTION_LENGTH) {
                    return String.Format(
                            cfg.translation.OptionTooLongError,
                            MAX_OPTION_LENGTH, option.UTF16Length(), option
                        );
                }
            }
            firstline = question;
            opts = options;
            return null;
        }

        private static string BuildAuthorSuffix(Message message) {
            if (!string.IsNullOrWhiteSpace(message.ForwardSenderName)) {
                return message.ForwardSenderName;
            }
            string firstName, lastName;
            if (message.ForwardFromChat != null) {
                firstName = message.ForwardFromChat.FirstName;
                lastName = message.ForwardFromChat.LastName;
            } else {
                User user;
                if (message.ForwardFrom != null) {
                    user = message.ForwardFrom;
                } else {
                    user = message.From;
                }
                firstName = user.FirstName;
                lastName = user.LastName;
            }
            string authorSuffix = $"by {firstName}";
            if (string.IsNullOrWhiteSpace(lastName)) {
                authorSuffix += " " + lastName;
            }
            return authorSuffix;
        }

        private static async void HandleStat(long chat_id, int msg_id) {
            var log = cfg.translation.Stats + "\n";
            foreach (var entry in db.StatLog()) {
                log += $"<b>{entry.Count}</b>: {entry.Name}\n";
            }
            await botClient.SendTextMessageAsync(chat_id, log, replyToMessageId: msg_id, disableWebPagePreview: true, parseMode: ParseMode.Html);
        }
    }
}