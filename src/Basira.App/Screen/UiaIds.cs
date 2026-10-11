namespace Basira.App.Screen;

/// <summary>UI Automation property, pattern and control-type ids (UIAutomationClient.h), kept local so they are explicit.</summary>
internal static class UiaIds
{
    // Properties
    public const int RuntimeId = 30000;
    public const int BoundingRectangle = 30001;
    public const int ProcessId = 30002;
    public const int ControlType = 30003;
    public const int LocalizedControlType = 30004;
    public const int Name = 30005;
    public const int HasKeyboardFocus = 30008;
    public const int IsKeyboardFocusable = 30009;
    public const int IsEnabled = 30010;
    public const int AutomationId = 30011;
    public const int ClassName = 30012;
    public const int HelpText = 30013;
    public const int IsPassword = 30019;
    public const int NativeWindowHandle = 30020;
    public const int IsOffscreen = 30022;
    public const int IsExpandCollapsePatternAvailable = 30028;
    public const int IsInvokePatternAvailable = 30031;
    public const int IsScrollPatternAvailable = 30034;
    public const int IsSelectionItemPatternAvailable = 30036;
    public const int IsTextPatternAvailable = 30040;
    public const int IsTogglePatternAvailable = 30041;
    public const int IsValuePatternAvailable = 30043;
    public const int ValueValue = 30045;
    public const int ValueIsReadOnly = 30046;
    public const int ScrollVerticallyScrollable = 30058;
    public const int ScrollHorizontallyScrollable = 30057;
    public const int IsLegacyIAccessiblePatternAvailable = 30090;
    public const int AriaRole = 30101;
    public const int AriaProperties = 30102;
    public const int HeadingLevel = 30173;
    public const int IsDialog = 30174;

    // Patterns
    public const int InvokePattern = 10000;
    public const int ValuePattern = 10002;
    public const int ScrollPattern = 10004;
    public const int ExpandCollapsePattern = 10005;
    public const int SelectionItemPattern = 10010;
    public const int TextPattern = 10014;
    public const int TogglePattern = 10015;
    public const int ScrollItemPattern = 10017;
    public const int LegacyIAccessiblePattern = 10018;

    // Control types
    public const int Button = 50000;
    public const int Calendar = 50001;
    public const int CheckBox = 50002;
    public const int ComboBox = 50003;
    public const int Edit = 50004;
    public const int Hyperlink = 50005;
    public const int Image = 50006;
    public const int ListItem = 50007;
    public const int List = 50008;
    public const int Menu = 50009;
    public const int MenuBar = 50010;
    public const int MenuItem = 50011;
    public const int ProgressBar = 50012;
    public const int RadioButton = 50013;
    public const int ScrollBar = 50014;
    public const int Slider = 50015;
    public const int Spinner = 50016;
    public const int StatusBar = 50017;
    public const int Tab = 50018;
    public const int TabItem = 50019;
    public const int Text = 50020;
    public const int ToolBar = 50021;
    public const int ToolTip = 50022;
    public const int Tree = 50023;
    public const int TreeItem = 50024;
    public const int Custom = 50025;
    public const int Group = 50026;
    public const int Thumb = 50027;
    public const int DataGrid = 50028;
    public const int DataItem = 50029;
    public const int Document = 50030;
    public const int SplitButton = 50031;
    public const int Window = 50032;
    public const int Pane = 50033;
    public const int Header = 50034;
    public const int HeaderItem = 50035;
    public const int Table = 50036;
    public const int TitleBar = 50037;
    public const int Separator = 50038;

    // HeadingLevel property values: HeadingLevel_None = 80050, HeadingLevel1..9 = 80051..80059.
    public const int HeadingLevelNone = 80050;

    // Text units
    public const int TextUnitCharacter = 0;
}
