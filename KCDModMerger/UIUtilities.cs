#region usings

using System;
using System.Windows.Controls;
using System.Windows.Threading;

#endregion

namespace KCDModMerger
{
    internal static class UIUtilities
    {
        /// <summary>
        /// Invokes if required.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="action">The action.</param>
        /// <param name="priority">The priority.</param>
        internal static void InvokeIfRequired(this Control control, Action action,
            DispatcherPriority priority = DispatcherPriority.Background)
        {
            if (!control.Dispatcher.CheckAccess())
                control.Dispatcher.Invoke(action, priority);
            else
                action.Invoke();
        }

        /// <summary>
        /// Disables the button.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <param name="tooltip">The tooltip.</param>
        internal static void DisableButton(this Button button, string tooltip = null)
        {
            button.InvokeIfRequired(() =>
            {
                button.IsEnabled = false;
                button.ToolTip = tooltip;
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Enables the button.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <param name="tooltip">The tooltip.</param>
        internal static void EnableButton(this Button button, string tooltip = null)
        {
            button.InvokeIfRequired(() =>
            {
                button.IsEnabled = true;
                button.ToolTip = tooltip;
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Determines whether this instance is default.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <returns>
        ///   <c>true</c> if the specified value is default; otherwise, <c>false</c>.
        /// </returns>
        internal static bool IsDefault<T>(this T value) where T : struct
        {
            bool isDefault = value.Equals(default(T));

            return isDefault;
        }
    }
}