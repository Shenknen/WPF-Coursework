using System.Windows;
using EquipmentDesk.Wpf.ViewModels;

namespace EquipmentDesk.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
