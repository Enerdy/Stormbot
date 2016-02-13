﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Stormbot.Bot.Core.DynPerm
{
    [JsonObject]
    public class DynamicPerms
    {
        [JsonProperty]
        public List<DynamicPermissionBlock> RolePerms { get; set; }

        [JsonProperty]
        public List<DynamicPermissionBlock> UserPerms { get; set; }

        [JsonConstructor]
        private DynamicPerms(List<DynamicPermissionBlock> roles, List<DynamicPermissionBlock> users)
        {
            if (roles == null)
                roles = new List<DynamicPermissionBlock>();

            if (users == null)
                users = new List<DynamicPermissionBlock>();

            RolePerms = roles;
            UserPerms = users;
        }

        public DynamicPerms() : this(null, null)
        {
        }
    }

    [JsonObject]
    public class DynamicPermissionBlock
    {
        [JsonProperty]
        public ulong Id { get; }

        [JsonProperty]
        public ModuleCommandPair Allow { get; set; }

        [JsonProperty]
        public ModuleCommandPair Deny { get; set; }

        [JsonConstructor]
        private DynamicPermissionBlock(ulong id, ModuleCommandPair allow, ModuleCommandPair deny)
        {
            if (allow == null)
                allow = new ModuleCommandPair();

            if (deny == null)
                deny = new ModuleCommandPair();

            Id = id;
            Allow = allow;
            Deny = deny;
        }

        public DynamicPermissionBlock(ulong id) : this(id, null, null)
        {
        }
    }

    [JsonObject]
    public class ModuleCommandPair
    {
        [JsonProperty]
        public HashSet<string> Modules { get; set; }

        [JsonProperty]
        public HashSet<string> Commands { get; set; }

        [JsonConstructor]
        private ModuleCommandPair(HashSet<string> modules, HashSet<string> commands)
        {
            if (modules == null)
                modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (commands == null)
                commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!Equals(modules.Comparer, StringComparer.OrdinalIgnoreCase))
                modules = new HashSet<string>(modules, StringComparer.OrdinalIgnoreCase);

            if (!Equals(commands.Comparer, StringComparer.OrdinalIgnoreCase))
                commands = new HashSet<string>(commands, StringComparer.OrdinalIgnoreCase);

            Modules = modules;
            Commands = commands;
        }

        public ModuleCommandPair() : this(null, null)
        {

        }
    }
}
