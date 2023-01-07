using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using BaseX;
using System.Reflection;

using System.Threading.Tasks;

namespace UserJoinLeaveNotifications
{
    public class UserJoinLeaveNotifications : NeosMod
    {
        public override string Name => "UserJoinLeaveNotifications";
        public override string Author => "badhaloninja";
        public override string Version => "1.4.0";
        public override string Link => "https://github.com/badhaloninja/UserJoinLeaveNotifications";
        public override void OnEngineInit()
        {
            config = GetConfiguration();
            config.OnThisConfigurationChanged += UpdateAssets;

            
            Engine.Current.RunPostInit(Setup);
        }


        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Uri> NotificationSoundUri = new ModConfigurationKey<Uri>("NotificationSound", "Notification sound for user joining or leaving - Disabled when null", () => null);
        
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> OverrideLeaveSound = new ModConfigurationKey<bool>("OverrideLeaveSound", "Override the notification sound for leaving", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Uri> NotificationLeaveSoundUri = new ModConfigurationKey<Uri>("NotificationLeaveSound", "Notification sound for leaving - Only used if override is enabled - Disabled when null", () => null);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> FriendLinks = new ModConfigurationKey<bool>("FriendLinks", "Add FriendLinks to notifications, can make notifiacation titles only show username", () => true);
        
        private static ModConfiguration config;
        private static MethodInfo addNotification;

        private static UserBag currentUserBag;

        private static StaticAudioClip joinLeaveAudioClip;
        private static StaticAudioClip leaveAudioClip;

        public static void Setup()
        {
            // Hook into the world focused event
            Engine.Current.WorldManager.WorldFocused += OnWorldFocused;

            // Store the method info for later use
            addNotification = AccessTools.Method(typeof(NotificationPanel), "AddNotification", new Type[] { typeof(string), typeof(string), typeof(Uri), typeof(color), typeof(string), typeof(Uri), typeof(IAssetProvider<AudioClip>) });
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
            if (user.IsLocalUser) return;
            if (NotificationPanel.Current == null) return;

            NotificationPanel.Current.RunInUpdates(3, async () =>
            { // Running immediately results in the getuser to return a BadRequest
                Uri thumbnail = await GetUserThumbnail(user.UserID);
                AddNotification(user.UserID ,string.Format("{0} joined", user.UserName),MathX.Lerp(color.Blue, color.White, 0.5f), "User Joined", thumbnail, false);
            });
        }

        private static async void OnUserLeft(SyncBagBase<RefID, User> bag, RefID key, User user)
        {
            if (user.IsLocalUser) return;
            if (NotificationPanel.Current == null) return;
            
            Uri thumbnail = await GetUserThumbnail(user.UserID);
            AddNotification(user.UserID, string.Format("{0} left", user.UserName), MathX.Lerp(color.Red, color.White, 0.5f), "User Left", thumbnail, true);
        }

        
        
        // Async method to fetch thumbnail from user id
        private static async Task<Uri> GetUserThumbnail(string userId)
        {
            var cloudUserProfile = (await Engine.Current.Cloud.GetUser(userId))?.Entity?.Profile;
            // Handle fetching profile, AddNotification only gets profile data for friends
            var thumbnail = CloudX.Shared.CloudXInterface.TryFromString(cloudUserProfile?.IconUrl) ?? NeosAssets.Graphics.Thumbnails.AnonymousHeadset;
            return thumbnail;
        }
        
        private static UserBag GetUserbag(World world)
        { // Return the user bag field
            return Traverse.Create(world).Field("_users").GetValue<UserBag>();
        }

        private static void AddNotification(string userId, string message, color backgroundColor, string mainMessage, Uri overrideProfile, bool UserLeaving)
        {
            // Not using Show Notification because it does not expose main message
            if (addNotification == null || NotificationPanel.Current == null) return;

            NotificationPanel.Current.RunSynchronously(() =>
            { // ;-;
                if (joinLeaveAudioClip == null)
                {
                    joinLeaveAudioClip = NotificationPanel.Current.Slot.AttachComponent<StaticAudioClip>();
                    joinLeaveAudioClip.URL.Value = config.GetValue(NotificationSoundUri);
                }
                if (leaveAudioClip == null)
                {
                    leaveAudioClip = NotificationPanel.Current.Slot.AttachComponent<StaticAudioClip>();
                    leaveAudioClip.URL.Value = config.GetValue(NotificationLeaveSoundUri);
                }

                StaticAudioClip clip = (joinLeaveAudioClip.URL.Value != null) ? joinLeaveAudioClip : null;

                if (UserLeaving && config.GetValue(OverrideLeaveSound))
                {
                    clip = (leaveAudioClip.URL.Value != null) ? leaveAudioClip : null;
                }
                
                addNotification.Invoke(NotificationPanel.Current, new object[] { config.GetValue(FriendLinks) ? userId : null, message, null, backgroundColor, mainMessage, overrideProfile, clip });
            });
        }
        
        private void UpdateAssets(ConfigurationChangedEvent @event)
        {
            if (@event.Key == NotificationSoundUri && joinLeaveAudioClip != null)
            {
                joinLeaveAudioClip.URL.Value = config.GetValue(NotificationSoundUri);
            }
            if (@event.Key == NotificationLeaveSoundUri && leaveAudioClip != null)
            {
                leaveAudioClip.URL.Value = config.GetValue(NotificationLeaveSoundUri);
            }
        }
    }
}
