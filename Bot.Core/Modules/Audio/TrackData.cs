﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Stormbot.Helpers;
using StrmyCore;

namespace Stormbot.Bot.Core.Modules.Audio
{
    [Serializable, JsonObject(MemberSerialization.OptOut)]
    public class TrackData
    {
        [JsonIgnore] private static readonly List<IStreamResolver> Resolvers = new List<IStreamResolver>
        {
            new YoutubeResolver(),
            new SoundcloudResolver(),
            new LivestreamerResolver()
        };

        // cached reference to a valid resolver, we don't serialzie this.
        [JsonIgnore] private IStreamResolver _cachedResolver;

        public string Location { get; }
        public TimeSpan Length { get; set; }
        public string Name { get; private set; }

        [JsonConstructor, UsedImplicitly]
        private TrackData(string location, TimeSpan length, string name)
        {
            Location = location;
            Length = length;
            Name = name;
        }

        private TrackData(string location, string name)
        {
            Location = location;
            Name = name;
        }

        [CanBeNull]
        public string GetStream()
        {
            string retval = null;

            if (File.Exists(Location)) retval = Location;

            if (_cachedResolver != null)
                retval = _cachedResolver.ResolveStreamUrl(Location);

            IStreamResolver resolver = Resolvers.FirstOrDefault(r => r.CanResolve(Location));

            if (resolver != null)
                retval = resolver.ResolveStreamUrl(Location);
            else
                Logger.FormattedWrite("TrackData", $"Failed getting stream url for {Location}", ConsoleColor.Red);

            if (Length == TimeSpan.Zero)
                GetLength(retval);

            return retval;
        }

        [CanBeNull]
        public static TrackData Parse(string input)
        {
            if (File.Exists(input))
                return new TrackData(input, Utils.GetFilename(input));

            foreach (IStreamResolver resolver in Resolvers)
                if (resolver.CanResolve(input))
                    return new TrackData(input, resolver.GetTrackName(input)) {_cachedResolver = resolver};

            return null;
        }
        private void GetLength(string location)
        {
            try
            {
                using (Process ffprobe = new Process
                {
                    StartInfo =
                    {
                        FileName = Constants.FfprobeDir,
                        Arguments =
                            $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{location}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                    },
                    EnableRaisingEvents = true
                })
                {
                    ffprobe.OutputDataReceived += (sender, args) =>
                    {
                        if (string.IsNullOrEmpty(args.Data)) return;
                        if (args.Data == "N/A") return;

                        Length = TimeSpan.FromSeconds(
                            int.Parse(
                                args.Data.Remove(
                                    args.Data.IndexOf('.'))));
                    };

                    if (!ffprobe.Start())
                        Logger.FormattedWrite(typeof (TrackData).Name, "Failed starting ffprobe.", ConsoleColor.Red);

                    ffprobe.BeginOutputReadLine();
                    ffprobe.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Logger.FormattedWrite(typeof (TrackData).Name, $"Failed getting track length. Exception: {ex}",
                    ConsoleColor.Red);
            }
        }
    }
}
