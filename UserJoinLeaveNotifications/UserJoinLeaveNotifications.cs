using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using BaseX;
using System.Reflection;

namespace UserJoinLeaveNotifications
{
    public class UserJoinLeaveNotifications : NeosMod
    {
        public override string Name => "UserJoinLeaveNotifications";
        public override string Author => "badhaloninja";
        public override string Version => "1.0.0";
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

            [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)] // Snapshot incase you are using the DesktopNotifications mod
            // HarmonyReversePatch has to be under a harmony patch class???
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
                oldFocusedWorld.UserJoined -= OnUserJoined;
                oldFocusedWorld.UserLeft -= OnUserLeft;
            }
            private void OnWorldFocused(World newFocusedWorld)
            {
                if(oldFocusedWorld != null)
                {
                    oldFocusedWorld.UserJoined -= OnUserJoined;
                    oldFocusedWorld.UserLeft -= OnUserLeft;
                }
                

                newFocusedWorld.UserJoined += OnUserJoined;
                newFocusedWorld.UserLeft += OnUserLeft;

                oldFocusedWorld = newFocusedWorld;
            }
            private void OnUserJoined(User user)
            {
                if (user.IsLocalUser) return;
                notificationPanel.RunSynchronously(() =>
                { // Events are nonlocking threads
                    notifPanelPatch.AddNotification(notificationPanel, user.UserID, String.Format("{0} joined", user.UserName), null, MathX.Lerp(color.Blue, color.White, 0.5f), "N/A", null, null);
                });
            }
            private void OnUserLeft(User user)
            {
                if (user.IsLocalUser) return;
                notificationPanel.RunSynchronously(() =>
                {
                    notifPanelPatch.AddNotification(notificationPanel, user.UserID, String.Format("{0} left", user.UserName), null, MathX.Lerp(color.Red, color.White, 0.5f), "N/A", null, null);
                });
            }
        }
    }
}