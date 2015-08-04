using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using JetBrains.Annotations;
using LatestTwitter.Contracts.Services;
using LatestTwitter.Models;
using Orchard.Caching;
using Orchard.Services;

namespace LatestTwitter.Services
{
    [UsedImplicitly]
    public class CachedTweetRetrievalService
        : ITweetRetrievalService
    {
        protected readonly string CacheKeyPrefix = "B74EDE32-86E4-4A58-850B-016E6F595CF9_";

        protected ICacheManager CacheManager { get; private set; }
        protected IClock Clock { get; private set; }

        public CachedTweetRetrievalService(ICacheManager cacheManager, IClock clock)
        {
            this.CacheManager = cacheManager;
            this.Clock = clock;
        }

        public List<TweetModel> GetTweetsFor(TwitterWidgetPart part)
        {
            // Build cache key
            var cacheKey = CacheKeyPrefix + part.Username;

            return CacheManager.Get(cacheKey, ctx =>
            {
                ctx.Monitor(Clock.When(TimeSpan.FromMinutes(part.CacheMinutes)));
                return RetrieveTweetsFromTwitterFor(part);
            });
        }

        protected List<TweetModel> RetrieveTweetsFromTwitterFor(TwitterWidgetPart part)
        {
            // Build tweet list
            var tweetList = new List<TweetModel>();

            if (!string.IsNullOrEmpty(part.Username) && !string.IsNullOrEmpty(part.TwitterConsumerKey) && !string.IsNullOrEmpty(part.TwitterConsumerSecret))
            {
                // Obtain bearer token using applicatiuon authroization
                var bearerToken = ObtainBearerToken(part.TwitterConsumerKey, part.TwitterConsumerSecret);

                // Call timeline API
                var timelineUrl = string.Format("https://api.twitter.com/1.1/statuses/user_timeline.json?exclude_replies={0}&screen_name={1}", !part.ShowReplies ? "true" : "false", Uri.EscapeDataString(part.Username));
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(timelineUrl);
                request.Headers.Add("Authorization", "Bearer " + bearerToken);
                request.Method = "GET";
                request.ContentType = "application/x-www-form-urlencoded;charset=UTF-8";
                request.AutomaticDecompression = DecompressionMethods.GZip;

                WebResponse response = request.GetResponse();
                string responseData = new StreamReader(response.GetResponseStream()).ReadToEnd();

                var serializer = new JavaScriptSerializer();
                var fromJsonArray = serializer.Deserialize<IEnumerable<TwitterJson>>(responseData);

                string[] hashtagFilter = null;
                if (!string.IsNullOrEmpty(part.HashTagsFilter))
                {
                    hashtagFilter = part.HashTagsFilter.Split(',');
                }

                var tweets = from tweet in fromJsonArray
                             where
                                 // Hashtags
                                (hashtagFilter == null || (hashtagFilter != null && hashtagFilter.Any(p => tweet.text.Contains(p))))
                                &&
                                 // Show replies
                                (part.ShowReplies || String.IsNullOrEmpty(tweet.in_reply_to_screen_name))
                             select new TweetModel()
                             {
                                 Message = tweet.text,
                                 Username = tweet.user.screen_name,
                                 Avatar = tweet.user.profile_image_url,
                                 Timestamp = ToFriendlyDate(DateTime.ParseExact(tweet.created_at, "ddd MMM dd HH:mm:ss %zzzz yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)),
                                 Id = tweet.id_str
                             };

                tweetList.AddRange(tweets.Take(part.Count > 0 ? part.Count : 10));
            }

            // Return
            return tweetList;
        }

        protected string ObtainBearerToken(string consumerKey, string consumerSecret)
        {
            ServicePointManager.Expect100Continue = false;

            var applicationAuthorization = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(string.Format("{0}:{1}", Uri.EscapeDataString(consumerKey), Uri.EscapeDataString(consumerSecret))));

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.twitter.com/oauth2/token");
            request.Headers.Add("Authorization", "Basic " + applicationAuthorization);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded;charset=UTF-8";
            request.AutomaticDecompression = DecompressionMethods.GZip;
            using (StreamWriter writer = new StreamWriter(request.GetRequestStream()))
            {
                writer.Write("grant_type=client_credentials");
            }

            WebResponse response = request.GetResponse();
            var token = "";
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                token = reader.ReadToEnd();
            }

            var serializer = new JavaScriptSerializer();
            var tokenJson = serializer.Deserialize<TokenJson>(token);

            return tokenJson.access_token;
        }

        protected string ToFriendlyDate(DateTime sourcedate)
        {
            // 1.
            // Get time span elapsed since the date.
            TimeSpan s = DateTime.UtcNow.Subtract(sourcedate);

            // 2.
            // Get total number of days elapsed.
            int dayDiff = (int)s.TotalDays;

            // 3.
            // Get total number of seconds elapsed.
            int secDiff = (int)s.TotalSeconds;

            // 4.
            // Don't allow out of range values.
            if (dayDiff < 0 || dayDiff >= 31)
            {
                return null;
            }

            // 5.
            // Handle same-day times.
            if (dayDiff == 0)
            {
                // A.
                // Less than one minute ago.
                if (secDiff < 60)
                {
                    return "just now";
                }
                // B.
                // Less than 2 minutes ago.
                if (secDiff < 120)
                {
                    return "1 minute ago";
                }
                // C.
                // Less than one hour ago.
                if (secDiff < 3600)
                {
                    return string.Format("{0} minutes ago",
                        Math.Floor((double)secDiff / 60));
                }
                // D.
                // Less than 2 hours ago.
                if (secDiff < 7200)
                {
                    return "1 hour ago";
                }
                // E.
                // Less than one day ago.
                if (secDiff < 86400)
                {
                    return string.Format("{0} hours ago",
                        Math.Floor((double)secDiff / 3600));
                }
            }
            // 6.
            // Handle previous days.
            if (dayDiff == 1)
            {
                return "yesterday";
            }
            if (dayDiff < 7)
            {
                return string.Format("{0} days ago",
                    dayDiff);
            }
            if (dayDiff < 31)
            {
                return string.Format("{0} weeks ago",
                    Math.Ceiling((double)dayDiff / 7));
            }
            return sourcedate.ToString();
        }

        private class TokenJson
        {
            public string access_token { get; set; }
        }

        private class UserJson
        {
            public string id_str { get; set; }
            public string profile_image_url { get; set; }
            public string screen_name { get; set; }
        }

        private class TwitterJson
        {
            public string id_str { get; set; }
            public string text { get; set; }
            public string created_at { get; set; }
            public string in_reply_to_screen_name { get; set; }
            public UserJson user { get; set; }
        }
    }
}
