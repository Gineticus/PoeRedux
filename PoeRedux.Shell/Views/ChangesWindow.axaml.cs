using Avalonia.Controls;
using PoeRedux.ViewModels;

namespace PoeRedux.Views;

public partial class ChangesWindow : Window
{
    public ChangesWindow() => InitializeComponent();

    public ChangesWindow(ChangesViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Opened += async (_, _) => await viewModel.LoadAsync();
        Closed += (_, _) => viewModel.Dispose();
    }
}
