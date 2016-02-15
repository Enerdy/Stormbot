﻿using System.Threading.Tasks;

namespace Stormbot.Bot.Core.Modules.Audio
{
    internal interface IStreamResolver
    {
        /// <summary> Attempts to resolve and return a content stream url from the given input. </summary>
        Task<string> ResolveStreamUrl(string input);

        /// <summary> Returns whether this resolver can resolve the given input.</summary>
        bool CanResolve(string input);

        /// <summary> Returns the name of the given track. </summary>
        Task<string> GetTrackName(string input);
    }
}