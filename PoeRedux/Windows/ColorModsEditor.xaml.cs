using PoeRedux.Models;
using System.Collections.ObjectModel;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PoeRedux;

public partial class ColorModsEditor : Window
{
    public ColorModsEditor(ObservableCollection<ColorModsViewModel> colorMods)
    {
        InitializeComponent();
        ColorModsItemsControl.ItemsSource = colorMods;
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
    }

    private void ApplyDarkTitleBar()
    {
        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            IntPtr hwnd = hwndSource.Handle;

            // Use DWMWA_USE_IMMERSIVE_DARK_MODE (20) for Windows 11 / Windows 10 build 19041+
            int attribute = 20;
            int useImmersiveDarkMode = 1;
            DwmSetWindowAttribute(hwnd, attribute, ref useImmersiveDarkMode, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var colorMods = ColorModsItemsControl.ItemsSource as ObservableCollection<ColorModsViewModel>;
        if (colorMods == null)
        {
            MessageBox.Show("No color mods to save.", "Error");
            return;
        }
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "color_mods.json"
        };
        if (saveFileDialog.ShowDialog() == true)
        {
            var colorModsDict = new Dictionary<string, object>();
            foreach (var mod in colorMods)
            {
                colorModsDict[mod.Name] = new
                {
                    enabled = mod.IsSelected,
                    color = mod.SelectedColor,
                };
            }
            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = System.Text.Json.JsonSerializer.Serialize(colorModsDict, options);
            System.IO.File.WriteAllText(saveFileDialog.FileName, json);
            MessageBox.Show("Color mods saved successfully.", "Success");
        }
    }

    private void LoadConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var colorMods = ColorModsItemsControl.ItemsSource as ObservableCollection<ColorModsViewModel>;
        if (colorMods == null)
        {
            MessageBox.Show("No color mods to load.", "Error");
            return;
        }
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
        };
        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var json = System.IO.File.ReadAllText(openFileDialog.FileName);
                var colorModsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>>(json);
                if (colorModsDict != null)
                {
                    foreach (var mod in colorMods)
                    {
                        if (colorModsDict.TryGetValue(mod.Name, out var modData))
                        {
                            if (modData.TryGetValue("enabled", out var enabledObj) 
                                && (enabledObj.ValueKind == System.Text.Json.JsonValueKind.True || enabledObj.ValueKind == System.Text.Json.JsonValueKind.False))
                            {
                                mod.IsSelected = enabledObj.GetBoolean();
                            }
                            if (modData.TryGetValue("color", out var colorObj) 
                                && colorObj.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                mod.SelectedColor = colorObj.GetString() ?? string.Empty;
                            }
                        }
                    }
                    MessageBox.Show("Color mods loaded successfully.", "Success");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load color mods: {ex.Message}", "Error");
            }
        }
    }

    public static bool Show(ObservableCollection<ColorModsViewModel> colorMods)
    {
        var dialog = new ColorModsEditor(colorMods);

        if (Application.Current.MainWindow != null)
        {
            dialog.Owner = Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        return dialog.ShowDialog() == true;
    }
}
