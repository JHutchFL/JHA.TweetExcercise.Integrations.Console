using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace JHA.TweetExcercise.Integrations.Console.Extensions
{
    public static class Concurrency
    {
        public static void AddRange<T>(this ConcurrentBag<T> @this, T[] toAdd)
        {
            for (int i = 0; i < toAdd.Length; i++)
            {
                @this.Add(toAdd[i]);
            }
        }
    }
}
