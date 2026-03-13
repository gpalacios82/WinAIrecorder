// Disambiguate WPF vs WinForms types that conflict when UseWindowsForms=true
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using Brush = System.Windows.Media.Brush;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using Clipboard = System.Windows.Clipboard;
