using System.Windows;
namespace JabInspector.App.Helpers;
public static class ClipboardHelper { public static void SetText(string text) => System.Windows.Clipboard.SetText(text); }
