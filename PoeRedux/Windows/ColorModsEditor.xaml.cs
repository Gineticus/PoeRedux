using PoeRedux.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;

namespace PoeRedux;

public partial class ColorModsEditor : Window
{
    private readonly ObservableCollection<ColorModsViewModel> _colorMods;

    public ColorModsEditor(ObservableCollection<ColorModsViewModel> colorMods)
    {
        InitializeComponent();
        _colorMods = colorMods;
        ColorModsItemsControl.ItemsSource = colorMods;
        DarkTitleBar.Enable(this);
    }

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
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "color_mods.json"
        };
        if (saveFileDialog.ShowDialog() == true)
        {
            var colorModsDict = new Dictionary<string, object>();
            foreach (var mod in _colorMods)
            {
                colorModsDict[mod.Name] = new
                {
                    enabled = mod.IsSelected,
                    color = mod.SelectedColor,
                };
            }
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = JsonSerializer.Serialize(colorModsDict, options);
            File.WriteAllText(saveFileDialog.FileName, json);
            MessageBox.Show("Color mods saved successfully.", "Success");
        }
    }

    private void LoadConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
        };
        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(openFileDialog.FileName);
                var colorModsDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json);
                if (colorModsDict != null)
                {
                    foreach (var mod in _colorMods)
                    {
                        if (colorModsDict.TryGetValue(mod.Name, out var modData))
                        {
                            if (modData.TryGetValue("enabled", out var enabledObj)
                                && (enabledObj.ValueKind == JsonValueKind.True || enabledObj.ValueKind == JsonValueKind.False))
                            {
                                mod.IsSelected = enabledObj.GetBoolean();
                            }
                            if (modData.TryGetValue("color", out var colorObj)
                                && colorObj.ValueKind == JsonValueKind.String)
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
