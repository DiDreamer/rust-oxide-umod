﻿using System;
using System.Collections.Generic;
using Oxide.Core;

using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.StarForge.Sleeping;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Crash Protection", "SweetLouHD", "1.0.0")]
    public class CrashProtection : ReignOfKingsPlugin
    {
        #region Configuration Data
        // Do not modify these values, to configure this plugin edit
        // 'SleeperCrashProtection.json' in your server's config folder.
        // <drive>:\...\save\oxide\config\

        bool configChanged;

        // Plugin settings
        private const string DefaultChatPrefix = "[FFFFFF][[64CEE1]Server[FFFFFF]]: [-]";
        public string ChatPrefix { get; private set; }

        // Plugin options
        private const bool DefaultNotifyAttacker = true;
        private const int DefaultTimeBetweenNotifications = 30; // 30 seconds
        private const int DefaultGracePeriod = 60; // 1 minute

        public bool NotifyAttacker { get; private set; }
        public int TimeBetweenNotifications { get; private set; }
        public int GracePeriod { get; private set; }

        // Plugin messages
        private const string DefaultPlayerNotification = "You can't deal damage to this sleeper.";

        public string PlayerNotification { get; private set; }

        #endregion

        #region StoredData

        class StoredData
        {
            public HashSet<Sleeper> SleeperData = new HashSet<Sleeper>();

            public StoredData()
            {
            }
        }

        class LocationInfo
        {
            public string x;
            public string y;
            public string z;
            Vector3 position;

            public LocationInfo(Vector3 position)
            {
                x = position.x.ToString();
                y = position.y.ToString();
                z = position.z.ToString();
                this.position = position;
            }

            public Vector3 GetPosition()
            {
                if (position == Vector3.zero)
                    position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                return position;
            }
        }

        class Sleeper
        {
            public string Id;
            public string Name;
            public ulong StartedSleeping;
            public LocationInfo Location;

            public Sleeper()
            {
            }

            public Sleeper(Player player)
            {
                Id = player.Id.ToString();
                Name = player.Name;
                StartedSleeping = Instance.GetTimestamp();
                var position = player?.CurrentCharacter?.Entity?.Position ?? player?.CurrentCharacter?.SavedPosition ?? Vector3.zero;
                Location = new LocationInfo(position);
            }

            public ulong GetUserId()
            {
                ulong id;
                return !ulong.TryParse(Id, out id) ? 0 : id;
            }
        }
        
        StoredData storedData;

        Hash<ulong, Sleeper> sleeperdata = new Hash<ulong, Sleeper>();

        #endregion

        public static CrashProtection Instance;
        
        private readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        private readonly Dictionary<Player, ulong> notifications = new Dictionary<Player, ulong>();

        private void Loaded()
        {
            Instance = this;

            storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("Sleeping");
            foreach (var sleeper in storedData.SleeperData)
                sleeperdata[sleeper.GetUserId()] = sleeper;

            LoadConfigData();
        }

        protected override void LoadDefaultConfig() => PrintWarning("New configuration file created.");

        private void Unload() => SaveData();

        private void OnServerSave() => SaveData();

        private void OnEntityHealthChange(EntityDamageEvent e)
        {
            if (e.Damage.Amount < 0) return;
            var sleeper = e.Entity.GetComponentInChildren<PlayerSleeperObject>();
            if (sleeper == null) return;
            var sleeperId = sleeper.SleeperId;
            if (IsAttackable(sleeperId)) return;
            e.Damage.Amount = 0;
            if (!e.Damage.DamageSource.IsPlayer) return;
            var attacker = e.Damage.DamageSource.Owner;
            if (!NotifyAttacker) return;
            if (!notifications.ContainsKey(attacker)) notifications.Add(attacker, 0);
            if ((int)(GetTimestamp() - notifications[attacker]) < TimeBetweenNotifications) return;
            SendMessage(attacker, $"{PlayerNotification}");
            notifications[attacker] = GetTimestamp();
        }

        private void OnPlayerConnected(Player player)
        {
            var sleepingplayer = sleeperdata[player.Id];
            if (sleepingplayer == null) return;
            storedData.SleeperData.Remove(sleepingplayer);
            sleeperdata?.Remove(player.Id);
        }

        private void OnPlayerDisconnected(Player player)
        {
            var sleepingplayer = sleeperdata[player.Id];
            if (sleepingplayer != null) storedData.SleeperData.Remove(sleepingplayer);
            sleepingplayer = new Sleeper(player);
            sleeperdata[player.Id] = sleepingplayer;
            storedData.SleeperData.Add(sleepingplayer);
        }

        private void SendMessage(Player player, string message, params object[] args) => SendReply(player, $"{ChatPrefix}{message}", args);

        public ulong GetTimestamp() => Convert.ToUInt64((DateTime.UtcNow.Subtract(epoch)).TotalSeconds);

        private bool IsAttackable(ulong id)
        {
            var sleepingplayer = sleeperdata[id];
            if (sleepingplayer == null) return true;
            var timeSleeping = GetTimestamp() - sleepingplayer.StartedSleeping;
			if(GracePeriod > -1)
			{
				return (int)timeSleeping > GracePeriod;
			} else {
				return false;
			}
        }

        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("Sleeping", storedData);

        private void LoadConfigData()
        {
            // Plugin settings
            ChatPrefix = GetConfigValue("Settings", "Chat Prefix", DefaultChatPrefix);

            // Plugin options
            NotifyAttacker = GetConfigValue("Options", "Notify Attacker", DefaultNotifyAttacker);
            TimeBetweenNotifications = GetConfigValue("Options", "Time Between Notifications In Seconds",DefaultTimeBetweenNotifications);
            GracePeriod = GetConfigValue("Options", "Grace Period In Seconds", DefaultGracePeriod);

            // Plugin messages
            PlayerNotification = GetConfigValue("Messages", "Notification", DefaultPlayerNotification);

            if (!configChanged) return;
            PrintWarning("The configuration file was updated!");
            SaveConfig();
        }
        
        private T GetConfigValue<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                configChanged = true;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            configChanged = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }

    }
}