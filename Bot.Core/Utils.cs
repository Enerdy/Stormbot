﻿using System.Globalization;
using System.Threading.Tasks;
using Discord;
using Stormbot.Helpers;

namespace Stormbot.Bot.Core
{
    public static class DiscordUtils
    {
        // quick hack
        public static bool ToHex(string input, out uint outVal)
        {
            input = input.ToUpperInvariant();

            if (input.Length > 6)
            {
                outVal = 0;
                return false;
            }

            outVal = uint.Parse(input, NumberStyles.HexNumber);
            return true;
        }

        public static async Task JoinInvite(string inviteId, Channel callback)
        {
            Invite invite = await callback.Client.GetInvite(inviteId);
            if (invite == null)
            {
                await callback.SafeSendMessage("Invite not found.");
                return;
            }
            if (invite.IsRevoked)
            {
                await
                    callback.SafeSendMessage("This invite has expired or the bot is banned from that server.");
                return;
            }

            await invite.Accept();
            await callback.SafeSendMessage("Joined server.");
            await Constants.Owner.SendPrivate($"Joined server: `{invite.Server.Name}`.");
        }
    }
}
