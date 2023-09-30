using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using Elements.Core;
using System.Reflection;

using System.Threading.Tasks;

namespace UserJoinLeaveNotifications
{
    public class UserJoinLeaveNotifications : ResoniteMod
    {
        public override string Name => "UserJoinLeaveNotifications";
        public override string Author => "badhaloninja";
        public override string Version => "2.0.0";
        public override string Link => "https://github.com/badhaloninja/UserJoinLeaveNotifications";

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> ContactLinks = new("ContactLinks", "Add ContactLinks to notifications, can make notification titles only show username", () => true);


        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> SoundsEnabled = new("SoundsEnabled", "Enable playing sounds when a user joins or leaves", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> RandomizeSoundPitch = new("RandomizeSoundPitch", "Randomize the pitch of the user join/leave sounds", () => false);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Uri> NotificationSoundUri = new("NotificationSound", "Notification sound for user joining or leaving - Disabled when null", () => null);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> NotificationVolume = new("NotificationVolume", "The volume for the user join/leave sound to play at", () => 1f);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> NotificationJoinColor = new("NotificationJoinColor", "The color for the join notification", () => RadiantUI_Constants.Sub.CYAN);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> OverrideLeaveSound = new("OverrideLeaveSound", "Override the notification sound for leaving", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Uri> NotificationLeaveSoundUri = new("NotificationLeaveSound", "Notification sound for leaving - Only used if override is enabled - Disabled when null", () => null);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> NotificationLeaveVolume = new("NotificationLeaveVolume", "The volume for the user leave sound to play at", () => 1f);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> NotificationLeaveColor = new("NotificationLeaveColor", "The color for the leave notification", () => RadiantUI_Constants.Sub.RED);




        public override void OnEngineInit()
        {
            config = GetConfiguration();
            config.OnThisConfigurationChanged += UpdateAssets;

            Engine.Current.RunPostInit(Setup);
        }

        private static ModConfiguration config;

        private static MethodInfo _addNotificationMethod;
        private static FieldInfo _lastSoundEffectTime;
        private static FieldInfo _worldUserBag;
        public static double LastSoundEffectTime { 
            get
            {
                if (NotificationPanel.Current == null) return 0d;

                return (double)_lastSoundEffectTime.GetValue(NotificationPanel.Current);
            } 
            set
            {
                if (NotificationPanel.Current != null) _lastSoundEffectTime.SetValue(NotificationPanel.Current, value);
            }
        }


        private static UserBag currentUserBag;

        private static StaticAudioClip joinLeaveAudioClip;
        private static StaticAudioClip leaveAudioClip;

        public static void Setup()
        {
            // Hook into the world focused event
            Engine.Current.WorldManager.WorldFocused += OnWorldFocused;

            // Store the method info for later use
            _addNotificationMethod = AccessTools.Method(typeof(NotificationPanel), "AddNotification", new Type[] { typeof(string), typeof(string), typeof(Uri), typeof(colorX), typeof(string), typeof(Uri), typeof(IAssetProvider<AudioClip>) });
            _lastSoundEffectTime = typeof(NotificationPanel).GetField("lastSoundEffect", BindingFlags.Instance | BindingFlags.NonPublic);
            _worldUserBag = typeof(World).GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        private static void OnWorldFocused(World focusedWorld)
        {
            // User join leave events only trigger as host
            if (currentUserBag != null)
            { // Remove the event handler from the old world
                currentUserBag.OnElementAdded -= OnUserJoined;
                currentUserBag.OnElementRemoved -= OnUserLeft;
            }
            
            // Store the user bag of the new world
            currentUserBag = GetUserbag(focusedWorld);
            // Add the event handler to the new world
            currentUserBag.OnElementAdded += OnUserJoined;
            currentUserBag.OnElementRemoved += OnUserLeft;
        }
        private static void OnUserJoined(SyncBagBase<RefID, User> bag, RefID key, User user, bool isNew)
        {
            if (user.IsLocalUser || NotificationPanel.Current == null) return;

            NotificationPanel.Current.RunInUpdates(3, async () =>
            { // Running immediately results in the getuser to return a BadRequest
                Uri thumbnail = await GetUserThumbnail(user.UserID);
                AddNotification(user.UserID, $"{user.UserName} joined", config.GetValue(NotificationJoinColor), "User Joined", thumbnail, false);
            });
        }

        private static async void OnUserLeft(SyncBagBase<RefID, User> bag, RefID key, User user)
        {
            if (user.IsLocalUser || NotificationPanel.Current == null) return;
            
            Uri thumbnail = await GetUserThumbnail(user.UserID);
            AddNotification(user.UserID, $"{user.UserName} left", config.GetValue(NotificationLeaveColor), "User Left", thumbnail, true);
        }

        private static void AddNotification(string userId, string message, colorX backgroundColor, string mainMessage, Uri overrideProfile, bool UserLeaving)
        {
            // Not using Show Notification because it does not expose main message
            if (_addNotificationMethod == null || NotificationPanel.Current == null) return;

            NotificationPanel.Current.RunSynchronously(() =>
            {
                if (config.GetValue(SoundsEnabled)) PlayNotificationSound(UserLeaving);

                _addNotificationMethod.Invoke(NotificationPanel.Current, new object[] { config.GetValue(ContactLinks) ? userId : null, message, null, backgroundColor, mainMessage, overrideProfile, null });
            });
        }

        private static void PlayNotificationSound(bool UserLeaving)
        {
            var notificationPanel = NotificationPanel.Current;
            EnsureAudioClips();


            StaticAudioClip clip = null;
            float volume = 1f;

            if (UserLeaving && config.GetValue(OverrideLeaveSound) && leaveAudioClip.URL.Value != null)
            {
                clip = leaveAudioClip;
                volume = config.GetValue(NotificationLeaveVolume);
            
            } else if (joinLeaveAudioClip.URL.Value != null)
            {
                clip = joinLeaveAudioClip;
                volume = config.GetValue(NotificationVolume);
            }

            if (clip != null && notificationPanel.Time.WorldTime - LastSoundEffectTime >= 0.1)
            {
                var clipSpeed = config.GetValue(RandomizeSoundPitch) ? RandomX.Range(0.95f, 1.05f) : 1f;

                AudioOutput audioOutput = notificationPanel.Slot.PlayOneShot(clip, volume, notificationPanel.InputInterface.VR_Active, clipSpeed, true, AudioDistanceSpace.Global, false);

                audioOutput.AudioTypeGroup.Value = AudioTypeGroup.UI;
                audioOutput.IgnoreReverbZones.Value = true;

                LastSoundEffectTime = notificationPanel.Time.WorldTime;
            }

        }

        // Async method to fetch thumbnail from user id
        private static async Task<Uri> GetUserThumbnail(string userId)
        {
            var cloudUserProfile = (await Engine.Current.Cloud.Users.GetUser(userId))?.Entity?.Profile;
            // Handle fetching profile, AddNotification only gets profile data for Contacts
            Uri.TryCreate(cloudUserProfile?.IconUrl, UriKind.Absolute, out Uri thumbnail);
            thumbnail ??= OfficialAssets.Graphics.Thumbnails.AnonymousHeadset;

            return thumbnail;
        }

        private static UserBag GetUserbag(World world) => (UserBag)_worldUserBag.GetValue(world);    
        private void UpdateAssets(ConfigurationChangedEvent @event) => NotificationPanel.Current?.RunSynchronously(() => EnsureAudioClips(@event));

        private static void EnsureAudioClips(ConfigurationChangedEvent @event = null)
        {
            if (!NotificationPanel.Current.World.CanCurrentThreadModify) return;

            if (@event == null || @event.Key == NotificationSoundUri)
            {
                joinLeaveAudioClip ??= NotificationPanel.Current.Slot.AttachComponent<StaticAudioClip>();

                joinLeaveAudioClip.URL.Value = config.GetValue(NotificationSoundUri);
            }

            if (@event == null || @event.Key == NotificationLeaveSoundUri)
            {
                leaveAudioClip ??= NotificationPanel.Current.Slot.AttachComponent<StaticAudioClip>();

                leaveAudioClip.URL.Value = config.GetValue(NotificationLeaveSoundUri);
            }
        }
    }
}
