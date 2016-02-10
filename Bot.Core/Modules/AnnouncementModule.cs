﻿using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Modules;
using Newtonsoft.Json;
using Stormbot.Bot.Core.Services;

namespace Stormbot.Bot.Core.Modules
{
    public class AnnouncementModule : IDataModule
    {
        [JsonObject(MemberSerialization.OptIn)]
        private class UserEventCallback
        {
            private Channel _channel;

            public Channel Channel
            {
                get { return _channel; }
                set
                {
                    _channel = value;
                    ChannelId = value.Id;
                }
            }

            [JsonProperty]
            public string Message { get; set; }

            [JsonProperty]
            public bool IsEnabled { get; set; }

            [JsonProperty]
            public ulong ChannelId { get; private set; }

            [JsonConstructor]
            private UserEventCallback(ulong channelid, string message, bool isenabled)
            {
                ChannelId = channelid;
                Message = message;
                IsEnabled = isenabled;
            }

            public UserEventCallback(Channel channel, string message) : this(channel.Id, message, true)
            {
                Channel = channel;
            }
        }

        [DataSave, DataLoad] private ConcurrentDictionary<ulong, UserEventCallback> _userJoinedSubs;
        [DataSave, DataLoad] private ConcurrentDictionary<ulong, UserEventCallback> _userLeftSubs;
        [DataSave, DataLoad] private ConcurrentDictionary<ulong, ulong> _joinedRoleSubs;

        private const string UserNameKeyword = "|userName|";
        private const string LocationKeyword = "|location|";

        private static readonly string DefaultMessage = $"{UserNameKeyword} has joined {LocationKeyword}!";

        private string SyntaxMessage =>
            $"**Syntax:**\r\n```" +
            $"- {UserNameKeyword} - replaced with the name of the user who triggered the event" +
            $"- {LocationKeyword} - replaced with the location (server or channel) where the event occured.```";

        private DiscordClient _client;

