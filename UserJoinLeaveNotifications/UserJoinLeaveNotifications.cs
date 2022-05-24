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
        public override string Version => "1.2.0";
        public override string Link => "https://github.com/badhaloninja/UserJoinLeaveNotifications";
        public override void OnEngineInit()
        {
            Engine.Current.RunPostInit(Setup);
        }
        

        private static MethodInfo addNotification;

        private static UserBag currentUserBag;
        
        
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
                
                AddNotification(string.Format("{0} joined", user.UserName),MathX.Lerp(color.Blue, color.White, 0.5f), "User Joined", thumbnail);
            });
        }

        private static async void OnUserLeft(SyncBagBase<RefID, User> bag, RefID key, User user)
        {
            if (user.IsLocalUser) return;
            if (NotificationPanel.Current == null) return;
            
            Uri thumbnail = await GetUserThumbnail(user.UserID);
            AddNotification(string.Format("{0} left", user.UserName), MathX.Lerp(color.Red, color.White, 0.5f), "User Left", thumbnail);
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
        
        private static void AddNotification(string message, color backgroundColor, string mainMessage, Uri overrideProfile)
        {
            // Not using Show Notification because it does not expose main message
            if (NotificationPanel.Current == null) return;
            NotificationPanel.Current.RunSynchronously(() =>
            { // ;-;
                addNotification.Invoke(NotificationPanel.Current, new object[] { null, message, null, backgroundColor, mainMessage, overrideProfile, null });
            });
        }
    }
}