using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Hspi.Utils
{
    internal static class ExceptionHelper
    {
        public static string GetFullMessage(this Exception ex)
        {
            return GetFullMessage(ex, Environment.NewLine);
        }

        public static string GetFullMessage(this Exception ex, string eol)
        {
            var list = GetMessageList(ex);

            List<string> results = new();
            foreach (var element in list)
            {
                if (results.Count == 0 || results[results.Count - 1] != element)
                {
                    results.Add(element);
                }
            }

            return string.Join(eol, results);
        }

        public static bool IsCancelException(this Exception ex)
        {
            return (ex is TaskCanceledException) ||
                   (ex is OperationCanceledException) ||
                   (ex is ObjectDisposedException);
        }

        private static List<string> GetMessageList(Exception ex)
        {
            var list = new List<string>();
            switch (ex)
            {
                case AggregateException aggregationException:
                    foreach (var innerException in aggregationException.InnerExceptions)
                    {
                        list.AddRange(GetMessageList(innerException));
                    }
                    break;

                default:
                    {
                        string message = ex.Message.Trim(new char[] { ' ', '\r', '\n' });
                        list.Add(message);
                        if (ex.InnerException != null)
                        {
                            list.AddRange(GetMessageList(ex.InnerException));
                        }
                    }
                    break;
            }

            return list;
        }
    };
}