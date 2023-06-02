using System;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using NewsAPI;
using NewsAPI.Models;
using NewsAPI.Constants;
using Newtonsoft.Json;
using System.Net.Http.Json;

class Program
{
    static async Task Main()
    {
        var botClient = new TelegramBotClient("5983272521:AAEJDcoUXbgWVjuvrrkkLTA_UNuX26opfFs");

        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() 
        };

        botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, receiverOptions, cts.Token);

        var me = await botClient.GetMeAsync();

        Console.WriteLine($"Start listening for @{me.Username}");
        Console.ReadLine();

        cts.Cancel();
    }
    static Dictionary<long, string> userAction = new();
    private static readonly List<string> _bookmarks = new List<string>();

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
            return;

        var chatId = message.Chat.Id;
        
        Console.WriteLine($"Received a message from chat {chatId}.");
        if (userAction.ContainsKey(chatId) && !string.IsNullOrEmpty(userAction[chatId]))
        {
            if("/querry" == userAction[chatId])
            {
                string newsName = update.Message.Text;
                userAction[chatId] = string.Empty;
                if (!string.IsNullOrEmpty(newsName))
                {
                    var articles = await CallMyNewsQuerryApiAsync($"{newsName}");

                    if (articles == null)
                    {
                        await botClient.SendTextMessageAsync(chatId, "Не вдалося отримати останні новини.", cancellationToken: cancellationToken);
                        return;
                    }
                    foreach (var article in articles)
                    {
                        var title = article.Title;
                        var author = article.Author;
                        var description = article.Description;
                        var url = article.Url;

                        var articleMessage = $"Заголовок: {title}\n" +
                                             $"Автор: {author}\n" +
                                             $"Опис: {description}\n" +
                                             $"URL: {url}";

                        await botClient.SendTextMessageAsync(chatId, articleMessage, cancellationToken: cancellationToken);
                    }
                }
            }
            if ("/country" == userAction[chatId])
            {
                string countryName = update.Message.Text;
                userAction[chatId] = string.Empty;
                if (!string.IsNullOrEmpty(countryName))
                {
                    if (countryName.Length != 2 || !countryName.All(char.IsLetter))
                    {
                        await botClient.SendTextMessageAsync(chatId, "Ви ввели неправильний код країни. Будь ласка, введіть двузначний код країни англійською мовою (наприклад, 'ua' для України, 'fr' для Франції і т.д.).", cancellationToken: cancellationToken);
                        return;
                    }
                    var articles = await CallMyNewsCountryApiAsync($"{countryName}");
                    if (articles == null)
                    {
                        await botClient.SendTextMessageAsync(chatId, "Не вдалося отримати останні новини.", cancellationToken: cancellationToken);
                        return;
                    }
                    foreach (var article in articles)
                    {
                        var title = article.Title;
                        var author = article.Author;
                        var description = article.Description;
                        var url = article.Url;

                        var articleMessage = $"Заголовок: {title}\n" +
                                             $"Автор: {author}\n" +
                                             $"Опис: {description}\n" +
                                             $"URL: {url}";

                        await botClient.SendTextMessageAsync(chatId, articleMessage, cancellationToken: cancellationToken);
                    }
                }
            }
            if ("/enterbookmark" == userAction[chatId])
            {
                string url = update.Message.Text;
                userAction[chatId] = string.Empty;

                if (!string.IsNullOrEmpty(url) && Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    bool bookmarkExists = await CheckBookmarkExistsAsync(url);

                    if (bookmarkExists)
                    {
                        await botClient.SendTextMessageAsync(chatId, "Такий URL уже є в закладках.", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        _bookmarks.Add(url);
                        await AddBookmarkAsync(url);
                        await botClient.SendTextMessageAsync(chatId, "URL успішно додано до закладок. Введіть команду /bookmarks, щоб продивитись закладки або /removebookmark щоб видалити", cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Некоректні дані. Перевірте правильність введення URL сайту.", cancellationToken: cancellationToken);
                }
            }
            if ("/removebookmark" == userAction[chatId])
            {
                string url = update.Message.Text;
                userAction[chatId] = string.Empty;

                if (!string.IsNullOrEmpty(url) && Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    bool bookmarkExists = await CheckBookmarkExistsAsync(url);
                    bool removed = await RemoveBookmarkAsync(url);

                    if (removed && bookmarkExists)
                    {
                        await botClient.SendTextMessageAsync(chatId, "Закладка успішно видалена.", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "Не вдалося видалити закладку.", cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Некоректні дані. Перевірте правильність вводу URL заклдки.", cancellationToken: cancellationToken);
                }
            }
        }
        switch (message.Text?.ToLower())
        {
            case "/start":
                var username = message.From.Username;
                await botClient.SendTextMessageAsync(chatId, $"Привіт {username}! Я Telegram-бот бот щоденних новин. Чим можу Вам допомогти?", cancellationToken: cancellationToken);
                await botClient.SendTextMessageAsync(chatId, "Доступні команди:\n" +
                    "/help - показати всі доступні команди\n" +
                    "/querry - показати останні новини за день по запиту\n" +
                    "/location - переглянути новину де згадуеться мое місто\n" +
                    "/country - новини по країні\n" +
                    "/bookmarks - переглянути закладки, які Ви додали щоб почитати пізніше\n" +
                    "/removebookmark - видалити закладку\n" +
                    "/enterbookmark - додати сайт у закладки щоб почитати пізніше", cancellationToken: cancellationToken);
                break;
            case "/help":
                await botClient.SendTextMessageAsync(chatId, "Доступні команди:\n" +
                    "/querry - показати останні новини за день по запиту\n" +
                    "/location - переглянути новину де згадуеться мое місто\n" +
                    "/country - новини по країні\n" +
                    "/bookmarks - переглянути закладки, які Ви додали щоб почитати пізніше\n" +
                    "/removebookmark - видалити закладку\n" +
                    "/enterbookmark - додати сайт у закладки щоб почитати пізніше", cancellationToken: cancellationToken);
                break;
            case "/country":
                await botClient.SendTextMessageAsync(chatId, "Введіть код країни(наприклад: fr, ua)", cancellationToken: cancellationToken);
                userAction[chatId] = "/country";
                break;
            case "/enterbookmark":
                userAction[chatId] = "/enterbookmark";
                await botClient.SendTextMessageAsync(chatId, "Введіть URL сайту для того щоб додати його до закладок:", cancellationToken: cancellationToken);
                break;
            case "/removebookmark":
                userAction[chatId] = "/removebookmark";
                await botClient.SendTextMessageAsync(chatId, "Введіть URL сайту для того щоб видалити його з закладок:", cancellationToken: cancellationToken);
                break;
            case "/location":
                var (location, articles) = await CallMyNewsByLocationAsync();

                if (articles == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Не двалося отримати останні новими.", cancellationToken: cancellationToken);
                    return;
                }

                await botClient.SendTextMessageAsync(chatId, $"Ваше місто: {location}", cancellationToken: cancellationToken);

                foreach (var article in articles)
                {
                    var title = article.Title;
                    var author = article.Author;
                    var description = article.Description;
                    var url = article.Url;
                    var articleMessage = $"Заголовок: {title}\n" +
                                         $"Автор: {author}\n" +
                                         $"Опис: {description}\n" +
                                         $"URL: {url}";
                    await botClient.SendTextMessageAsync(chatId, articleMessage, cancellationToken: cancellationToken);
                }
                break;
            case "/bookmarks":
                var bookmarks = await GetBookmarksAsync();

                if (bookmarks == null)
                {
                    await botClient.SendTextMessageAsync(chatId, "Не вдалося отримати список закладок.", cancellationToken: cancellationToken);
                    return;
                }

                if (bookmarks.Count == 0)
                {
                    await botClient.SendTextMessageAsync(chatId, "У вас поки немає закладок.", cancellationToken: cancellationToken);
                }
                else
                {
                    StringBuilder messageBuilder = new StringBuilder("Ваші закладки:\n");
                    for (int i = 0; i < bookmarks.Count; i++)
                    {
                        string bookmark = bookmarks[i];
                        messageBuilder.AppendLine($"{i + 1}. {bookmark}");
                    }

                    await botClient.SendTextMessageAsync(chatId, messageBuilder.ToString(), cancellationToken: cancellationToken);
                }

                break;
            case "/querry":
                await botClient.SendTextMessageAsync(chatId, "Введіть запит для новин(вводити англійською):", cancellationToken: cancellationToken);
                userAction[chatId] = "/querry";
                break;
        }
    }
    static async Task<List<Article>> CallMyNewsQuerryApiAsync(string querry)
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.BaseAddress = new Uri("https://localhost:7142/");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await httpClient.GetAsync($"/News/querry?q={querry}");
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var articles = JsonConvert.DeserializeObject<List<Article>>(jsonResponse);
                return articles;
            }
            else
            {
                Console.WriteLine($"Failed to call API. Status code: {response.StatusCode}");
                return null;
            }
        }
    }
    static async Task<List<Article>> CallMyNewsCountryApiAsync(string querry)
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.BaseAddress = new Uri("https://localhost:7142/");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await httpClient.GetAsync($"/News/country?countryCode={querry}");
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var articles = JsonConvert.DeserializeObject<List<Article>>(jsonResponse);
                return articles;
            }
            else
            {
                Console.WriteLine($"Failed to call API. Status code: {response.StatusCode}");
                return null;
            }
        }
    }
    static async Task<(string Location, List<Article> News)> CallMyNewsByLocationAsync()
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.BaseAddress = new Uri("https://localhost:7142/");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await httpClient.GetAsync("/News/location");
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var newsResponse = JsonConvert.DeserializeObject<NewsResponse>(jsonResponse);
                return (newsResponse.Location, newsResponse.News);
            }
            else
            {
                Console.WriteLine($"Failed to call API. Status code: {response.StatusCode}");
                return (null, null);
            }
        }
    }
    static async Task AddBookmarkAsync(string url)
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.BaseAddress = new Uri("https://localhost:7142/");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var bookmarkRequest = new BookmarkRequest { Url = url };
            var jsonRequest = JsonConvert.SerializeObject(bookmarkRequest);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await httpClient.PostAsync("/Bookmarks/bookmark", content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("URL успешно добавлен в закладки.");
            }
            else
            {
                Console.WriteLine("Не удалось добавить URL в закладки.");
            }
        }
    }
    static async Task<List<string>> GetBookmarksAsync()
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.BaseAddress = new Uri("https://localhost:7142/");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await httpClient.GetAsync("/Bookmarks/checkbookmark");
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var bookmarks = JsonConvert.DeserializeObject<List<string>>(jsonResponse);
                return bookmarks;
            }
            else
            {
                Console.WriteLine($"Failed to call API. Status code: {response.StatusCode}");
                return null;
            }
        }
    }
    static async Task<bool> RemoveBookmarkAsync(string url)
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.BaseAddress = new Uri("https://localhost:7142/");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await httpClient.DeleteAsync($"/Bookmarks/delbookmark?url={url}");
            return response.IsSuccessStatusCode;
        }
    }
    static async Task<bool> CheckBookmarkExistsAsync(string url)
    {
        return _bookmarks.Contains(url);
    }
    static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",_ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}

