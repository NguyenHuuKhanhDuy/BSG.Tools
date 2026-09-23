using System.Windows;

namespace BSG.Tools
{
    /// <summary>
    /// Marks an input whose value differs from the last saved settings. The default
    /// TextBox template shows a "modified" bar at its left edge while this is true.
    /// </summary>
    public static class ModifiedState
    {
        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.RegisterAttached("IsModified", typeof(bool), typeof(ModifiedState),
                new FrameworkPropertyMetadata(false));

        public static bool GetIsModified(DependencyObject element) => (bool)element.GetValue(IsModifiedProperty);

        public static void SetIsModified(DependencyObject element, bool value) => element.SetValue(IsModifiedProperty, value);
    }
}
