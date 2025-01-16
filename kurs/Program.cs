using Telegram.Bot;

using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using static System.Net.Mime.MediaTypeNames;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Telegram.Bot.Types.ReplyMarkups;
using kurs;
using System.Linq;
using Npgsql;
using System.Threading;
class Program
{
    static Dictionary<long, string> userStates = new Dictionary<long, string>();

    static TelegramBotClient bot;
    static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    [Obsolete]
    static void Main()
    {
        
        bot = new TelegramBotClient("8067636563:AAEdws3nOejSVK2N-hrTm0P0sCxBmj8ILRI");
        ApplicationContext db = new ApplicationContext();
        Console.WriteLine("Запущен бот " + bot.GetMeAsync().Result.FirstName);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { }
        };
        bot.StartReceiving(
             HandleUpdateAsync,
             HandleErrorAsync,
             receiverOptions
         );

        // Запускаем задачу проверки уведомлений
        Task.Run(() => NotificationChecker(db, cancellationTokenSource.Token));





        bool tr = true;
        while (tr)
        {
            string te = Console.ReadLine();
            ;
            string result = string.Join("", db.GetTaskDate(te));
           
            string txt = Console.ReadLine();
            if (txt == "exit")
            {
                tr = false;
            }
            else
            {
                Console.WriteLine("Неизвестная команда");
            }
        }
       
        DateTime currentMonth;
        

