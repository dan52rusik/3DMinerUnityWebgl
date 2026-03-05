using UnityEngine;
using System.Collections.Generic;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Centralized registry to track if any game menu/window is currently open.
    /// Used to block character movement and mining while interacting with UI.
    /// </summary>
    public static class GameUIWindow
    {
        private static readonly HashSet<GameObject> activeWindows = new HashSet<GameObject>();

        /// <summary>
        /// Call this when a UI window is shown or hidden.
        /// </summary>
        public static void SetWindowActive(GameObject windowGo, bool active)
        {
            if (windowGo == null) return;

            if (active)
                activeWindows.Add(windowGo);
            else
                activeWindows.Remove(windowGo);

            // Cleanup null references (in case some windows were destroyed)
            activeWindows.RemoveWhere(item => item == null);
        }

        /// <summary>
        /// Returns true if at least one tracked UI window is active.
        /// </summary>
        public static bool IsAnyWindowActive()
        {
            activeWindows.RemoveWhere(item => item == null);
            return activeWindows.Count > 0;
        }
    }
}
