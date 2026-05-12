using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace Barcode_File_Find
{
    public partial class FileSelectionWindow : Window
    {
        public string? SelectedFilePath { get; private set; }

        public FileSelectionWindow(IEnumerable<string> filePaths)
        {
            InitializeComponent();
            FilesListBox.ItemsSource = filePaths;
        }

        private void OpenSelected()
        {
            if (FilesListBox.SelectedItem is string selected)
            {
                SelectedFilePath = selected;
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("먼저 파일을 선택해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSelected();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void FilesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelected();
        }
    }
}

