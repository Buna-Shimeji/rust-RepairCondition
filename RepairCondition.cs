
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RepairCondition", "Buna_Shimeji", "0.0.2")]
    [Description("repair item condition")]
    internal class RepairCondition : RustPlugin
    {
        [PluginReference]
        private Plugin Economics;

        // どれだけ段階的に直せるか
        private List<int> limit = new List<int>() { 100, 75, 50, 25 };

        private void Init()
        {
            permission.RegisterPermission("repaircondition.chatcommand", this);

            foreach(var a in limit)
            {
                permission.RegisterPermission($"repaircondition.fix.{a}", this);
            }
        }

        [ChatCommand("repair")]
        void RepairCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, "repaircondition.chatcommand"))
            {
                SendMessage(player, "MessageNoPermission");
                return;
            }

            if (config.useEconomics && Economics == null)
            {
                PrintError("Economics is not load!");
                return;
            }

            int MaxFix = 0;

            // power!
            // 与えられたパーミッショのうち最も大きい値をもってくる
            foreach(var a in limit)
            {
                if (HasPermission(player, $"repaircondition.fix.{a}"))
                {
                    MaxFix = a;
                    break;
                }
            }

            // アイテムが修理可能か
            Item activeItem = player.GetActiveItem();
            if (activeItem == null || !activeItem.hasCondition || (config.blackList != null && config.blackList.Contains(activeItem.info.shortname) ))
            {
                SendMessage(player, "MessageCantRepair");
                return;
            }

            // パーミッションと今の割合の比較
            if(activeItem.maxCondition / activeItem.info.condition.max > MaxFix/100.0f)
            {
                SendMessage(player, "MessageNotAnyFurther");
                return;
            }

            // 支払い
            object isBuy = Economics?.Call("Withdraw", player.userID, (double)config.amount);
            if (isBuy == null || !(bool)isBuy)
            {
                SendMessage(player, "MessageNotEnoughMoney");
                return;
            }

            // 修理
            activeItem.maxCondition = activeItem.info.condition.max * (MaxFix / 100.0f);
            activeItem.RepairCondition(activeItem.info.condition.max * (MaxFix / 100.0f) - activeItem.condition);
            
            SendMessage(player, "MessageRepaired", activeItem.info.displayName.translated, MaxFix);
        }

        #region Localization
        protected override void LoadDefaultMessages()
        {
            if(config.useLangAPI) lang.RegisterMessages(langage, this);
        }

        Dictionary<string, string> langage = new Dictionary<string, string>()
        {
            ["MessageNoPermission"] = "You don't have permission to use this command.",
            ["MessageNotEnoughMoney"] = "Not enough money.",
            ["MessageCantRepair"] = "This item cannot be repaired.",
            ["MessageRepaired"] = "{0} was {1}% repaired.",
            ["MessageNotAnyFurther"] = "Cannot be repaired any further."
        };
        #endregion Localization

        #region Method
        private bool HasPermission(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        private void SendMessage(BasePlayer player, string key, params object[] args)
        {
            if (player == null) return;
            player.ChatMessage(string.Format(GetTranslation(key, player), args));
        }

        private string GetTranslation(string key, BasePlayer player = null)
        {
            if (config.useLangAPI) return lang.GetMessage(key, this, player?.UserIDString);
            return langage[key];
        }
        #endregion Method

        #region Config
        private static ConfigData config = new ConfigData();

        class ConfigData
        {
            public bool useLangAPI;
            public bool useEconomics;
            public float amount;
            public List<string> blackList;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt!");
                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (Interface.Oxide.CallHook("OnConfigValidate") != null)
            {
                PrintWarning("Using default configuration...");
                config = new ConfigData();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData() {
                useLangAPI = false,
                useEconomics = true,
                amount = 1000.0f,
                blackList = new List<string>()
                {
                    "shortnames"
                }
            };
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion Config
    }
}