        public void Install(ModuleManager manager)
        {
            _client = manager.Client;

            manager.CreateCommands("autorole", group =>
            {
                // commands that are available when the server doesnt have an auto role set on join.
                group.CreateGroup("", noSubGroup =>
                {
                    noSubGroup.AddCheck((cmd, usr, chnl) => !_joinedRoleSubs.ContainsKey(chnl.Server.Id));

                    noSubGroup.CreateCommand("create")
                        .Description("Enables the bot to add a given role to newly joined users.")
                        .Parameter("rolename", ParameterType.Unparsed)
                        .Do(async e =>
                        {
                            string roleQuery = e.GetArg("rolename");
                            Role role = e.Server.FindRoles(roleQuery).FirstOrDefault();

                            if (role == null)
                            {
                                await e.Channel.SendMessage($"A role with the name of `{roleQuery}` was not found.");
                                return;
                            }

                            _joinedRoleSubs.TryAdd(e.Server.Id, role.Id);

                            await
                                e.Channel.SendMessage($"Created an auto role asigned for new users. Role: {role.Name}");
                        });
                });

                // commands that are available when the server does have an auto role set on join.
                group.CreateGroup("", subGroup =>
                {
                    subGroup.AddCheck((cmd, usr, chnl) => (_joinedRoleSubs.ContainsKey(chnl.Server.Id)));

                    subGroup.CreateCommand("destroy")
                        .Description("Destoys the auto role assigner for this server.")
                        .Do(e => RemoveAutoRoleAssigner(e.Server.Id, e.Channel));

                    subGroup.CreateCommand("role")
                        .Parameter("rolename", ParameterType.Unparsed)
                        .Description("Changes the role of the auto role assigner for this server.")
                        .Do(async e =>
                        {
                            string roleQuery = e.GetArg("rolename");
                            Role role = e.Server.FindRoles(roleQuery, false).FirstOrDefault();

                            if (role == null)
                            {
                                await e.Channel.SendMessage($"A role with the name of `{roleQuery}` was not found.");
                                return;
                            }
                            _joinedRoleSubs[e.Server.Id] = role.Id;

                            await e.Channel.SendMessage($"Set the auto role assigner role to `{role.Name}`.");
                        });
                });
            });

            manager.CreateCommands("announce", group =>
            {
                group.CreateGroup("join", joinGroup =>
                {
                    // joinGroup callback exists commands
                    joinGroup.CreateGroup("", existsJoin =>
                    {
                        existsJoin.AddCheck((cmd, usr, chnl) =>
                        {
                            if (_userJoinedSubs.ContainsKey(chnl.Server.Id))
                                return _userJoinedSubs[chnl.Server.Id].IsEnabled;

                            return false;
                        });

                        existsJoin.CreateCommand("message")
                            .Description($"Sets the join message for this current server.\r\n{SyntaxMessage}")
                            .Parameter("message", ParameterType.Unparsed)
                            .Do(async e =>
                            {
                                string msg = e.GetArg("message");
                                _userJoinedSubs[e.Server.Id].Message = msg;
                                await e.Channel.SendMessage($"Set join message to {msg}");
                            });
                        existsJoin.CreateCommand("channel")
                            .Description("Sets the callback channel for this servers join announcements.")
                            .Parameter("channelName", ParameterType.Unparsed)
                            .Do(async e =>
                            {
                                string channelName = e.GetArg("channelName").ToLowerInvariant();
                                Channel channel =
                                    e.Server.TextChannels.FirstOrDefault(c => c.Name.ToLowerInvariant() == channelName);

                                if (channel == null)
                                {
                                    await e.Channel.SendMessage($"Channel with the name {channelName} was not found.");
                                    return;
                                }

                                _userJoinedSubs[e.Server.Id].Channel = channel;
                                await e.Channel.SendMessage($"Set join callback to channel {channel.Name}");
                            });
                        existsJoin.CreateCommand("destroy")
                            .Description("Stops announcing when new users have joined this server.")
                            .Do(async e =>
                            {
                                _userJoinedSubs[e.Server.Id].IsEnabled = false;
                                await
                                    e.Channel.SendMessage(
                                        "Disabled user join messages. You can re-enable them at any time.");
                            });
                    });
                    // no join callback exists commands
                    joinGroup.CreateGroup("", doesntExistJoin =>
                    {
                        doesntExistJoin.AddCheck((cmd, usr, chnl) =>
                        {
                            if (!_userJoinedSubs.ContainsKey(chnl.Server.Id))
                                return true;

                            return !_userJoinedSubs[chnl.Server.Id].IsEnabled;
                        });

                        doesntExistJoin.CreateCommand("enable")
                            .Description("Enables announcing for when a new user joins this server.")
                            .Do(async e =>
                            {
                                if (_userJoinedSubs.ContainsKey(e.Server.Id))
                                    _userJoinedSubs[e.Server.Id].IsEnabled = true;
                                else
                                    _userJoinedSubs.TryAdd(e.Server.Id, new UserEventCallback(e.Channel, DefaultMessage));

                                await
                                    e.Channel.SendMessage(
                                        "Enabled user join messages.\r\nYou can now change the channel and the message by typing !help announce join.");
                            });
                    });
                });

                group.CreateGroup("leave", leaveGroup =>
                {
                    // joinGroup callback exists commands
                    leaveGroup.CreateGroup("", existsLeave =>
                    {
                        existsLeave.AddCheck((cmd, usr, chnl) =>
                        {
                            if (_userLeftSubs.ContainsKey(chnl.Server.Id))
                                return _userLeftSubs[chnl.Server.Id].IsEnabled;

                            return false;
                        });

                        existsLeave.CreateCommand("message")
                            .Description($"Sets the leave message for this current server.\r\n{SyntaxMessage}")
                            .Parameter("message", ParameterType.Unparsed)
                            .Do(async e =>
                            {
                                string msg = e.GetArg("message");
                                _userLeftSubs[e.Server.Id].Message = msg;
                                await e.Channel.SendMessage($"Set leave message to {msg}");
                            });
                        existsLeave.CreateCommand("channel")
                            .Description("Sets the callback channel for this servers leave announcements.")
                            .Parameter("channelName", ParameterType.Unparsed)
                            .Do(async e =>
                            {
                                string channelName = e.GetArg("channelName").ToLowerInvariant();
                                Channel channel =
                                    e.Server.TextChannels.FirstOrDefault(c => c.Name.ToLowerInvariant() == channelName);

                                if (channel == null)
                                {
                                    await e.Channel.SendMessage($"Channel with the name {channelName} was not found.");
                                    return;
                                }

                                _userLeftSubs[e.Server.Id].Channel = channel;
                                await e.Channel.SendMessage($"Set leave callback to channel {channel.Name}");
                            });
                        existsLeave.CreateCommand("destroy")
                            .Description("Stops announcing when users have left joined this server.")
                            .Do(async e =>
                            {
                                _userLeftSubs[e.Server.Id].IsEnabled = false;
                                await
                                    e.Channel.SendMessage(
                                        "Disabled user join messages. You can re-enable them at any time.");
                            });
                    });
                    // no leavea callback exists commands
                    leaveGroup.CreateGroup("", doesntExistLeave =>
                    {
                        doesntExistLeave.AddCheck((cmd, usr, chnl) =>
                        {
                            if (!_userLeftSubs.ContainsKey(chnl.Server.Id))
                                return true;

                            return !_userLeftSubs[chnl.Server.Id].IsEnabled;
                        });

                        doesntExistLeave.CreateCommand("enable")
                            .Description("Enables announcing for when a user leaves this server.")
                            .Do(async e =>
                            {
                                if (_userLeftSubs.ContainsKey(e.Server.Id))
                                    _userLeftSubs[e.Server.Id].IsEnabled = true;
                                else
                                    _userLeftSubs.TryAdd(e.Server.Id, new UserEventCallback(e.Channel, DefaultMessage));

                                await
                                    e.Channel.SendMessage(
                                        "Enabled user leave messages.\r\nYou can now change the channel and the message by typing !help announce leave.");
                            });
                    });
                });
            });
            manager.UserJoined += async (s, e) =>
            {
                if (!manager.EnabledServers.Contains(e.Server)) return;
                if (_userJoinedSubs.ContainsKey(e.Server.Id))
                {
                    UserEventCallback callback = _userJoinedSubs[e.Server.Id];
                    if (callback.IsEnabled)
                        await callback.Channel.SendMessage(ParseString(callback.Message, e.User, e.Server));
                }

                if (_joinedRoleSubs.ContainsKey(e.Server.Id))
                {
                    // verify that the role still exists.
                    Role role = e.Server.GetRole(_joinedRoleSubs[e.Server.Id]);

                    if (role == null)
                    {
                        await RemoveAutoRoleAssigner(e.Server.Id, null, false);

                        Channel callback = e.Server.TextChannels.FirstOrDefault();
                        if (callback != null)
                            await callback.SendMessage("Auto role assigner was given a non existant role. Removing.");

                        return;
                    }

                    await e.User.AddRoles(role);
                }
            };

            manager.UserLeft += async (s, e) =>
            {
                if (!manager.EnabledServers.Contains(e.Server)) return;
                if (!_userLeftSubs.ContainsKey(e.Server.Id)) return;

                UserEventCallback callback = _userLeftSubs[e.Server.Id];
                if (callback.IsEnabled)
                    await callback.Channel.SendMessage(ParseString(callback.Message, e.User, e.Server));
            };
        }

        private async Task RemoveAutoRoleAssigner(ulong serverId, Channel callback, bool shouldCallback = true)
        {
            ulong ignored;
            _joinedRoleSubs.TryRemove(serverId, out ignored);

            if (shouldCallback)
                await callback.SendMessage("Removed auto role assigner for this server.");
        }

        public void OnDataLoad()
        {
            if (_userJoinedSubs == null)
                _userJoinedSubs = new ConcurrentDictionary<ulong, UserEventCallback>();
            if (_userLeftSubs == null)
                _userLeftSubs = new ConcurrentDictionary<ulong, UserEventCallback>();
            if (_joinedRoleSubs == null)
                _joinedRoleSubs = new ConcurrentDictionary<ulong, ulong>();

            LoadChannels(_userJoinedSubs);
            LoadChannels(_userLeftSubs);
        }

        private void LoadChannels(ConcurrentDictionary<ulong, UserEventCallback> dict)
        {
            foreach (var pair in dict)
                pair.Value.Channel = _client.GetServer(pair.Key).GetChannel(pair.Value.ChannelId);
        }

        private string ParseString(string input, User user, dynamic location)
            => input.Replace(UserNameKeyword, user.Name).Replace(LocationKeyword, location.Name);
    }
}