using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tweetinvi;
using System.Configuration;
using Tweetinvi.Models;
using JHA.TweetExcercise.Integrations.Console.Services.TwitterSettings;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Tweetinvi.Models.V2;
using Tweetinvi.Streams;
using Tweetinvi.Streaming.V2;
using System.Collections;
using Tweetinvi.Core.Exceptions;
using JHA.TweetExcercise.Integrations.Console.Extensions;
using System.Timers;

namespace MyProject;


public class Program
{
    static Stopwatch _queueProducerSW = new Stopwatch();
    static Stopwatch _queueConsumerSW = new Stopwatch();
    static Int32 _tweetsReceived = 0;
    static Int32 _processedTweetCount = 0;

    static ConcurrentQueue<TweetV2> _tweetQueue = new ConcurrentQueue<TweetV2>();
    static ConcurrentBag<String> _hashtagBag = new ConcurrentBag<String>();

    public static async Task Main(string[] args)
    {
        
        ITwitterSettingsService _settingsService = new AppconfigTwitterSettings();
        TwitterConfiguration _settings = _settingsService.GetSettings();

        var appCredentials = new ConsumerOnlyCredentials(_settings.ApiKey, _settings.ApiSecret)
        {
            BearerToken = _settings.Bearer
        };

        //instantiate a client
        var userClient = new TwitterClient(appCredentials);

        //handle client exceptions
        userClient.Events.OnTwitterException += (sender, args) =>
        {
            HandleTwitterException(args);
        };

        //instantiate the stream
        var sampleStreamV2 = userClient.StreamsV2.CreateSampleStream();
        sampleStreamV2.TweetReceived += (sender, args) =>
        {

            _tweetQueue.Enqueue(args.Tweet);
            _tweetsReceived++;

        };

        //create a Timer component
        using System.Timers.Timer consumerProcessor = new System.Timers.Timer();

        //throttle it for reads
        consumerProcessor.Interval = 5;
        //create a timer callback as a simple pump to read the queue
        consumerProcessor.Elapsed += (sender, args) =>
        {
            ProcessTweet();
        };

        //loop count to 10
        int loopCount = 0;
        int maxLoopCount = 10;

        //create a timer to update the console on progress
        using System.Timers.Timer DiagnosticsTimer = new System.Timers.Timer();
        DiagnosticsTimer.Interval = 5000;
        var breakLoop = false;
        //create a timer callback as a simple pump to read the queue
        DiagnosticsTimer.Elapsed += async (sender, args) =>
        {
            var diagnosticTimer = sender as System.Timers.Timer;
            if (diagnosticTimer != null)
            {
                diagnosticTimer.Stop();


                loopCount++;
                int qCount = _tweetQueue.Count;

                //give a count of the number of tweets received
                Console.WriteLine("Count of messages received " + _tweetsReceived.ToString() + " in " + _queueProducerSW.Elapsed.TotalSeconds.ToString() + " seconds");

                //give a count of the number of tweets received per second
                Console.WriteLine("Count of messages received per second " + (_tweetsReceived / _queueProducerSW.Elapsed.TotalSeconds).ToString());

                //give a count of the number of tweets processed
                Console.WriteLine("Count of messages processed " + _processedTweetCount.ToString() + " in " + _queueConsumerSW.Elapsed.TotalSeconds.ToString() + " seconds");

                //give a count of the number of tweets processed per second
                Console.WriteLine("Count of messages processed per second " + (_processedTweetCount / _queueConsumerSW.Elapsed.TotalSeconds).ToString());

                //give a current queue count
                Console.WriteLine("Count of messages currently in queue " + qCount.ToString());

                //determine the top10 hashtags
                var top10 =
                    from hashtag in _hashtagBag
                    group hashtag by hashtag into g
                    select new { g.Key, Count = g.Count() };
                var topHashTags = top10.OrderByDescending(p => p.Count).Take(10).ToList();

                //give a list of the top 10 with the hashtag and its count
                Console.WriteLine("Top 10 HashTags so far:");
                foreach (var tag in topHashTags)
                {
                    Console.WriteLine(tag.Key + " - " + tag.Count.ToString());
                }
                Console.WriteLine(System.Environment.NewLine);
                Console.WriteLine(System.Environment.NewLine);

                if (loopCount == maxLoopCount)
                {
                    //wait to see if the user wants to run again
                    Console.WriteLine(System.Environment.NewLine);
                    Console.WriteLine("Press ENTER to continue or X to quit:");
                    var again = Console.ReadLine();
                    if (again == "x")
                    {
                        sampleStreamV2.StopStream();
                        consumerProcessor.Stop();
                        consumerProcessor.Dispose();
                        breakLoop = true;
                    }
                    else
                    {
                        loopCount = 0;
                        consumerProcessor.Stop();
                        _hashtagBag.Clear();
                        _tweetQueue.Clear();

                        _tweetsReceived = 0;
                        _processedTweetCount = 0;
                        Console.WriteLine("Starting to collect Twitter Samples");
                        _queueProducerSW.Restart();

                        diagnosticTimer.Start();

                        await Task.Delay(20000);
                        Console.WriteLine("Starting to process samples");
                        _queueConsumerSW.Restart();
                        consumerProcessor.Start();
                    }
                }
                else
                {
                    diagnosticTimer.Start();
                }
            }
        };


        Console.WriteLine("Starting to collect Twitter Samples");

        //start the stream as a fire and forget operation so the queue is filling
        //we do not want to await this
        //the queue begins filling from the stream with this task
        var t = Task.Factory.StartNew(async () => await FireAndForget(sampleStreamV2))
        .ContinueWith(t => {
            switch (t.Status)
            {
                case TaskStatus.Created:
                case TaskStatus.WaitingForActivation:
                case TaskStatus.WaitingToRun:
                case TaskStatus.Running:
                case TaskStatus.WaitingForChildrenToComplete:
                case TaskStatus.RanToCompletion:
                    break;
                case TaskStatus.Canceled:
                case TaskStatus.Faulted:
                    //Log error message if any 
                    HandleStreamException(t.Exception);
                    break;
                default:
                    break;
            }
        });

        //start the stopwatch
        _queueProducerSW.Start();

        //start the loop timer
        DiagnosticsTimer.Start();

        //prime the queue
        await Task.Delay(20000);

        Console.WriteLine("Starting to process samples");
        _queueConsumerSW.Start();
        //start the timer to read from the queue
        consumerProcessor.Start();



        //let it work until break 
        while (!breakLoop)
        {
        }

        DiagnosticsTimer.Dispose();
    }

    internal static void HandleTwitterException(ITwitterException ex)
    {
        // lets delay all operations from this client by 1 second
        System.Console.WriteLine(ex);
    }

    internal static void HandleStreamException(Exception ex)
    {
        if(ex == null)
        {
            System.Console.WriteLine(ex);
        }
    }

    internal static void ProcessTweet()
    {
        if (_tweetQueue.TryDequeue(out TweetV2? tweet))
        {
            if (tweet != null)
            {
                var tags = tweet.Entities.Hashtags;
                if (tags != null)
                {
                    for (int i = 0; i < tags.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(tags[i].Tag))
                        {
                            _hashtagBag.Add(tags[i].Tag);
                        }
                    }
                }
                _processedTweetCount++;
            }
        }
    }

    internal static  async Task FireAndForget(ISampleStreamV2 sampleStreamV2)
    {
        await sampleStreamV2.StartAsync();
    }

}