        static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
        {
            var callbackQuery = update.CallbackQuery;
            var message = update.Message;

            if (message == null)
            {
                if (callbackQuery != null)
                {
                    await HandleCallbackQueryAsync(bot, callbackQuery, cancellationToken);
                }
                return;
            }
            var chatId = message.Chat.Id;
            var userId = message.From.Id;

            ApplicationContext db = new ApplicationContext();
            string username = message.From?.Username ?? "UnknownUser";
            string tableName = $"{username}_{userId}";
            Console.WriteLine(tableName);

            if (update.Type == UpdateType.Message && message.Text != null)
            {
                if (userStates.TryGetValue(userId, out string currentState))
            {
                if (currentState.StartsWith("SelectedDate:"))
                {
                    string[] stateParts = currentState.Split('|');
                    string dateString = stateParts[0].Split(':')[1];
                    string taskText = stateParts.Length > 1 ? stateParts[1] : "Нет задачи"; 

                    if (TimeSpan.TryParse(message.Text, out TimeSpan selectedTime))
                    {
                        string fullDateTime = $"{dateString} {selectedTime}";
                        Console.WriteLine($"Формируем дату и время: {fullDateTime}");

                        db.AddUserMessage(tableName, taskText, fullDateTime);

                        await bot.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"Задача добавлена на {fullDateTime}.",
                            cancellationToken: cancellationToken
                        );
                            await bot.SendTextMessageAsync(
                       chatId: chatId,
                       text: "Доступные команды:",
                       replyMarkup: MainInlineKeyboard(),
                       cancellationToken: cancellationToken
                   );
                            userStates.Remove(userId); // Удаляем состояние
                    }

                    else
                    {
                        await bot.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Неверный формат времени. Попробуйте еще раз (например, 14:30).",
                            cancellationToken: cancellationToken
                        );
                    }
                    return;
                }
                else if (currentState == "AddingTask")
                {
                    // Сохраняем текст задачи
                    string taskText = message.Text;

                    userStates[userId] = $"SelectedDate:{DateTime.UtcNow:yyyy.MM.dd}|{taskText}";
                    Console.WriteLine($"Текст задачи сохранен: {taskText}");

                    await bot.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Выберите дату:",
                        replyMarkup: InlineCalendarFactory.GetKeyboard(DateTime.UtcNow, editingMessageId: 0),
                        cancellationToken: cancellationToken
                    );
                    return;
                }
            }   

                if (message.Text == "/start")
                {
                    Task.Run(() => db.CreateDynamicTabl(tableName));
                    await bot.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Привет, это бот-планировщик. Вот список доступных команд:",
                        replyMarkup: MainInlineKeyboard(),
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    await bot.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Я не понимаю эту команду(доступные команды: /start).",
                        cancellationToken: cancellationToken
                    );
                }
            }

            else if (callbackQuery != null)
            {
                await HandleCallbackQueryAsync(bot, callbackQuery, cancellationToken);
            }
        }

        static async Task HandleCallbackQueryAsync(ITelegramBotClient bot, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            if (callbackQuery.Data == "AddTask")
            {
                long userId = callbackQuery.From.Id;

                userStates[userId] = "AddingTask";

                await bot.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: "Введите задачу:",
                    cancellationToken: cancellationToken
                );
            }
            else if (callbackQuery.Data == "DostTasks")
            {
                ApplicationContext db = new ApplicationContext();
                string username = callbackQuery.From.Username;
                long userId = callbackQuery.From.Id;
                string tableName = $"{username}_{userId}";


                await bot.EditMessageReplyMarkup(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId,
                    replyMarkup: GetInlineKeyboard2(db.GetTaskTxt(tableName), db.GetTaskDate(tableName))
                );
            }
            else if (callbackQuery.Data.StartsWith("previous_month:"))
            {
                DateTime currentMonth = GetDateFromCallback(callbackQuery.Data);
                DateTime previousMonth = currentMonth.AddMonths(-1);

                await bot.EditMessageReplyMarkupAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId,
                    replyMarkup: InlineCalendarFactory.GetKeyboard(previousMonth, callbackQuery.Message.MessageId),
                    cancellationToken: cancellationToken
                );
            }
            else if (callbackQuery.Data.StartsWith("next_month:"))
            {
                DateTime currentMonth = GetDateFromCallback(callbackQuery.Data);
                DateTime nextMonth = currentMonth.AddMonths(1);

                await bot.EditMessageReplyMarkupAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId,
                    replyMarkup: InlineCalendarFactory.GetKeyboard(nextMonth, callbackQuery.Message.MessageId),
                    cancellationToken: cancellationToken
                );
            }
            else if (callbackQuery.Data.StartsWith("date:"))
            {
                long userId = callbackQuery.From.Id;

                DateTime selectedDate = GetDateFromCallback(callbackQuery.Data);
                string formattedDate = selectedDate.ToString("yyyy.MM.dd");

                if (userStates.TryGetValue(userId, out string currentState))
                {
                    if (currentState.StartsWith("SelectedDate:"))
                    {
                        string[] stateParts = currentState.Split('|');
                        string taskText = stateParts.Length > 1 ? stateParts[1] : "Нет задачи";

                        userStates[userId] = $"SelectedDate:{formattedDate}|{taskText}";
                    }
                }
                else
                {
                    userStates[userId] = $"SelectedDate:{formattedDate}";
                }

                await bot.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: $"Вы выбрали дату: {formattedDate}. Введите время задачи (например, 14:30):",
                    cancellationToken: cancellationToken
                );
            }

            else if (callbackQuery.Data == "MainHome")
            {
                await bot.EditMessageReplyMarkup(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId,
                    replyMarkup: MainInlineKeyboard()
                );
            }
        }

        static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }
    }
    private static DateTime GetDateFromCallback(string callbackData)
    {
        string dateString = callbackData.Split(':')[1];
        int year = int.Parse(dateString.Substring(0, 4));
        int month = int.Parse(dateString.Substring(4, 2));
        int day = int.Parse(dateString.Substring(6, 2));

        return new DateTime(year, month, day);
    }
    private static InlineKeyboardMarkup MainInlineKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]{
                InlineKeyboardButton.WithCallbackData("Доступные задачи", "DostTasks"),
                InlineKeyboardButton.WithCallbackData("Добавить задачу", "AddTask")
            }
        });
    }

    private static InlineKeyboardMarkup GetInlineKeyboard2(List<string> tasks, List<string> TaskDate)
    {
        var buttons = new List<List<InlineKeyboardButton>>();

        // Заголовок списка
        buttons.Add(new List<InlineKeyboardButton>
    {
        InlineKeyboardButton.WithCallbackData("Список доступных задач:", "MainHome")
    });

        for (int i = 0; i < tasks.Count; i++)
        {
            string task = tasks[i];
            string date = TaskDate[i];

            // Урезаем текст для отображения
            string displayText = task.Length > 24 ? task.Substring(0, 24) + "..." : task;

            string callbackData = $"task_{i}";

            buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData($"{displayText} ({date})", callbackData)
        });
        }

        // Дополнительные кнопки
        buttons.Add(new List<InlineKeyboardButton>
    {
        InlineKeyboardButton.WithCallbackData("Главное меню", "MainHome"),
    });

        buttons.Add(new List<InlineKeyboardButton>
    {
        InlineKeyboardButton.WithCallbackData("Назад", "action_prev"),
        InlineKeyboardButton.WithCallbackData("Вперед", "action_next")
    });

        return new InlineKeyboardMarkup(buttons);
    }


    static async Task NotificationChecker(ApplicationContext db, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                DateTime utcNow = DateTime.UtcNow;
                TimeZoneInfo ekaterinburgTimeZone = TimeZoneInfo.CreateCustomTimeZone("Ekaterinburg Standard Time", TimeSpan.FromHours(5), "Ekaterinburg Time", "Ekaterinburg Time");
                DateTime ekaterinburgNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, ekaterinburgTimeZone);

                // Используйте ekaterinburgNow для проверки задач
                foreach (var tableName in db.GetUserTables())
                {
                    if (tableName == "info") { continue; }
                    else
                    {
                        var tasks = db.GetTasksForNotification(tableName, ekaterinburgNow);
                        foreach (var task in tasks)
                        {
                            long userId = task.UserId;
                            string taskText = task.TaskText;

                            // Отправка уведомления
                            await bot.SendTextMessageAsync(
                                chatId: userId,
                                text: $"Напоминание! Задача: {taskText}",
                                cancellationToken: token
                            );

                            // Удаляем задачу или помечаем её как выполненную
                            db.MarkTaskAsNotified(tableName, task.TaskId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в проверке уведомлений: {ex.Message}");
            }

            // Ждём 1 минуту перед повторной проверкой
            await Task.Delay(TimeSpan.FromMinutes(1), token);
        }
    }


    public class Info
{
    public string name { get; set; }
    public int id { get; set; }
}
public class ApplicationContext : DbContext
{

        public DbSet<Info> Info { get; set; } = null!;

        public List<string> GetTaskTxt(string tabn)
        {
            var TasTex = new List<string>();
            using (var command = Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = $@"SELECT tasktext FROM ""{tabn}""";
                Database.OpenConnection();
                using (var result = command.ExecuteReader())
                {
                    while (result.Read())
                    {
                        TasTex.Add(result.GetString(0));
                    }
                }
            }
            return TasTex;
        }

        public List<string> GetTaskDate(string tabn)
        {
            var TasDate = new List<string>();
            using (var command = Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = $@"SELECT sentto FROM ""{tabn}""";
                Database.OpenConnection();
                using (var result = command.ExecuteReader())
                {
                    while (result.Read())
                    {
                        TasDate.Add(result.GetString(0));
                    }
                }
            }
            return TasDate;
        }

        public void CreateDynamicTabl(string tableName)
        {
            var createTableQuery = $@"
            CREATE TABLE IF NOT EXISTS ""{tableName}"" (
                ""id"" SERIAL PRIMARY KEY,
                ""tasktext"" TEXT NOT NULL,
                ""sentat"" TEXT NOT NULL,
                ""sentto"" TEXT NOT NULL
            )";

            Database.ExecuteSqlRaw(createTableQuery);
        }

        public void InfoTable(string name, long id)
        {
            var createTable = $@"
            INSERT INTO info (""id"", ""name"")
            VALUES (@p0, @p1)
            ON CONFLICT (""id"") 
            DO NOTHING";

            Database.ExecuteSqlRaw(createTable, id, name);
        }

        public List<string> GetUserTables()
        {
            var tables = new List<string>();
            using (var command = Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = @"
                SELECT tablename 
                FROM pg_tables 
                WHERE schemaname = 'public' AND tablename LIKE '%_%'";
                Database.OpenConnection();
                using (var result = command.ExecuteReader())
                {
                    while (result.Read())
                    {
                        tables.Add(result.GetString(0));
                    }
                }
            }
            return tables;

        }

        // Получение задач для уведомления
        public List<(long UserId, string TaskText, int TaskId, string SentTo)> GetTasksForNotification(string tableName, DateTime now)
        {
            var tasks = new List<(long, string, int, string)>();
            using (var command = Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = $@"
                SELECT id, tasktext, sentto 
                FROM ""{tableName}"" 
                WHERE sentto <= @p0";  // Задачи, время которых наступило или наступает
                command.Parameters.Add(new NpgsqlParameter("p0", now.ToString("yyyy.MM.dd HH:mm:ss")));
                Database.OpenConnection();
                using (var result = command.ExecuteReader())
                {
                    while (result.Read())
                    {
                        tasks.Add((
                            6804562929, // Предположим, что userId будет в одном месте (можно извлечь из базы)
                            result.GetString(1),  // taskText
                            result.GetInt32(0),   // taskId
                            result.GetString(2)   // sentto (время задачи)
                        ));
                    }
                }
            }
            return tasks;
        }

        public void MarkTaskAsNotified(string tableName, int taskId)
        {
            using (var command = Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = $@"
                DELETE FROM ""{tableName}"" 
                WHERE id = @p0";
                command.Parameters.Add(new NpgsqlParameter("p0", taskId));
                Database.OpenConnection();
                command.ExecuteNonQuery();
            }
        }

        public void AddUserMessage(string tableName, string messageText, string dat)
        {
            var addMessageQuery = $@"
            INSERT INTO ""{tableName}"" (""tasktext"", ""sentat"", ""sentto"")
            VALUES (@p0, @p1, @p2)";

            Database.ExecuteSqlRaw(addMessageQuery, messageText, Convert.ToString(DateTime.UtcNow), dat);
        }
        public ApplicationContext()
        {
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=kurs;Username=postgres;Password=nikitos");
        }
    }
}