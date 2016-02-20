﻿using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Levels;
using Discord.Modules;
using Newtonsoft.Json;
using Stormbot.Bot.Core.Services;
using StrmyCore;

namespace Stormbot.Bot.Core.DynPerm
{
    public class DynamicPermissionModule : IModule
    {
        private const string PastebinIdentifier = "http://pastebin.com/";
        private const string RawPath = "raw/";
        private DiscordClient _client;
        private DynamicPermissionService _dynPerms;
        private PastebinService _pastebin;

        public void Install(ModuleManager manager)
        {
            Nullcheck(Config.PastebinPassword, Config.PastebinUsername, Config.PastebinApiKey);

            _client = manager.Client;
            _dynPerms = _client.GetService<DynamicPermissionService>();
            _pastebin = _client.GetService<PastebinService>();

            manager.CreateCommands("dynperm", group =>
            {
                group.MinPermissions((int) PermissionLevel.ServerAdmin);

                group.AddCheck((cmd, usr, chnl) => !chnl.IsPrivate);

                group.CreateCommand("set")
                    .Description(
                        "Sets the dynamic permissions for this server.**Pastebin links are supported.**Use the dynperm help command for more info.")
                    .Parameter("perms", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        string input = e.GetArg("perms");
                        string error;

                        if (input.StartsWith(PastebinIdentifier))
                        {
                            string rawUrl = input.Insert(PastebinIdentifier.Length, RawPath);
                            input = await Utils.AsyncDownloadRaw(rawUrl);
                        }

                        DynPermFullData perms = _dynPerms.SetDynPermFullData(e.Server.Id, input, out error);

                        if (!string.IsNullOrEmpty(error))
                        {
                            await e.Channel.SendMessage($"Failed parsing Dynamic Permissions. {error}");
                            return;
                        }

                        await e.Channel.SendMessage($"Parsed Dynamic Permissions:\r\n```" +
                                                    $"- Role Rules: {perms.Perms.RolePerms.Count}\r\n" +
                                                    $"- User Rules: {perms.Perms.UserPerms.Count}```");
                    });

                // commands which can only be executed if the caller server has dynperms.
                group.CreateGroup("", existsGroup =>
                {
                    existsGroup.AddCheck((cmd, usr, chnl) => _dynPerms.GetPerms(chnl.Server.Id) != null);

                    existsGroup.CreateCommand("show")
                        .Description("Shows the Dynamic Permissions for this server.")
                        .Do(async e =>
                        {
                            DynPermFullData data = _dynPerms.GetPerms(e.Server.Id);

                            if (string.IsNullOrEmpty(data.PastebinUrl) || data.IsDirty)
                            {
                                if (!_pastebin.IsLoggedIn)
                                    await _pastebin.Login(Config.PastebinUsername, Config.PastebinPassword);

                                data.PastebinUrl = await _pastebin.Paste(new PastebinService.PasteBinEntry
                                {
                                    Expiration = PastebinService.PasteBinExpiration.Never,
                                    Format = "json",
                                    Private = true,
                                    Text = JsonConvert.SerializeObject(data.Perms),
                                    Title = $"{e.Server.Name}@{DateTime.Now}"
                                });
                            }

                            await e.Channel.SendMessage($"Paste: {data.PastebinUrl}");
                        });

                    existsGroup.CreateCommand("clear")
                        .Description(
                            "Clears the Dynamic Permissions. This cannot be undone. Pass yes as an argument for this to work.")
                        .Parameter("areyousure")
                        .Do(async e =>
                        {
                            string input = e.GetArg("areyousure").ToLowerInvariant();
                            if (input == "yes" ||
                                input == "y")
                            {
                                _dynPerms.DestroyServerPerms(e.Server.Id);
                                await e.Channel.SendMessage("Dynamic Permissions have been wiped.");
                            }
                        });
                });

                group.CreateCommand("help")
                    .Description("help")
                    .Do(
                        async e =>
                            await
                                e.Channel.SendMessage("https://github.com/SSStormy/Stormbot/blob/master/docs/dynperm.md"));
            });
        }

        private void Nullcheck(params string[] nullCheck)
        {
            foreach (string check in nullCheck.Where(string.IsNullOrEmpty))
                throw new ArgumentNullException(nameof(check));
        }
    }
}