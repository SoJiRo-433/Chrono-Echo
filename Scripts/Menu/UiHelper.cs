using Godot;
using System;

/// <summary>Shared font/theme plus quick popup dialogs.</summary>
public static class UiHelper
{
    private const string FontPath = "res://Assets/Fonts/NotoSerifJP-VariableFont_wght.ttf";
    private static Theme _theme;

    public static Theme GetTheme()
    {
        if (_theme != null) return _theme;

        _theme = new Theme { DefaultFontSize = 26 };
        var font = ResourceLoader.Load<Font>(FontPath);
        if (font != null) _theme.DefaultFont = font;
        return _theme;
    }

    /// <summary>Yes/Cancel popup. onConfirm runs only if the player confirms.</summary>
    public static void Confirm(Node parent, string title, string message, Action onConfirm,
                               string okText = "Yes", string cancelText = "Cancel")
    {
        var dialog = new ConfirmationDialog
        {
            Title = title,
            DialogText = message,
            OkButtonText = okText,
            CancelButtonText = cancelText,
            DialogAutowrap = true,
            Theme = GetTheme(),
            ProcessMode = Node.ProcessModeEnum.Always
        };
        dialog.Confirmed += () => onConfirm?.Invoke();
        Show(parent, dialog);
    }

    /// <summary>Simple message popup with an OK button.</summary>
    public static void Notify(Node parent, string title, string message)
    {
        var dialog = new AcceptDialog
        {
            Title = title,
            DialogText = message,
            OkButtonText = "OK",
            DialogAutowrap = true,
            Theme = GetTheme(),
            ProcessMode = Node.ProcessModeEnum.Always
        };
        Show(parent, dialog);
    }

    private static void Show(Node parent, AcceptDialog dialog)
    {
        parent.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(620, 240));
        dialog.VisibilityChanged += () =>
        {
            if (!dialog.Visible) dialog.QueueFree();
        };
    }
}
