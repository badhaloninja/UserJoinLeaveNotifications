using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using BaseX;
using System.Reflection;
using FrooxEngine.LogiX.WorldModel;
namespace UserJoinLeaveNotifications
{
    public class UserJoinLeaveNotifications : NeosMod
    {
        public override string Name => "UserJoinLeaveNotifications";
        public override string Author => "badhaloninja";
        public override string Version => "1.1.1";
        public override string Link => "https://github.com/badhaloninja/UserJoinLeaveNotifications";
        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("me.badhaloninja.UserJoinLeaveNotifications");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(NotificationPanel), "OnAttach")]
        class notifPanelPatch
        {
            public static void Postfix(NotificationPanel __instance)
            {
                new NewNotificationHandler().Setup(__instance);
            }

            // HarmonyReversePatch has to be under a harmony patch class???
            [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)] // Snapshot incase you are using the DesktopNotifications mod
            [HarmonyPatch(typeof(NotificationPanel), "AddNotification", new Type[] { typeof(string), typeof(string), typeof(Uri), typeof(color), typeof(string), typeof(Uri), typeof(IAssetProvider<AudioClip>) })]
            public static void AddNotification(NotificationPanel instance, string userId, string message, Uri thumbnail, color backgroundColor, string mainMessage, Uri overrideProfile, IAssetProvider<AudioClip> clip)
            {
                throw new NotImplementedException("It's a stub");
            }
        }

        class NewNotificationHandler
        { // To handle the small chance that the functions would need to be removed from the events incase the component gets deleted (very unlikely under normal circumstances)
            private NotificationPanel notificationPanel;
            private World oldFocusedWorld;

            public void Setup(NotificationPanel instance)
            {
                notificationPanel = instance;

                instance.Engine.WorldManager.WorldFocused += OnWorldFocused;

                instance.Destroyed += cleanUp;
            }

            private void cleanUp(IChangeable changeable)
            {
                oldFocusedWorld.WorldManager.WorldFocused -= OnWorldFocused;

                /* Join leave events only work as host 
                 * 
                 * Interfacing with the UserBag here so I don't have to manually check for changes in some loop
                 * and to avoid patching core functions
                 * 
                 */
                
                GetUserbag(oldFocusedWorld).OnElementAdded -= OnUserJoined;
                GetUserbag(oldFocusedWorld).OnElementRemoved -= OnUserLeft;
            }
            private void OnWorldFocused(World newFocusedWorld)
            {
                if (oldFocusedWorld != null)
                {
                    GetUserbag(oldFocusedWorld).OnElementAdded -= OnUserJoined;
                    GetUserbag(oldFocusedWorld).OnElementRemoved -= OnUserLeft;
                }
                GetUserbag(newFocusedWorld).OnElementAdded += OnUserJoined;
                GetUserbag(newFocusedWorld).OnElementRemoved += OnUserLeft;

                oldFocusedWorld = newFocusedWorld;
            }
            private void OnUserJoined(SyncBagBase<RefID, User> bag, RefID key, User user, bool isNew)
            {
                if (user.IsLocalUser) return;
                notificationPanel.RunInUpdates(3, async () =>
                { // Running immediately results in the getuser to return a BadRequest
                    var cloudUserProfile = (await Engine.Current.Cloud.GetUser(user.UserID))?.Entity?.Profile;
                    // Handle fetching profile, AddNotification only gets profile data for friends
                    var thumbnail = CloudX.Shared.CloudXInterface.TryFromString(cloudUserProfile?.IconUrl) ?? NeosAssets.Graphics.Thumbnails.AnonymousHeadset;
                    notificationPanel.RunSynchronously(() =>
                    { // Events are nonlocking threads
                        notifPanelPatch.AddNotification(notificationPanel, null, string.Format("{0} joined", user.UserName), null, MathX.Lerp(color.Blue, color.White, 0.5f), "User Joined", thumbnail, null);
                    });
                });
                
            }
            private async void OnUserLeft(SyncBagBase<RefID, User> bag, RefID key, User user)
            {
                if (user.IsLocalUser) return;
                var cloudUserProfile = (await Engine.Current.Cloud.GetUser(user.UserID))?.Entity?.Profile;
                // Handle fetching profile, AddNotification only gets profile data for friends
                var thumbnail = CloudX.Shared.CloudXInterface.TryFromString(cloudUserProfile?.IconUrl) ?? NeosAssets.Graphics.Thumbnails.AnonymousHeadset;
                notificationPanel.RunSynchronously(() =>
                { // Events are nonlocking threads
                    notifPanelPatch.AddNotification(notificationPanel, null, string.Format("{0} left", user.UserName), null, MathX.Lerp(color.Red, color.White, 0.5f), "User Left", thumbnail, null);
                });
            }
            private static UserBag GetUserbag(World world)
            { // Return the user bag field
                return Traverse.Create(world).Field("_users").GetValue<UserBag>();
            }
        }
        
    }
}