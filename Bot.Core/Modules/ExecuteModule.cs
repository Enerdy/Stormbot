﻿using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Levels;
using Discord.Modules;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Stormbot.Helpers;

namespace Stormbot.Bot.Core.Modules
{
    public class ExecuteModule : IModule
    {
        public class Globals
        {
            public CommandEventArgs e;
            public DiscordClient client;

            public Globals(CommandEventArgs e, DiscordClient client)
            {
                this.e = e;
                this.client = client;
            }
        }

        private DiscordClient _client;

        private ScriptOptions _scriptOptions = ScriptOptions.Default;

        public void Install(ModuleManager manager)
        {
            _client = manager.Client;

            _scriptOptions = _scriptOptions.AddReferences(
                typeof (object).Assembly,
                typeof (System.Linq.Enumerable).Assembly,
                GetType().Assembly,
                _client.GetType().Assembly)
                .AddImports("System", "System.Linq", "System.Collections.Generic", "Stormbot", "Discord");

            manager.CreateCommands("exec", group =>
            {
                group.MinPermissions((int) PermissionLevel.BotOwner);

                group.CreateCommand("")
                    .Description("Compiles and runs a C# script.")
                    .Parameter("query", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        if (e.User.Name != "SSStormy" ||
                            e.User.Id != Constants.UserOwner)
                            return; // really have to make sure that it's me calling this tbh.

                        try
                        {
                            object output =
                                await CSharpScript.EvaluateAsync(e.GetArg("query"), globals: new Globals(e, _client));

                            if (output == null || (output as Task) != null || (output as string) == string.Empty)
                                await e.Channel.SendMessage("Output was empty or null.");
                            else
                                await e.Channel.SendMessage(Format.Code(output.ToString()));
                        }
                        catch (CompilationErrorException ex)
                        {
                            await e.Channel.SendMessage($"Compilation failed: {Format.Code(ex.Message)}");
                        }
                        GC.Collect();
                    });
            });
        }
    }
}