using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace kurs
{
    public class Undefined
    {
        public string KeyWord => "undefined";
    }

    public class RequestPreviousMonthInfo
    {
        public string KeyWord => "previous_month:";
    }

    public class RequestNextMonthInfo
    {
        public string KeyWord => "next_month:";
    }

    public class RequestDateInfo
    {
        public string KeyWord => "date:";
    }

    public static class InlineCalendarFactory
    {
        public static InlineKeyboardMarkup GetKeyboard(DateTime dateMonth, int editingMessageId)
        {
            DateTime date = new DateTime(dateMonth.Year, dateMonth.Month, 1);
            List<List<InlineKeyboardButton>> rows = new List<List<InlineKeyboardButton>>();

            rows.Add(new List<InlineKeyboardButton>());
            rows.Add(new List<InlineKeyboardButton>());
            rows.Add(new List<InlineKeyboardButton>());

            rows[0].AddRange(new InlineKeyboardButton[] { InlineKeyboardButton.WithCallbackData(date.Month.ToString() + "." + date.Year.ToString(), new Undefined().KeyWord) });

            string month = date.Month.ToString();
            string day = date.Day.ToString();

            if (date.Month < 10)
                month = "0" + date.Month.ToString();

            if (date.Day < 10)
                day = "0" + date.Day.ToString();

            rows[1].AddRange(new InlineKeyboardButton[] { InlineKeyboardButton.WithCallbackData("<", new RequestPreviousMonthInfo().KeyWord + date.Year.ToString()
                                                                                                + month + day
                                                                                                + "m:" + editingMessageId.ToString()),
                                                          InlineKeyboardButton.WithCallbackData(">", new RequestNextMonthInfo().KeyWord + date.Year.ToString()
                                                                                                + month + day
                                                                                                + "m:" + editingMessageId.ToString())});

            rows[2].AddRange(new InlineKeyboardButton[] { InlineKeyboardButton.WithCallbackData("Пн", new Undefined().KeyWord),
                                                          InlineKeyboardButton.WithCallbackData("Вт", new Undefined().KeyWord),
                                                          InlineKeyboardButton.WithCallbackData("Ср", new Undefined().KeyWord),
                                                          InlineKeyboardButton.WithCallbackData("Чт", new Undefined().KeyWord),
                                                          InlineKeyboardButton.WithCallbackData("Пт", new Undefined().KeyWord),
                                                          InlineKeyboardButton.WithCallbackData("Сб", new Undefined().KeyWord),
                                                          InlineKeyboardButton.WithCallbackData("Вс", new Undefined().KeyWord)});

            int daysAmount = DateTime.DaysInMonth(date.Year, date.Month);
            int daysInWeek = 7;
            bool isThisMonth = false;

            for (int i = 1, n = 3; i <= daysAmount; n++)
            {
                rows.Add(new List<InlineKeyboardButton>());

                for (int k = 1; k <= daysInWeek; k++, i++)
                {
                    if (k == (int)date.Date.DayOfWeek || k > 6)
                        isThisMonth = true;

                    if (i > daysAmount)
                    {
                        for (int m = k; m <= daysInWeek; m++)
                        {
                            rows[n].Add(InlineKeyboardButton.WithCallbackData(" ", new Undefined().KeyWord));
                        }
                        break;
                    }

                    if (!isThisMonth)
                    {
                        rows[n].Add(InlineKeyboardButton.WithCallbackData(" ", new Undefined().KeyWord));
                        i = 0;
                        continue;
                    }

                    string callbackDate = new RequestDateInfo().KeyWord + date.Year.ToString();
                    month = date.Month.ToString();
                    day = i.ToString();

                    if (date.Month < 10)
                        month = "0" + date.Month.ToString();

                    if (i < 10)
                        day = "0" + i.ToString();

                    callbackDate += month + day + "m:" + editingMessageId.ToString();
                    rows[n].Add(InlineKeyboardButton.WithCallbackData(i.ToString(), callbackDate));
                }
            }
            InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(rows);
            
            return keyboard;
        }
    }
}
