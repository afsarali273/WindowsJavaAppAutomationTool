namespace JabInspector.Core.Diagnostics;

public sealed class InspectorLogger
{
    public event Action<string>? MessageLogged;
    public void Log(string message) => MessageLogged?.Invoke($"{DateTime.Now:HH:mm:ss}  {message}");
}
