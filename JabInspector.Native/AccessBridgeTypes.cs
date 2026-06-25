using System.Runtime.InteropServices;

namespace JabInspector.Native;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct AccessibleContextInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string Name;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string Description;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Role;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string RoleEnUs;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string States;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string StatesEnUs;
    public int IndexInParent;
    public int ChildrenCount;
    public int X;
    public int Y;
    public int Width;
    public int Height;
    [MarshalAs(UnmanagedType.Bool)] public bool AccessibleComponent;
    [MarshalAs(UnmanagedType.Bool)] public bool AccessibleAction;
    [MarshalAs(UnmanagedType.Bool)] public bool AccessibleSelection;
    [MarshalAs(UnmanagedType.Bool)] public bool AccessibleText;
    [MarshalAs(UnmanagedType.Bool)] public bool AccessibleInterfaces;
    [MarshalAs(UnmanagedType.Bool)] public bool AccessibleValue;
    [MarshalAs(UnmanagedType.Bool)] public bool AccessibleTable;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct AccessibleActionInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct AccessibleActions
{
    public int ActionsCount;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public AccessibleActionInfo[] ActionInfo;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct AccessibleActionsToDo
{
    public int ActionsCount;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public AccessibleActionInfo[] Actions;
}

[StructLayout(LayoutKind.Sequential)]
public struct AccessibleTextInfo
{
    public int CharCount;
    public int CaretIndex;
    public int IndexAtPoint;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct AccessibleTextItemsInfo
{
    public char Letter;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Word;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string Sentence;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct AccessibleTextSelectionInfo
{
    public int SelectionStartIndex;
    public int SelectionEndIndex;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string SelectedText;
}
