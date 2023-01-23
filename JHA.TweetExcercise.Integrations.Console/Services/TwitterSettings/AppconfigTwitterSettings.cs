using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace JHA.TweetExcercise.Integrations.Console.Services.TwitterSettings
{
    internal class AppconfigTwitterSettings: ITwitterSettingsService
    {
        public AppconfigTwitterSettings() 
        {

        }

        public TwitterConfiguration GetSettings()
        {
            return new TwitterConfiguration()
            {
                ApiKey = ConfigurationManager.AppSettings["ConsumerKey"],
                ApiSecret = ConfigurationManager.AppSettings["ConsumerSecret"],
                Bearer = ConfigurationManager.AppSettings["BearerToken"]
            };
        }
    }
}
