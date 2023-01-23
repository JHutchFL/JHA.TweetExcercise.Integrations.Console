using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JHA.TweetExcercise.Integrations.Console.Services.TwitterSettings
{
    internal interface ITwitterSettingsService
    {
        TwitterConfiguration GetSettings();
    }
}
