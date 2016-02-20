﻿using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Stormbot.Bot.Core.Services;
using StrmyCore;

namespace Stormbot.Bot.Core.Modules.Twitch
{
    internal sealed class BttvEmoteSource : CentralziedEmoteSource
    {
        protected override string DataSource => "https://api.betterttv.net/2/emotes";

        protected override void PopulateDictionary(JObject data)
        {
            foreach (JToken token in data["emotes"])
                EmoteDict.Add(token["code"].ToObject<string>(), token["id"].ToObject<string>());

            Logger.FormattedWrite(GetType().Name, $"Loaded {EmoteDict.Count} bttv emotes.", ConsoleColor.DarkGreen);
        }

        public override async Task<string> GetEmote(string emote, HttpService http)
        {
            if (!EmoteDict.ContainsKey(emote))
                return null;

            string imageId = EmoteDict[emote];
            string dir = Path.Combine(Constants.TwitchEmoteFolderDir, imageId + ".png");

            if (!File.Exists(dir))
            {
                HttpContent content =
                    await http.Send(HttpMethod.Get, $"https://cdn.betterttv.net/emote/{imageId}/2x");
                File.WriteAllBytes(dir, await content.ReadAsByteArrayAsync());
            }

            return dir;
        }
    }
}