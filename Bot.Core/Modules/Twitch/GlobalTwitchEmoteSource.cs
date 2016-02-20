﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Stormbot.Bot.Core.Services;
using StrmyCore;

namespace Stormbot.Bot.Core.Modules.Twitch
{
    internal sealed class GlobalTwitchEmoteSource : CentralziedEmoteSource
    {
        protected override string DataSource => "https://api.twitch.tv/kraken/chat/emoticons";

        protected override void PopulateDictionary(JObject data)
        {
            foreach (JToken token in data["emoticons"])
            {
                EmoteDict.Add(token["regex"].ToObject<string>(),
                    ((JArray) token["images"]).Children().First()["url"].ToObject<string>());
            }

            Logger.FormattedWrite(
                GetType().Name,
                $"Loaded {EmoteDict.Count} global twitch emotes.",
                ConsoleColor.DarkGreen);
        }

        public override async Task<string> GetEmote(string emote, HttpService http)
        {
            if (!EmoteDict.ContainsKey(emote))
                return null;

            string url = EmoteDict[emote];
            string dir = Path.Combine(Constants.TwitchEmoteFolderDir, emote + ".png");

            if (!File.Exists(dir))
            {
                HttpContent content = await http.Send(HttpMethod.Get, url);
                File.WriteAllBytes(dir, await content.ReadAsByteArrayAsync());
            }

            return dir;
        }
    }
}